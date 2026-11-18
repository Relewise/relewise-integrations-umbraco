param(
    [string] $BaseUri='http://localhost:5521',
    [Parameter(Mandatory)] [string] $EvidenceFile
)
$ErrorActionPreference='Stop'
if (!([uri]$BaseUri).IsLoopback) { throw 'These tests require the local demo.' }
$checks=[Collections.Generic.List[string]]::new()
function Request([string] $path,[string] $method='Get',$body=$null) {
    $arguments=@{Uri=$BaseUri+$path;Method=$method;TimeoutSec=60}
    if ($null -ne $body) { $arguments.Body=$body | ConvertTo-Json -Depth 5; $arguments.ContentType='application/json' }
    Invoke-RestMethod @arguments | Write-Output
}
function Await([scriptblock] $condition,[string] $description) {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    do {
        if (& $condition) { $checks.Add($description); Write-Output "Passed: $description"; return }
        Start-Sleep -Seconds 1
    } while ($timer.Elapsed.TotalSeconds -lt 180)
    throw "Timed out: $description"
}
$fixtures=[Collections.Generic.List[object]]::new();$failure=$null
try {
    foreach($cycle in @('unpublish-publish','unpublish-delete','unpublish-publish-delete')) {
        $fixture=Request '/smoke/article' 'Post' @{Name=('Ordering fixture '+[guid]::NewGuid().ToString('N'));Text='Published ordering fixture.'}
        $fixtures.Add($fixture)
        Await { @((Request "/smoke/content/$($fixture.key)").Contents).Count -eq 1 } "$cycle fixture is exported"
        [void](Request "/smoke/cycle/$($fixture.key)/$cycle" 'Post')
        if ($cycle -eq 'unpublish-publish') {
            Await {
                $stored=(Request "/smoke/content/$($fixture.key)").Contents[0]
                $null -ne $stored -and !$stored.Disabled -and $stored.Data.rwUmbracoPublished.Value -eq $true
            } 'Rapid unpublish/republish retains a published enabled entity'
        } else {
            Await { @((Request "/smoke/content/$($fixture.key)").Contents).Count -eq 0 } "$cycle removes the entity"
        }
        # Rebuild is awaited through the same queue; earlier changes cannot be replayed afterward.
        [void](Request '/smoke/rebuild' 'Post')
        if ($cycle -eq 'unpublish-publish') {
            $stored=(Request "/smoke/content/$($fixture.key)").Contents[0]
            if ($null -eq $stored -or $stored.Disabled) { throw 'Rebuild lost the republished entity.' }
        } elseif (@((Request "/smoke/content/$($fixture.key)").Contents).Count -ne 0) { throw 'A rebuild restored a deleted fixture.' }
        $checks.Add("Rebuild preserves the outcome of $cycle")
        if ($cycle.EndsWith('-delete')) { [void]$fixtures.Remove($fixture) }
    }
} catch { $failure=$_.Exception.Message;throw }
finally {
    foreach($fixture in $fixtures) {
        try {
            [void](Request "/smoke/state/$($fixture.key)/delete" 'Post')
            Await { @((Request "/smoke/content/$($fixture.key)").Contents).Count -eq 0 } 'Ordering fixture cleanup completes'
        } catch {
            if ($null -eq $failure) { $failure=$_.Exception.Message }
            Write-Warning 'Ordering fixture cleanup failed; inspect the local demo.'
        }
    }
    [pscustomobject]@{passed=($null -eq $failure);failure=$failure;assertions=$checks.Count;checks=$checks;runtime=(Request '/smoke/runtime')} |
        ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $EvidenceFile
}
