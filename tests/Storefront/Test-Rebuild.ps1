param(
    [string] $BaseUri = 'http://localhost:5521',
    [ValidateRange(1,200)] [int] $Count = 100,
    [Parameter(Mandatory)] [string] $EvidenceFile
)
$ErrorActionPreference='Stop'
if (!([uri]$BaseUri).IsLoopback) { throw 'These tests require the local demo.' }
function Request([string] $path, [string] $method='Get', $body=$null) {
    $arguments=@{ Uri=$BaseUri+$path; Method=$method; TimeoutSec=180 }
    if ($null -ne $body) { $arguments.Body=ConvertTo-Json -InputObject $body -Depth 4; $arguments.ContentType='application/json' }
    Invoke-RestMethod @arguments | Write-Output
}
function Await([scriptblock] $condition) {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    do { if (& $condition) { return }; Start-Sleep -Seconds 1 } while ($timer.Elapsed.TotalSeconds -lt 180)
    throw 'Batch ingestion did not complete.'
}
$fixture=$null; $failure=$null; $result=$null
try {
    $before=@(Request '/smoke/requests').Count
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $fixture=Request "/smoke/articles/$Count" 'Post'
    Await { @((Request "/smoke/content/$($fixture.keys[0])").Contents).Count -eq 1 -and @((Request "/smoke/content/$($fixture.keys[-1])").Contents).Count -eq 1 }
    $publicationMilliseconds=$timer.ElapsedMilliseconds
    $publishedRequests=@(Request '/smoke/requests') | Select-Object -Skip $before
    $publicationWrites=@($publishedRequests | Where-Object operation -Match '/BatchedTrackingRequest$').Count
    $before=@(Request '/smoke/requests').Count
    $memoryBefore=Request '/smoke/runtime'
    $timer.Restart()
    [void](Request '/smoke/rebuild' 'Post')
    $rebuildMilliseconds=$timer.ElapsedMilliseconds
    $rebuiltRequests=@(Request '/smoke/requests') | Select-Object -Skip $before
    $result=[pscustomobject]@{
        fixtureCount=$Count; publicationWrites=$publicationWrites; publicationMilliseconds=$publicationMilliseconds; rebuildMilliseconds=$rebuildMilliseconds
        publicationOperations=@($publishedRequests | Group-Object operation | Select-Object Name,Count)
        rebuildOperations=@($rebuiltRequests | Group-Object operation | Select-Object Name,Count)
        publicationRequestBytes=($publishedRequests | Measure-Object requestBytes -Sum).Sum
        rebuildRequestBytes=($rebuiltRequests | Measure-Object requestBytes -Sum).Sum
        before=$memoryBefore; after=(Request '/smoke/runtime')
    }
    if (@($publishedRequests | Where-Object operation -Match '/(ProductQuery|ContentQuery)$').Count -ne 0) { throw 'Ordinary publication unexpectedly reads remote entities.' }
    if ($Count -ge 10 -and $publicationWrites -ge $Count) { throw 'The bulk fixture did not combine any publication requests.' }
    if (@($rebuiltRequests | Where-Object operation -Match '/(ProductQuery|ContentQuery)$').Count -gt 2) { throw 'The small rebuild unexpectedly performs per-entity remote reads.' }
    Write-Output "Passed: $Count publications send no remote lookup requests; rebuild uses fixed reconciliation queries."
} catch { $failure=$_.Exception.Message; throw }
finally {
    if ($fixture) {
        [void](Request '/smoke/remove-fixtures' 'Post' @($fixture.keys))
        Await { @((Request "/smoke/content/$($fixture.keys[0])").Contents).Count -eq 0 -and @((Request "/smoke/content/$($fixture.keys[-1])").Contents).Count -eq 0 }
    }
    [pscustomobject]@{ passed=($null -eq $failure); failure=$failure; result=$result } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $EvidenceFile
}
