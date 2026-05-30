# Toolbox Roadmap

High-level decomposition of work for the Toolbox project. Each sub-project below gets its own design spec under `docs/superpowers/specs/` and is implemented independently.

## Sub-projects

### 1. Foundation (scaffolding)
Solution structure, .NET class lib setup, root-level documentation files.

- Solution layout: src / tests / examples / source-generators
- .NET class library project(s)
- `README.md` (proper version)
- `CHANGELOG.md`
- AI-usage disclosure file

**Status:** Complete. Spec: [2026-05-22-foundation-design.md](superpowers/specs/2026-05-22-foundation-design.md).
**Blocks:** everything else.

### 2. CI/CD
GitHub Actions workflows for build, test, and NuGet publishing.

- Build + test on push/PR
- Publish NuGet on release/tag

**Status:** Complete. Spec: [2026-05-23-cicd-design.md](superpowers/specs/2026-05-23-cicd-design.md).
**Depends on:** Foundation.

### 3. Result class
A `Result<T>` / `Result<T, TError>` type for explicit success/failure flow.

**Status:** Not started.
**Depends on:** Foundation.

### 4. ParallelFanout class
Utility for fanning out work in parallel. Fail-fast `FanOut` — runs async operations that must all succeed and cancels the rest on the first fault.

**Status:** Complete. Spec: [2026-05-26-parallelfanout-design.md](superpowers/specs/2026-05-26-parallelfanout-design.md), plan: [2026-05-26-parallelfanout.md](superpowers/plans/2026-05-26-parallelfanout.md).
**Depends on:** Foundation.

### 5. DI Attributes + Source Generator
Attributes that mark services for DI registration, plus a source generator that emits the registration method(s).

- Largest unknown — source generator complexity
- Exercises the source-generators project layout decided in Foundation

**Status:** Complete. Spec: [2026-05-27-di-source-generator-design.md](superpowers/specs/2026-05-27-di-source-generator-design.md), plan: [2026-05-27-di-source-generator.md](superpowers/plans/2026-05-27-di-source-generator.md).
**Depends on:** Foundation.

## Order

Foundation → CI/CD → features (Result / ParallelFanout / DI source generator) in any order.

Features could in principle be parallelized, but in practice will be done sequentially.

**Done so far:** Foundation, CI/CD, ParallelFanout, DI source generator. **Remaining:** Result.
