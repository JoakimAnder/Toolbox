# CI/CD — Design Spec

**Date:** 2026-05-23
**Sub-project:** CI/CD (see [docs/ROADMAP.md](../../ROADMAP.md))
**Status:** Approved, ready for implementation planning.

## Goal

Set up GitHub Actions workflows that build and test the Toolbox repo on every change, and publish the `JoakimAnder.Toolbox` NuGet package to nuget.org when a `v*` tag is pushed.

The CI/CD sub-project produces the first real release (`v0.1.0`) as the proof the whole pipeline works end-to-end. Subsequent feature sub-projects (Result, ParallelFanout, DI source generator) inherit the pipeline unchanged.

## Scope

In scope:
- `.github/workflows/ci.yml` — build + test on push to `main` and on all pull requests.
- `.github/workflows/release.yml` — build + test + pack + publish to NuGet.org on `v*` tag push.
- Opt-in to NuGet lockfiles repo-wide so restores are reproducible and CI caching is meaningful.
- One-time manual setup: register a trusted publisher on nuget.org; create an empty `nuget-release` GitHub Deployment Environment.
- Add a CI status badge to `README.md`.
- Update `CHANGELOG.md` `[Unreleased] → Added` to record the wiring.

Out of scope (deferred to a future sub-project):
- CodeQL / security scanning.
- Dependabot / Renovate.
- Multi-OS matrix (Ubuntu/Windows/macOS).
- Pre-release publishing for non-tag commits.
- Automated GitHub Release creation with changelog excerpt.
- `dotnet format --verify-no-changes` lint gate.
- Code-coverage upload.
- Required-reviewer protection on the `nuget-release` environment.
- Branch protection rules on `main`.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Workflow layout | Two files: `ci.yml` + `release.yml` | Clean separation — release can't fire on a regular push; CI stays simple. |
| Publish trigger | Git tag push matching `v*` | Single source of truth: MinVer reads the tag and the workflow publishes. Fits the foundation's MinVer wiring. |
| NuGet.org auth | Trusted publishing via OIDC | No long-lived API key; no secret rotation; uses `NuGet/login@v1` to exchange a GitHub OIDC token for a short-lived NuGet token. |
| CI triggers | Push to `main` + all pull requests | Solo dev who mostly works via PRs but sometimes commits straight to `main`. Feature-branch pushes don't burn Actions minutes on their own. |
| Runner | `ubuntu-latest` only | Class library — no platform-specific code; cheapest, fastest runner. |
| Lockfiles | Enabled repo-wide via `RestorePackagesWithLockFile=true` | Stable cache key for `actions/setup-dotnet@v4`. (`--locked-mode` on release was attempted but abandoned — see [SDK pinning](#decisions) and the [Lockfiles and --locked-mode](#lockfiles-and---locked-mode) note below.) |
| Caching | `actions/setup-dotnet@v4` built-in cache, keyed on `**/packages.lock.json` | Modern, minimal config; lockfiles give a stable cache key. |
| Concurrency (CI) | Group by ref; `cancel-in-progress` only on pull requests | A new PR push cancels prior runs; main-branch pushes never cancel each other. |
| Concurrency (release) | None | A partial publish is worse than wasted minutes. |
| Permissions | `contents: read` everywhere; release adds `id-token: write` | Least privilege. OIDC exchange requires `id-token: write`. |
| Deployment environment | `nuget-release` (empty, no required reviewers) | Surfaces releases in the repo's deployments timeline; future hatch for adding protection rules without touching the workflow. |
| Test logger | Default console logger | The `GitHubActions` logger (Tyrrrz/GitHubActionsTestLogger) would give inline PR annotations but requires a package reference; deferred since smoke tests need only pass/fail counts. |
| SDK pinning | `global.json` rollForward: `disable` | Predictable builds across local + CI; setup-dotnet@v4 installs the exact pinned SDK. SDK upgrades require an explicit `global.json` (+ lockfile if drift occurs) commit. (Overrides the foundation spec's `latestFeature` choice.) |
| First version | `v0.2.0` | First real tag once the pipeline is verified end-to-end. (`v0.1.0` was an unpublished foundation milestone tag.) |

## File layout

```
Toolbox/
├── .github/
│   └── workflows/
│       ├── ci.yml          (new)
│       └── release.yml     (new)
├── Directory.Build.props   (one-line addition: RestorePackagesWithLockFile)
├── README.md               (add CI badge)
├── CHANGELOG.md            (record wiring)
└── (each project)/packages.lock.json   (new — five files, committed)
```

## Workflow: `ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

permissions:
  contents: read

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build
```

Notes:
- `fetch-depth: 0` is required by MinVer to compute the version from git history.
- CI does **not** use `--locked-mode` on restore — lockfile drift surfaces as a normal warning during PR review rather than a hard failure.
- The test step uses the default console logger. The `GitHubActions` logger (Tyrrrz/GitHubActionsTestLogger) would give inline PR annotations on failed tests but requires adding a package; deferred until there are tests that benefit from per-test reporting.

## Workflow: `release.yml`

```yaml
name: Release

on:
  push:
    tags: ['v*']

permissions:
  contents: read
  id-token: write

jobs:
  publish:
    runs-on: ubuntu-latest
    environment: nuget-release
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build

      - run: dotnet pack -c Release --no-build -o ./artifacts src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj

      - uses: NuGet/login@v1
        id: nuget-login
        with:
          api-key-source-url: https://api.nuget.org/v3/index.json

      - run: >
          dotnet nuget push ./artifacts/*.nupkg
          --api-key "${{ steps.nuget-login.outputs.NUGET_API_KEY }}"
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate
```

Notes:
- `id-token: write` is mandatory for the OIDC exchange.
- The release restore does NOT use `--locked-mode`; see [Lockfiles and --locked-mode](#lockfiles-and---locked-mode) below.
- Tests are re-run before publish in case a tag is pushed at an older commit than the latest green CI run.
- `--no-build` on `pack` reuses the build artifacts and avoids re-running source generators.
- The `*.nupkg` glob matches only the main package file. `dotnet nuget push` automatically uploads any sibling `.snupkg` symbol package alongside it, so the symbols ship on the same push without an extra command.
- `--skip-duplicate` makes the push idempotent if the workflow is re-run for the same tag.

## Lockfile opt-in

Add one property to the existing root `Directory.Build.props`:

```xml
<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
```

Final root `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisMode>Recommended</AnalysisMode>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

Then `dotnet restore` once locally to generate the per-project lockfiles. Five files get committed:

```
src/JoakimAnder.Toolbox/packages.lock.json
src/JoakimAnder.Toolbox.SourceGenerators/packages.lock.json
tests/JoakimAnder.Toolbox.Tests/packages.lock.json
tests/JoakimAnder.Toolbox.SourceGenerators.Tests/packages.lock.json
examples/JoakimAnder.Toolbox.Examples/packages.lock.json
```

The repo's current `.gitignore` does not exclude `packages.lock.json`, so no `.gitignore` change is needed — just verify after generation.

### Lockfiles and `--locked-mode`

The original design used `dotnet restore --locked-mode` on the release workflow to guarantee the release built against the exact dependency graph CI tested. End-to-end testing revealed two distinct failures that locked-mode cannot tolerate:

1. **Feature-band drift** (`NU1004`). `Microsoft.NET.ILLink.Tasks` (an SDK-bundled trim/AOT tool) version is determined by the active .NET SDK feature band. With `global.json` originally set to `rollForward: latestFeature`, the runner picked a higher feature-band SDK than the local machine that generated the lockfiles. Fixed by pinning to `rollForward: disable` (recorded above).
2. **Cross-source hash drift** (`NU1403`). Even with the same package version, the SHA-512 hash recorded in the lockfile differed between the local machine (which restored from the SDK's implicit NuGet fallback folder) and the runner (which restored from nuget.org). Microsoft ships bit-different builds through these channels for the same package ID/version.

After working around both, it became clear that locked-mode in .NET 10's SDK + lockfile ecosystem is a leaky abstraction for SDK-bundled packages and would require ongoing maintenance every time the SDK or runner image shifted. The decision was to **drop `--locked-mode` from the release workflow** and accept the weaker guarantee that release runs in the same runner image as CI minutes apart. Lockfiles remain in the repo because they still serve as a stable cache key for `actions/setup-dotnet@v4` — their original primary purpose in this design.

## README CI badge

Add a CI badge to `README.md`, between the existing NuGet and License badges:

```markdown
[![NuGet](https://img.shields.io/nuget/v/JoakimAnder.Toolbox.svg)](https://www.nuget.org/packages/JoakimAnder.Toolbox/)
[![CI](https://github.com/JoakimAnder/Toolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/JoakimAnder/Toolbox/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
```

## CHANGELOG entry

Append to `[Unreleased] → Added` in `CHANGELOG.md`:

```markdown
- GitHub Actions CI workflow (build + test on push to `main` and on pull requests).
- GitHub Actions release workflow (publish to NuGet.org on `v*` tag push, using trusted publishing).
- NuGet lockfiles enabled repo-wide for reproducible restores and stable CI caching.
```

## One-time manual setup

These steps are performed by hand once, outside the workflow files. They are **prerequisites** for the release workflow to succeed but are **not** part of the implementation plan's automated work.

1. **Create the `nuget-release` GitHub Deployment Environment.**
   In repo settings → Environments → New environment → name: `nuget-release`. No required reviewers. No deployment branches restriction. Save.

2. **Register a trusted publisher on nuget.org.**
   On https://www.nuget.org/, account settings → Trusted Publishers → Add new policy:
   - **Policy type:** GitHub Actions
   - **Package owner:** the nuget.org account that owns `JoakimAnder.Toolbox`
   - **Package pattern:** `JoakimAnder.Toolbox` (matches the main package; symbols package inherits)
   - **Repository owner:** `JoakimAnder`
   - **Repository:** `Toolbox`
   - **Workflow filename:** `release.yml`
   - **Environment:** `nuget-release`

   Since the package does not yet exist on nuget.org at the time of first publish, the trusted-publisher policy must be created against a **package pattern** rather than an existing package. NuGet.org supports this — the policy applies to any future package matching the pattern owned by the configured account.

## Completion criteria

The CI/CD sub-project is complete when **all** of the following are true:

1. `.github/workflows/ci.yml` and `.github/workflows/release.yml` exist and are valid YAML (GitHub does not reject them on push).
2. `RestorePackagesWithLockFile` is set in the root `Directory.Build.props`; five `packages.lock.json` files are committed under the corresponding projects.
3. A pull request opened against `main` triggers `ci.yml`; the workflow goes green; the run shows restore, build (zero errors / zero warnings), tests passing.
4. A push to `main` triggers `ci.yml` and goes green.
5. The trusted publisher is registered on nuget.org with the parameters in [One-time manual setup](#one-time-manual-setup).
6. The `nuget-release` GitHub Deployment Environment exists in repo settings (empty — no required reviewers).
7. Tagging a commit `v0.1.0` and pushing the tag triggers `release.yml`, which:
   - re-runs build and tests,
   - packs and pushes `JoakimAnder.Toolbox.0.1.0.nupkg` + `JoakimAnder.Toolbox.0.1.0.snupkg` to nuget.org,
   - the package becomes visible on `https://www.nuget.org/packages/JoakimAnder.Toolbox/0.1.0`.
8. The README CI badge renders green when viewed on `main`.
9. `CHANGELOG.md` has the new `[Unreleased] → Added` entries.

## Non-goals

See [Out of scope](#scope). Each item there is deferred deliberately; none is forgotten. They can be picked up as a follow-up "CI hygiene" sub-project once the minimum pipeline has been in use long enough to know which additions are actually pulling weight.

## Open questions

None at design time. The implementation plan resolves the following at execution time:
- Latest stable major-version tags for `actions/checkout`, `actions/setup-dotnet`, and `NuGet/login` (the design pins `@v4`, `@v4`, `@v1` respectively; the implementer may bump to the latest stable at execution time).
- Confirmation that `dotnet restore` at the foundation's current dependency graph produces lockfiles that restore cleanly under `--locked-mode`.
