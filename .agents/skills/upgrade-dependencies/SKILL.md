---
name: upgrade-dependencies
description: Upgrade NuGet, npm, and Umbraco dependencies in relewise-integrations-umbraco with a safe git workflow and PR delivery flow. Use when asked to refresh dependencies, upgrade Umbraco, maintain package versions, or prepare dependency-upgrade PRs. Require a Trello card link, require a clean worktree on main before doing any upgrade work, preserve existing NuGet and npm version-range styles, validate with .NET and npm builds, then push a branch and create a PR whose description starts with the Trello card.
---

# Upgrade Dependencies

## Goal
Upgrade repository dependencies for `Relewise.Integrations.Umbraco.sln`, including:

- NuGet packages in `src/Integrations.Umbraco/Integrations.Umbraco.csproj`.
- NuGet packages in `samples/Umbraco/Umbraco.csproj`.
- npm packages in `src/Integrations.Umbraco/Client`.
- Umbraco packages across the package project, sample project, and client backoffice package.

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
- For exact versions in sample projects, update to exact versions unless the existing declaration is already a range.
- For `Relewise.Client` and `Relewise.Client.Extensions` in the package project, prefer the minimum needed supported version over the latest available version. Only raise the lower bound when the integration code requires newer client APIs or fixes, and document that reason in the PR body.

npm rules:

- Preserve the existing range prefix or comparator form for each package.
- Do not rewrite comparator ranges such as `>=1.1.6 <2.0.0` to caret, tilde, or exact versions.
- Do not rewrite wildcard/range declarations such as `^*`; keep them unchanged unless the user explicitly asks for a different policy.
- When updating pinned npm versions, keep them pinned. When updating caret versions, keep caret versions.

Record skipped or preserved ranged dependencies in both the PR body and final output.

## Umbraco Upgrade Rules
Upgrade Umbraco deliberately as one compatible set.

- Treat all `Umbraco.Cms*` NuGet packages as a coordinated group.
- In `src/Integrations.Umbraco/Integrations.Umbraco.csproj`, preserve the package project's compatibility range and do not raise the lower bound just because the sample or validation target is upgraded. For example, keep `[17.0.0,18)` as `[17.0.0,18)` unless package code needs a newer minimum; do not change it to `17.x.y`, `[17.x.y,18)`, or `[17.x.y,19)` without a concrete compatibility reason.
- In `samples/Umbraco/Umbraco.csproj`, keep exact Umbraco package versions exact and align all sample Umbraco packages to the same target version. The sample project is only a validation/sample host and must not drive the package project's minimum supported Umbraco version.
- Keep `@umbraco-cms/backoffice` in `src/Integrations.Umbraco/Client/package.json` in its existing declaration style. If it is `^*`, do not change it.
- After an Umbraco upgrade, inspect compile errors for breaking API changes and fix compatibility issues that are direct consequences of the upgrade.
- Keep package metadata aligned when relevant, including `PackageTags` and `umbraco-marketplace.json`, but do not change supported-major claims unless the dependency upgrade actually changes the supported Umbraco major.

If the user asks for a major Umbraco upgrade, confirm the target major and adjust ranges consistently before proceeding.

## NuGet Upgrade Workflow
1. Generate an outdated report:
```powershell
dotnet list Relewise.Integrations.Umbraco.sln package --outdated --format json > .\outdated-dotnet-packages.json
```

2. Inspect current package declarations before changing them. Use an XML-aware approach when possible, or carefully read the relevant `.csproj` files.

3. Upgrade eligible packages while preserving range style:

- For ranged NuGet references, update the minimum version inside the range and keep the upper bound.
- For exact NuGet references, use `dotnet add <project> package <id> --version <latestVersion>` or edit only the version attribute if `dotnet add` would rewrite surrounding XML undesirably.
- Skip packages when the latest available version would violate the current range's upper bound, unless the user explicitly requested a major upgrade.

4. Verify remaining outdated packages:
```powershell
dotnet list Relewise.Integrations.Umbraco.sln package --outdated
```

Explain any remaining outdated packages as intentional range-bound skips, exclusions, or blocked updates.

## npm Upgrade Workflow
Upgrade direct npm dependencies in `src/Integrations.Umbraco/Client`.

```powershell
Push-Location .\src\Integrations.Umbraco\Client
npm install
if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
```

Before installing each update, inspect the current value in `dependencies` and `devDependencies`:

- Skip comparator or wildcard declarations that must remain unchanged.
- Install caret dependencies as `<package>@^<latestVersion>` when the current declaration starts with `^` and is not `^*`.
- Install tilde dependencies as `<package>@~<latestVersion>` when the current declaration starts with `~`.
- Install pinned dependencies as `<package>@<latestVersion>` when the current declaration is pinned.

After package updates, regenerate and prove the lockfile from a clean install state:
```powershell
Remove-Item -LiteralPath .\node_modules -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\package-lock.json -Force -ErrorAction SilentlyContinue
npm install
if ($LASTEXITCODE -ne 0) { throw 'npm install failed while regenerating package-lock.json.' }
npm ci
if ($LASTEXITCODE -ne 0) { throw 'npm ci failed after regenerating package-lock.json.' }
Pop-Location
```

Record whether `package-lock.json` changed and whether it was regenerated from a clean install state.

## Resolve Upgrade Fallout
Fix compatibility issues directly caused by upgraded packages, including:

- Obsolete methods and APIs.
- Signature or behavior changes.
- Package-version conflicts and downgrade warnings.
- Compile-time errors in the package project, sample project, or generated client build.

Pause and request a collaborative plan if fixes become broad refactors, business-logic redesigns, or unclear functional decisions.

## Validation
Run all required validations:
```powershell
dotnet restore Relewise.Integrations.Umbraco.sln
dotnet build Relewise.Integrations.Umbraco.sln
Push-Location .\src\Integrations.Umbraco\Client
npm ci
npm run build
Pop-Location
```

If the repository has tests at the time of execution, run them as well. Treat any failed validation as blocking unless the user explicitly accepts the failure.

## Commit, Push, and Pull Request
Commit dependency and direct compatibility fixes on the upgrade branch.

Push:
```powershell
git push -u origin $branchName
```

Create a PR. Prefer GitHub CLI when available:
```powershell
$prBodyPath = ".\upgrade-dependencies-pr-body.md"
$prBodyTemplate = @'
__TRELLO_CARD_URL__

## Summary
- <short summary of dependency upgrades>
- <Umbraco upgrade summary, if applicable>
- <npm lockfile regenerated from a clean install state, if npm changed>
- <ranged dependencies preserved or skipped, if any>

## Validation
- `dotnet restore Relewise.Integrations.Umbraco.sln`: <result>
- `dotnet build Relewise.Integrations.Umbraco.sln`: <result>
- `npm ci` in `src/Integrations.Umbraco/Client`: <result>
- `npm run build` in `src/Integrations.Umbraco/Client`: <result>

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
- Updated npm packages.
- Umbraco target version and range-preservation decisions.
- Skipped or preserved dependencies and reasons.
- Compatibility fixes applied.
- Validation results.
- Whether the npm lockfile was regenerated from a clean install state.
- Pushed branch URL.
- Created GitHub PR URL, or manual PR fallback instructions if automated PR creation was unavailable.
