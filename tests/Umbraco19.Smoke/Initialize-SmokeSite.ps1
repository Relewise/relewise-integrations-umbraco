param(
    [string] $PackageVersion = '19.0.0-rc1',
    [string] $Directory = (Join-Path ([IO.Path]::GetTempPath()) ('umbraco19-smoke-' + [guid]::NewGuid().ToString('N'))),
    [string] $CredentialFile
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (Test-Path -LiteralPath $Directory) { throw 'Choose a new directory to preserve any existing site/database.' }
New-Item -ItemType Directory -Path $Directory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs'), (Join-Path $PSScriptRoot 'LiveSearchChecks.cs'), (Join-Path $PSScriptRoot 'Umbraco19.Smoke.csproj') -Destination $Directory
$project = Join-Path $Directory 'Umbraco19.Smoke.csproj'
$xml = [xml](Get-Content -LiteralPath $project -Raw)
($xml.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Relewise.Integrations.Umbraco').Version = $PackageVersion
$xml.Save($project)
$settings = @{
    Relewise = @{ DatasetId='00000000-0000-0000-0000-000000000001'; ApiKey='local-smoke-only'; ServerUrl='http://127.0.0.1:5419/relewise' }
    Smoke = @{ UseJson=$true }
    Umbraco = @{ CMS = @{
        Global = @{ Id=[guid]::NewGuid().ToString(); UseHttps=$false }
        ModelsBuilder = @{ ModelsMode='Nothing' }
        Unattended = @{ InstallUnattended=$true; UpgradeUnattended=$true; UnattendedUserName='Local smoke'; UnattendedUserEmail='smoke@example.invalid'; UnattendedUserPassword=('Smoke!' + [guid]::NewGuid().ToString('N') + 'Aa1') }
    } }
    ConnectionStrings = @{ umbracoDbDSN='Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True'; umbracoDbDSN_ProviderName='Microsoft.Data.Sqlite' }
}
if ($CredentialFile) {
    $credentials = Get-Content -LiteralPath $CredentialFile -Raw | ConvertFrom-Json
    $settings.Relewise = $credentials.Relewise
    $settings.Smoke.UseJson = $false
}
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Directory 'local.settings.json')
$feed = [Security.SecurityElement]::Escape((Join-Path $repo 'artifacts/umbraco19'))
"<configuration><packageSources><clear/><add key=`"local`" value=`"$feed`"/><add key=`"nuget`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>" | Set-Content -LiteralPath (Join-Path $Directory 'NuGet.config')
dotnet restore $project --configfile (Join-Path $Directory 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Smoke package restore failed.' }
dotnet build $project --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Smoke host build failed.' }
Write-Output "Site prepared at $Directory. Credentials remain in local.settings.json."
Write-Output "Run with ASPNETCORE_ENVIRONMENT=Development: dotnet run --project $project --no-build --no-launch-profile"
