param(
    [string] $BaseUri = 'http://localhost:5521',
    [Parameter(Mandatory)] [string] $EvidenceFile,
    [switch] $ExternalCatalogue
)
$ErrorActionPreference = 'Stop'
if (!([uri]$BaseUri).IsLoopback) { throw 'These tests require the local demo.' }
$checks = [Collections.Generic.List[string]]::new()
function Request([string] $path, [string] $method = 'Get', $body = $null) {
    $arguments = @{ Uri=$BaseUri + $path; Method=$method; TimeoutSec=60 }
    if ($null -ne $body) { $arguments.Body=$body | ConvertTo-Json -Depth 8; $arguments.ContentType='application/json' }
    Invoke-RestMethod @arguments | Write-Output
}
function Wait-Check([scriptblock] $condition, [string] $description, [int] $TimeoutSeconds = 180) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $lastError = ''
    do {
        try { if (& $condition) { $checks.Add($description); Write-Output "Passed: $description"; return } }
        catch { $lastError=$_.Exception.Message }
        Start-Sleep -Seconds 1
    } while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    throw "Timed out: $description. $lastError"
}
function Product([string] $id) { (Request "/smoke/product/$id").Products[0] }
$probe=$null; $variant=$null; $restoreCulture=$false; $restoreBrand=$false; $failure=$null
try {
    $nodes=@(Request '/smoke/identities')
    $pack=$nodes | Where-Object canonicalId -EQ 'U19-SMOKE-PACK'
    $brand=$nodes | Where-Object type -EQ 'brand'
    $localized=$nodes | Where-Object type -EQ 'localizedArticle'
    if (!$ExternalCatalogue) {
        $variant=Request "/smoke/variant/$($pack.key)" 'Post'
        Wait-Check { @((Product 'U19-SMOKE-PACK').AllVariants.VariantId) -contains $variant.id } 'A published child Variant is exported within its Product'
        [void](Request "/smoke/state/$($variant.key)/unpublish" 'Post')
        Wait-Check { @((Product 'U19-SMOKE-PACK').AllVariants.VariantId) -notcontains $variant.id } 'Unpublishing a Variant refreshes its parent Product'
        [void](Request "/smoke/state/$($variant.key)/publish" 'Post')
        Wait-Check { @((Product 'U19-SMOKE-PACK').AllVariants.VariantId) -contains $variant.id } 'Republishing a Variant restores its canonical ID'
        [void](Request "/smoke/state/$($variant.key)/delete" 'Post')
        Wait-Check { @((Product 'U19-SMOKE-PACK').AllVariants.VariantId) -notcontains $variant.id } 'Deleting a Variant refreshes its parent after commit'
        $variant=$null
    }
    $restoreCulture=$true
    [void](Request "/smoke/state/$($localized.key)/unpublish?culture=da-DK" 'Post')
    Wait-Check { (Request '/smoke/query?provider=localized&culture=da-DK').total -eq 0 -and (Request '/smoke/query?provider=localized&culture=en-US').total -eq 1 } 'Partial unpublication removes Danish membership while retaining English'
    [void](Request "/smoke/state/$($localized.key)/publish?culture=da-DK" 'Post')
    Wait-Check { (Request '/smoke/query?provider=localized&culture=da-DK').total -eq 1 } 'Publishing Danish again restores culture membership'
    $restoreCulture=$false
    $restoreBrand=$true
    [void](Request "/smoke/edit/$($pack.key)" 'Post' @{ Alias='brand'; Value=$null })
    [void](Request '/smoke/rebuild' 'Post')
    Wait-Check { (Product 'U19-SMOKE-PACK').Brand.BrandId -ieq $brand.canonicalId } 'An empty Brand picker retains the existing partial-update relationship'
    [void](Request "/smoke/edit/$($pack.key)" 'Post' @{ Alias='brand'; Value=('umb://document/' + ([guid]$brand.key).ToString('N')) })
    $restoreBrand=$false
    $probe=Request '/smoke/probe' 'Post'
    Wait-Check { $null -ne (Product $probe.id) } 'A new mapped Product is ingested'
    if ($ExternalCatalogue) {
        [void](Request "/smoke/admin/$($probe.id)/external" 'Post')
        [void](Request "/smoke/state/$($probe.key)/unpublish" 'Post')
        Wait-Check {
            $stored=Product $probe.id
            !$stored.Disabled -and $stored.Data.rwUmbracoPublished.Value -eq $false -and $stored.Data.externalField.Value -eq 'preserve-me' -and @($stored.AllVariants.VariantId) -contains 'external-variant' -and @($stored.Assortments).Count -eq 2 -and $stored.CategoryPaths[0].BreadcrumbPathStartingFromRoot[0].Id -eq 'U19-SMOKE-EQUIPMENT'
        } 'None policy preserves external fields, Variants and enabled state on unpublication'
        Wait-Check {
            @((Request '/smoke/query?provider=catalogue').documents) -notcontains $probe.key -and @((Request '/smoke/query?provider=commerceCatalogue').documents) -contains $probe.key
        } 'RequirePublishedPage independently selects unavailable CMS pages' 600
        [void](Request "/smoke/admin/$($probe.id)/disable" 'Post')
        [void](Request "/smoke/state/$($probe.key)/publish" 'Post')
        Wait-Check { $stored=Product $probe.id; $stored.Disabled -and $stored.Data.rwUmbracoPublished.Value -eq $true } 'None policy does not enable an externally disabled Product'
        [void](Request "/smoke/admin/$($probe.id)/enable" 'Post')
        [void](Request "/smoke/state/$($probe.key)/delete" 'Post')
        Wait-Check {
            $stored=Product $probe.id
            $null -ne $stored -and !$stored.Disabled -and $stored.Data.rwUmbracoPublished.Value -eq $false -and $stored.Data.externalField.Value -eq 'preserve-me'
        } 'None policy retains the commerce entity after deletion of its CMS page'
    } else {
        $old=$probe.id; $new=$old + '-CHANGED'
        [void](Request "/smoke/id/$($probe.key)/$new" 'Post')
        Wait-Check { $null -ne (Product $new) } 'Changing the configured property ID exports its new canonical identity'
        [void](Request '/smoke/rebuild' 'Post')
        Wait-Check { (Product $old).Disabled -and !(Product $new).Disabled } 'A completed full rebuild reconciles the former identity'
        [void](Request "/smoke/state/$($probe.key)/delete" 'Post')
        Wait-Check { $null -eq (Product $new) } 'Deletion targets the current property-based Product ID'
    }
    $probe=$null
    if ((Request '/smoke/persistence').integrationRecords -ne 0) { throw 'Integration-owned records were persisted.' }
} catch { $failure=$_.Exception.Message; throw }
finally {
    if ($restoreCulture) { [void](Request "/smoke/state/$($localized.key)/publish?culture=da-DK" 'Post') }
    if ($restoreBrand) { [void](Request "/smoke/edit/$($pack.key)" 'Post' @{ Alias='brand'; Value=('umb://document/' + ([guid]$brand.key).ToString('N')) }) }
    foreach ($fixture in @($variant,$probe) | Where-Object { $_ }) {
        try { [void](Request "/smoke/state/$($fixture.key)/delete" 'Post') } catch { Write-Warning 'Inspect the local fixture cleanup.' }
    }
    [pscustomobject]@{ passed=($null -eq $failure); failure=$failure; assertions=$checks.Count; checks=$checks; runtime=(Request '/smoke/runtime') } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $EvidenceFile
}
