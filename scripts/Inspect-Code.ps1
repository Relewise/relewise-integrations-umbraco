# Local developer check; do not run in CI/CD. Build the solution first. Reports stay outside the repository.
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$settingsHash = (Get-FileHash (Join-Path $repository '.editorconfig') -Algorithm SHA256).Hash
$cache = Join-Path ([IO.Path]::GetTempPath()) ("umbraco-inspections-cache-" + $settingsHash)
$report = Join-Path ([IO.Path]::GetTempPath()) ("umbraco-inspections-" + [Guid]::NewGuid() + ".sarif")

Push-Location $repository
try {
    dotnet tool run jb inspectcode Relewise.Integrations.Umbraco.slnx `
        --no-build --swea --severity=WARNING --no-updates --verbosity=WARN `
        '--exclude=**/obj/**;**/bin/**;**/node_modules/**;**/wwwroot/storefront/**;**/App_Plugins/**;**/umbraco/**' `
        "--output=$report" "--caches-home=$cache"
    if ($LASTEXITCODE -ne 0) {
        throw 'ReSharper inspection failed.'
    }

    $inspection = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $findings = @($inspection.runs | ForEach-Object { $_.results })
    if ($findings.Count -gt 0) {
        $findings | ForEach-Object { Write-Output "$($_.ruleId): $($_.message.text)" }
        throw "ReSharper found $($findings.Count) warnings/errors. Report: $report"
    }

    Write-Output 'ReSharper: no warnings or errors.'
}
finally {
    Pop-Location
}
