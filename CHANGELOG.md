# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `ParallelFanout` (`JoakimAnder.Toolbox.Threading.FanOut`): fail-fast parallel execution of async operations that must all succeed, cancelling the remaining operations on the first fault. Fluent typed builder (`new FanOut().Add(...).Add(...).WhenAll(ct)`) plus static `WhenAll` overloads — void and typed, arity 2–8, and an `IEnumerable` void overload.
- DI registration source generator: `[Singleton]`/`[Scoped]`/`[Transient]` attributes that emit grouped `AddAttributed*Services` extension methods, with explicit-or-self service types and keyed-service support.
- `Result<TError>` / `Result<T, TError>` (`JoakimAnder.Toolbox.Results`): typed success/failure containers for explicit failure flow at boundaries. Caller-defined `TError` with implicit conversions from `T` / `TError`; `Match` and `TryGetValue` / `TryGetError` for honest consumer reads; `Map`, `MapError`, and `Bind` composition operators with sync and async variants (including extensions on `Task<Result<…>>`); `Result.Try` / `Result.TryAsync` for exception interop (rethrows `OperationCanceledException`); `ValueOrThrow` / `ThrowIfFailure` for the reverse direction. Composes with `FanOut` via `Result.TryAsync` (recommended) or per-op Results collapsed with `Bind`.

## [0.2.0] — 2026-05-23

### Added

- Repository scaffolding: solution layout, build configuration, documentation.
- Main library project (`JoakimAnder.Toolbox`) targeting .NET 10.
- Source generator project (`JoakimAnder.Toolbox.SourceGenerators`) bundled into the main NuGet at `analyzers/dotnet/cs/`.
- xUnit smoke tests for main library and source generators.
- Runnable examples project.
- MinVer-driven versioning, SourceLink, symbol package (.snupkg), AOT compatibility.
- GitHub Actions CI workflow (build + test on push to `main` and on pull requests).
- GitHub Actions release workflow (publish to NuGet.org on `v*` tag push, using OIDC trusted publishing).

### Changed

- `global.json` rollForward policy is `disable` (was `latestFeature`). SDK is pinned exactly for predictable builds across local and CI environments.
- `actions/setup-dotnet@v4` cache key is now `Directory.Packages.props` (Central Package Management's version source), so the cache invalidates only on real version changes.

[Unreleased]: https://github.com/JoakimAnder/Toolbox/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/JoakimAnder/Toolbox/releases/tag/v0.2.0
