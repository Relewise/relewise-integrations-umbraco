param(
    [string] $BaseUri = 'http://localhost:5521',
    [Parameter(Mandatory)] [string] $EvidenceFile,
    [switch] $ExpectNoProfiles
)
$ErrorActionPreference = 'Stop'
if (!([uri]$BaseUri).IsLoopback) { throw 'These tests require the local demo.' }
$checks = [Collections.Generic.List[object]]::new()
$started = [Diagnostics.Stopwatch]::StartNew()
function Request([string] $path, [string] $method = 'Get', $body = $null) {
    $arguments = @{ Uri=$BaseUri + $path; Method=$method; TimeoutSec=45 }
    if ($null -ne $body) { $arguments.Body=$body | ConvertTo-Json -Depth 8; $arguments.ContentType='application/json' }
    Invoke-RestMethod @arguments | Write-Output
}
function Wait-Check([scriptblock] $condition, [string] $description) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $lastError = ''
    do {
        try {
            if (& $condition) {
                $checks.Add([pscustomobject]@{ check=$description; milliseconds=$timer.ElapsedMilliseconds })
                Write-Output "Passed: $description"
                return
            }
        } catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 1
    } while ($timer.Elapsed.TotalSeconds -lt 180)
    throw "Timed out: $description. $lastError"
}
$fixture = $null
$failure = $null
try {
    $pipelines = @(Request '/smoke/pipelines')
    if (@($pipelines | Where-Object indexer -EQ 'MappedEntityIndexer').Count -ne 1) { throw 'Expected one Relewise writer registration.' }
    if (($pipelines | Where-Object indexAlias -EQ 'Umb_PublishedContent').indexer -ne 'IExamineIndexer') { throw 'Examine indexing was replaced.' }
    $checks.Add([pscustomobject]@{ check='One Relewise pipeline and the original Examine indexer'; milliseconds=0 })
    if ($ExpectNoProfiles) {
        if (($pipelines | Where-Object indexAlias -EQ 'Umb_PublishedContent').searcher -ne 'IExamineSearcher') { throw 'Examine is not the ordinary search provider.' }
        try {
            [void](Request '/smoke/query?provider=articles')
            throw 'A Relewise search profile is unexpectedly registered.'
        } catch {
            if ($_.Exception.Response.StatusCode -ne 404) { throw }
        }
        $checks.Add([pscustomobject]@{ check='No Relewise query profiles; Examine serves ordinary searches'; milliseconds=0 })
    }
    $run = [guid]::NewGuid().ToString('N')
    $original = 'harboralpha' + $run.Substring(0,8)
    $updated = 'harborbravo' + $run.Substring(0,8)
    $fixture = Request '/smoke/article' 'Post' @{ Name="Combined indexing probe $run"; Text=$original }
    function Both([string] $term, [int] $expected) {
        $encoded = [uri]::EscapeDataString($term)
        $examine = Request "/smoke/examine-query?query=$encoded"
        $relewise = Request "/smoke/relewise-query?query=$encoded"
        ($examine.total -eq $expected -and $relewise.hits -eq $expected)
    }
    Wait-Check { Both $original 1 } 'Publication reaches both Examine and Relewise'
    [void](Request "/smoke/edit/$($fixture.key)" 'Post' @{ Alias='bodyText'; Value=$updated; Publish=$false })
    Wait-Check { (Request "/smoke/examine-query?draft=true&query=$updated").total -eq 1 } 'The saved draft reaches the draft index'
    Wait-Check { (Both $original 1) -and (Both $updated 0) } 'Published indexes retain the published values during draft editing'
    $stored = Request "/smoke/content/$($fixture.key)"
    if ($stored.Contents[0].Data.bodyText.Value -ne $original) { throw 'Draft text leaked into the stored Relewise entity.' }
    [void](Request "/smoke/state/$($fixture.key)/publish" 'Post')
    Wait-Check { (Both $updated 1) -and (Both $original 0) } 'Publishing an edit replaces the indexed text in both destinations'
    [void](Request "/smoke/state/$($fixture.key)/unpublish" 'Post')
    Wait-Check { Both $updated 0 } 'Unpublication excludes the page from both destinations'
    Wait-Check {
        $value=(Request "/smoke/content/$($fixture.key)").Contents[0]
        $value.Disabled -and $value.Data.rwUmbracoPublished.Value -eq $false
    } 'Unpublication synchronizes the directly mapped entity'
    [void](Request "/smoke/state/$($fixture.key)/publish" 'Post')
    Wait-Check { Both $updated 1 } 'Republication restores both destinations'
    [void](Request '/smoke/rebuild' 'Post')
    Wait-Check { Both $updated 1 } 'A full Relewise export retains the current published state'
    [void](Request "/smoke/state/$($fixture.key)/delete" 'Post')
    Wait-Check { (Both $updated 0) -and @((Request "/smoke/content/$($fixture.key)").Contents).Count -eq 0 } 'Permanent deletion removes the directly mapped entity'
    $fixture = $null
    if ((Request '/smoke/persistence').integrationRecords -ne 0) { throw 'The integration wrote persistent records into Umbraco.' }
    $checks.Add([pscustomobject]@{ check='No integration-owned persisted records'; milliseconds=0 })
} catch {
    $failure = $_.Exception.Message
    throw
} finally {
    if ($fixture) {
        try { [void](Request "/smoke/state/$($fixture.key)/delete" 'Post') } catch { Write-Warning 'Fixture cleanup failed; inspect the local demo.' }
    }
    $requests = @(Request '/smoke/requests')
    [pscustomobject]@{
        passed=($null -eq $failure); failure=$failure; assertions=$checks.Count; checks=$checks
        elapsedMilliseconds=$started.ElapsedMilliseconds; runtime=(Request '/smoke/runtime')
        requestsByOperation=@($requests | Group-Object operation | Select-Object Name,Count)
    } | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $EvidenceFile
}
