---
name: upgrade-dependencies
description: Upgrade NuGet, npm, and Umbraco dependencies across the integration, dashboard, DemoStorefront and tests. Use when asked to refresh dependencies, upgrade Umbraco, maintain package versions, or prepare dependency-upgrade PRs. Preserve version-range policies, validate both frontends and local storefront behavior, and deliver through the Trello-linked branch and PR workflow.
---

# Upgrade Dependencies

## Goal
Upgrade repository dependencies for `Relewise.Integrations.Umbraco.slnx`, including:

- NuGet packages in `src/Integrations.Umbraco/Integrations.Umbraco.csproj`.
- NuGet packages in `samples/Relewise.DemoStorefront/Relewise.DemoStorefront.csproj`.
- Test dependencies in `src/Integrations.Umbraco.Tests/Integrations.Umbraco.Tests.csproj`.
- Dashboard npm dependencies and lockfile in `src/Integrations.Umbraco/Client`.
- Storefront npm dependencies and lockfile in `samples/Relewise.DemoStorefront/Client`.
- Umbraco packages across the integration, storefront, tests and dashboard backoffice package.
- Framework/compiler settings, Node/npm engines and workflow SDK versions when required by the upgrade.

Inspect the solution and dependency manifests at execution time so newly added projects
are not missed. Include test SDKs, runners and assertion/mocking libraries in the review;
do not restrict upgrades to production dependencies.

Deliver a pushed branch and GitHub PR linked to the required Trello card.

## Required Input
Require a Trello card URL for the dependency-upgrade task.

If the initial prompt does not include a Trello URL, ask for it before running upgrade work. Put the Trello URL as the first non-empty line of the PR body.

## Preflight Git Safety
Run all commands from the repository root.

1. Require a strict clean worktree before branch creation, pulling, or upgrade work:
```powershell
git status --porcelain
```
Abort if any output exists, including untracked files.

2. Require that the current branch is exactly `main`:
```powershell
$currentBranch = git branch --show-current
if ($currentBranch -ne 'main') { throw "Must be on main before upgrading dependencies. Current branch: $currentBranch" }
```
Do not switch branches automatically. Stop and tell the user to switch to `main` with a clean workspace.

3. Ensure `origin` exists:
```powershell
git remote get-url origin
```
Abort if this fails.

4. Fetch latest refs:
```powershell
git fetch origin --prune
```
Abort on failure.

5. Update local `main` with fast-forward only:
```powershell
git pull --ff-only origin main
```
Abort if this fails or is not fast-forward.

Do not continue when any preflight step fails.

## Branch Creation
Create a monthly branch with two-digit month format:

```powershell
$stamp = Get-Date -Format 'yyyyMM'
$branchName = "chore/upgrade-dependencies-$stamp"
```

Abort if `$branchName` already exists locally or remotely:
```powershell
git show-ref --verify --quiet "refs/heads/$branchName"
git ls-remote --exit-code --heads origin $branchName
```

Create and switch:
```powershell
git switch -c $branchName
```

## Version Range Preservation
Preserve existing dependency declaration styles.

NuGet rules:

- If a package uses NuGet range syntax with brackets or parentheses, keep it as a range.
- For lower-bound ranges such as `[17.0.0,18)`, keep the existing lower bound unless the package project genuinely requires a newer minimum version because of direct API usage, compatibility fixes, security policy, or another explicit reason. If a lower bound must be raised, keep the existing upper bound and bracket/parenthesis style.
- Do not convert ranged package references to pinned versions.
- For exact versions in sample and test projects, update to exact versions unless the existing declaration is already a range.
- For `Relewise.Client` and `Relewise.Client.Extensions` in the package project, prefer the minimum needed supported version over the latest available version. Only raise the lower bound when the integration code requires newer client APIs or fixes, and document that reason in the PR body.

npm rules:

- Preserve the existing range prefix or comparator form for each package.
- Do not rewrite comparator ranges such as `>=1.1.6 <2.0.0` to caret, tilde, or exact versions.
- Do not rewrite wildcard/range declarations such as `^*`; keep them unchanged unless the user explicitly asks for a different policy.
- When updating pinned npm versions, keep them pinned. When updating caret versions, keep caret versions.
- For the dashboard, treat the TypeScript compiler and frontend bundler as part of the Umbraco backoffice compatibility surface. Before upgrading its `typescript` or `vite`, inspect the frontend `package.json` at the exact target Umbraco release tag.
- Do not upgrade the dashboard's `typescript` or `vite` to a major version newer than the version used by the target Umbraco release. Use that release's major/baseline versions, preserving this repository's existing range prefixes.
- A successful local dashboard compile does not by itself justify moving ahead of Umbraco's toolchain. Only exceed Umbraco's TypeScript or Vite major when the user explicitly accepts that compatibility risk.
- DemoStorefront's frontend is independent of the backoffice toolchain. Upgrade Vue, `@vitejs/plugin-vue` and Vite as a compatible set using their peer/engine requirements. Do not force it onto the dashboard's Vite version.
- Regenerate the lockfile so `@umbraco-cms/backoffice` resolves to the same Umbraco version being validated, while preserving its existing declaration style in `package.json`.
- Check Node/npm requirements in both frontend manifests and their target toolchains, including `@umbraco-cms/backoffice`. Use a build environment satisfying both; align workflow versions and documented prerequisites when necessary. Report validation as incomplete if those requirements are not met.

Record skipped or preserved ranged dependencies in both the PR body and final output.

## Umbraco Upgrade Rules
Upgrade Umbraco deliberately as one compatible set.

- Treat all `Umbraco.Cms*` NuGet packages as a coordinated group. Align pinned versions in DemoStorefront and the test project to the same validation target; check the resolved dashboard backoffice version against that target.
- In `src/Integrations.Umbraco/Integrations.Umbraco.csproj`, preserve the package project's compatibility range and do not raise the lower bound just because the sample or validation target is upgraded. For example, keep `[17.0.0,18)` as `[17.0.0,18)` unless package code needs a newer minimum; do not change it to `17.x.y`, `[17.x.y,18)`, or `[17.x.y,19)` without a concrete compatibility reason.
- In `samples/Relewise.DemoStorefront/Relewise.DemoStorefront.csproj`, keep exact Umbraco package versions exact and align all sample Umbraco packages to the same target version. The sample project is only a validation/sample host and must not drive the package project's minimum supported Umbraco version.
- Keep `@umbraco-cms/backoffice` in `src/Integrations.Umbraco/Client/package.json` in its existing declaration style. If it is `^*`, do not change it.
- After an Umbraco upgrade, inspect compile errors for breaking API changes and fix compatibility issues that are direct consequences of the upgrade.
- Keep package metadata aligned when relevant, including `PackageTags` and `umbraco-marketplace.json`, but do not change supported-major claims unless the dependency upgrade actually changes the supported Umbraco major.
- When a new Umbraco major is released and the goal is to support it if compatible, update the sample project to the latest patch of that new major and run the full validation suite. If validation passes without package-code compatibility fixes, expand the package project's upper-bound range to include that major while preserving the existing lower bound; for example `[17.0.0,18)` can become `[17.0.0,19)` after validating Umbraco 18.
- When expanding supported Umbraco majors, update NuGet `PackageTags` with the new `Umbraco-vN` tag and update `umbraco-marketplace.json` tags or version metadata when present so marketplace support signals match the package dependency range.
- Record whether the new major was validated only through the sample project or also required package-code fixes. If fixes were required, summarize them and keep the widened range only when the fixed package builds successfully against the new major.

For a major Umbraco upgrade, use the target major explicitly requested by the user and adjust ranges consistently. Ask for the target only when it is unspecified or ambiguous.

## NuGet Upgrade Workflow
1. Generate an outdated report:
```powershell
New-Item -ItemType Directory -Path .\artifacts\upgrade-dependencies -Force | Out-Null
dotnet list Relewise.Integrations.Umbraco.slnx package --outdated --format json > .\artifacts\upgrade-dependencies\outdated-dotnet-packages.json
```

2. Inspect current package declarations before changing them. Use an XML-aware approach when possible, or carefully read the relevant `.csproj` files.

3. Upgrade eligible packages while preserving range style:

- For ranged NuGet references, preserve the minimum unless the code or an explicitly agreed compatibility/security requirement needs a newer version. Keep the upper bound unless a major-version change has been requested and validated.
- For exact NuGet references, use `dotnet add <project> package <id> --version <latestVersion>` or edit only the version attribute if `dotnet add` would rewrite surrounding XML undesirably.
- Skip packages when the latest available version would violate the current range's upper bound, unless the user explicitly requested a major upgrade.

4. Verify remaining outdated packages:
```powershell
dotnet list Relewise.Integrations.Umbraco.slnx package --outdated
```

Explain any remaining outdated packages as intentional range-bound skips, exclusions, or blocked updates.

## npm Upgrade Workflow
Apply this workflow separately to both clients:

- `src/Integrations.Umbraco/Client` (dashboard).
- `samples/Relewise.DemoStorefront/Client` (storefront).

Review `dependencies` and `devDependencies` in each manifest, including Vue and its
Vite plugin in the storefront. Inspect peer dependencies and engines before choosing
versions, and apply the distinct toolchain policies above.

- Preserve comparator/wildcard declarations that must remain unchanged.
- Install caret dependencies as `<package>@^<targetVersion>`, tilde dependencies as
  `<package>@~<targetVersion>` and pinned dependencies as `<package>@<targetVersion>`.
- Update each existing lockfile with `npm install`; do not delete lockfiles by default.
  Regenerate one from scratch only when necessary and explain the resulting changes.
- Run `npm ci` to prove a clean, reproducible install from each resulting lockfile,
  followed by an explicit production build. Do not rely on MSBuild's incremental
  targets, which can skip frontend compilation when generated output already exists.

For each client, check the command exit code and restore the working directory:

```powershell
$clientProjects = @(
    'src/Integrations.Umbraco/Client',
    'samples/Relewise.DemoStorefront/Client'
)
foreach ($clientProject in $clientProjects) {
    Push-Location $clientProject
    try {
        npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed: $clientProject" }
        npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed: $clientProject" }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm build failed: $clientProject" }
    }
    finally { Pop-Location }
}
```

Record dependency changes, intentional skips and lockfile changes separately for
the dashboard and storefront. Distinguish a clean `npm ci` install from deleting
and regenerating a lockfile.

## Resolve Upgrade Fallout
Fix compatibility issues directly caused by upgraded packages, including:

- Obsolete methods and APIs.
- Signature or behavior changes.
- Package-version conflicts and downgrade warnings.
- Compile-time errors in the integration, storefront, tests or either frontend build.
- Changes to Razor rendering, Vue interactions, converters, seeding or SDK behavior caused by the upgraded dependencies.

Pause and request a collaborative plan if fixes become broad refactors, business-logic redesigns, or unclear functional decisions.

## Validation
Complete both client installs/builds above, then restore, build and test the whole
solution. Run Debug and Release sequentially; Umbraco build targets can write shared
schema files even when output directories differ.

```powershell
dotnet restore Relewise.Integrations.Umbraco.slnx
if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }
dotnet format Relewise.Integrations.Umbraco.slnx --verify-no-changes --diagnostics IDE0005 IDE0065 IDE0130 --severity info --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Namespace/using checks failed.' }
foreach ($configuration in @('Debug', 'Release')) {
    dotnet build Relewise.Integrations.Umbraco.slnx --configuration $configuration --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed: $configuration" }
    dotnet test Relewise.Integrations.Umbraco.slnx --configuration $configuration --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Solution tests failed: $configuration" }
}
```

ReSharper checks belong in the developer's IDE; do not install JetBrains CLI tooling
or add ReSharper steps to CI/CD.

### Local DemoStorefront smoke test
Read [the storefront setup and behavior guide](../../../samples/Relewise.DemoStorefront/README.md)
before running the host. Use local Relewise API/Datasets only, with credentials in
ignored `local.settings.json` or environment variables. Never substitute a production
Dataset or Examine when Relewise is unavailable. Missing local API access blocks
completion of the connected smoke test; report that gap rather than claiming success.

Preserve existing developer databases/settings. Use an isolated local copy or a
fresh temporary SQLite database for clean-install testing; stop the sample before
switching database or connection state. Run sample hosts sequentially and keep their
application URL/seeded domains aligned with the listening address. Do not stop a
shared local API or reset someone else's Dataset; use a dedicated disposable local
Dataset for ingestion, tracking and lifecycle tests.

Verify these observable behaviors in desktop and mobile layouts:

1. **Clean installation without credentials:** startup installs/seeds Umbraco; bundled
   images, English/Danish content, nested blocks and market-specific prices render.
   The preview notice is visible; search, Offers, recommendations and shopping remain
   unavailable rather than falling back to another provider.
2. **Connected ingestion and indexing:** a fresh local Dataset receives the seeded
   entities, built-in/custom fields, converter output and canonical reference IDs.
   Check the Relewise dashboard and default/named provider configuration. Edit/publish,
   unpublish and delete disposable content, then rebuild; verify the expected lifecycle
   actions without duplicate export paths and preserve legacy AutoMap output.
3. **Search and markets:** exercise predictions/keyboard selection, Content and Product
   search, Category/Brand landing-page scope, facets, price/sale filters, sorting and
   pagination. Switch markets and check language, currency, prices and sale flags.
4. **Recommendations:** check Similar Products, Recently Viewed and Popular
   Products/Categories after genuine local activity. Also check the fresh Dataset with
   no activity: empty sections and empty-cart fallbacks must behave correctly; do not
   fabricate popularity or history to make the UI appear populated.
5. **Cart, checkout and tracking:** add Product/Variant items, change quantities, remove
   items and complete a simulated order. Check empty-cart presentation, retry handling
   and shared user/canonical entity IDs across export, views, searches, recommendations,
   Cart and Order events. Confirm Variant IDs where applicable.
6. **Connection failure:** use an invalid local credential to check the connection
   notice and relevant HTML/JSON error responses. Optional recommendation/prediction
   failures must leave the rest of the page usable. Restore the original configuration
   afterward and verify the configured storefront works again.

Use the browser and local API/Dataset inspection to verify behavior, not just HTTP 200
responses. Keep raw reports, credentials and generated databases ignored; capture a few
useful sanitized screenshots when they help review UI changes. Follow repository
instructions for evidence placement. Record which scenarios ran, results and any
limitations. A build alone is not a storefront smoke test.

Treat failed validation or missing required smoke coverage as blocking unless the
user explicitly accepts the limitation.

## Commit, Push, and Pull Request
Commit dependency and direct compatibility fixes on the upgrade branch.

Push:
```powershell
git push -u origin $branchName
```

Create a PR. Prefer GitHub CLI when available:
```powershell
$prBodyPath = Join-Path $env:TEMP "umbraco-upgrade-dependencies-pr-body.md"
$prBodyTemplate = @'
__TRELLO_CARD_URL__

## Summary
- <short summary of dependency upgrades>
- <Umbraco upgrade summary, if applicable>
- <dashboard and storefront dependency/lockfile changes, if npm changed>
- <ranged dependencies preserved or skipped, if any>

## Validation
- `dotnet restore Relewise.Integrations.Umbraco.slnx`: <result>
- Solution builds/tests in Debug and Release; namespace/using checks: <results>
- Dashboard `npm ci` and `npm run build`: <results>
- Storefront `npm ci` and `npm run build`: <results>
- Local DemoStorefront smoke test, including fresh installation/Dataset and desktop/mobile: <results and gaps>

## Notes
- <known issues, intentional skips, or major-version constraints>
'@
$prBody = $prBodyTemplate -replace '__TRELLO_CARD_URL__', $TrelloCardUrl
Set-Content -Path $prBodyPath -Value $prBody -Encoding utf8

$prUrl = gh pr create --base main --head $branchName --title "chore: upgrade dependencies ($stamp)" --body-file $prBodyPath
if ($LASTEXITCODE -ne 0) { throw 'gh pr create failed.' }
Write-Host "PR URL: $prUrl"
```

Manual fallback:
```powershell
git push -u origin $branchName
Write-Host "Create PR: https://github.com/Relewise/relewise-integrations-umbraco/compare/main...$branchName?expand=1"
Write-Host "PR title: chore: upgrade dependencies ($stamp)"
Write-Host "PR body file: $prBodyPath"
```

## Output Expectations
Provide a concise final summary with:

- Trello link used.
- Created branch name.
- Updated NuGet packages grouped by project.
- Updated npm packages grouped by dashboard and storefront.
- Umbraco target version and range-preservation decisions.
- Skipped or preserved dependencies and reasons.
- Compatibility fixes applied.
- Debug/Release build and test results, test counts and formatting checks.
- Both frontend clean-install/build results and Node/npm versions used.
- Local smoke scenarios, including fresh installation, preview, connected behavior, failure states and desktop/mobile coverage; disclose gaps and link sanitized evidence when available.
- Lockfile changes for each frontend, distinguishing clean installs from full lockfile regeneration.
- Pushed branch URL.
- Created GitHub PR URL, or manual PR fallback instructions if automated PR creation was unavailable.
