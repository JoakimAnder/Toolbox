# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository scaffolding: solution layout, build configuration, documentation.
- Main library project (`JoakimAnder.Toolbox`) targeting .NET 10.
- Source generator project (`JoakimAnder.Toolbox.SourceGenerators`) bundled into the main NuGet at `analyzers/dotnet/cs/`.
- xUnit smoke tests for main library and source generators.
- Runnable examples project.
- MinVer-driven versioning, SourceLink, symbol package (.snupkg), AOT compatibility.
- GitHub Actions CI workflow (build + test on push to `main` and on pull requests).
- GitHub Actions release workflow (publish to NuGet.org on `v*` tag push, using OIDC trusted publishing).
- NuGet lockfiles enabled repo-wide for reproducible restores and stable CI caching.

### Changed

- `global.json` rollForward policy is `disable` (was `latestFeature`). SDK is pinned exactly for predictable builds across local and CI environments.
- Release workflow restore no longer uses `--locked-mode`. Lockfiles remain in the repo as the stable cache key for `actions/setup-dotnet@v4`, but the strict cross-environment hash check on release was incompatible with how the .NET SDK ships and signs its bundled packages (see the design spec).
