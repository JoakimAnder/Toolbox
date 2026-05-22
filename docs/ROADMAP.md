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

**Status:** in progress — being brainstormed now.
**Blocks:** everything else.

### 2. CI/CD
GitHub Actions workflows for build, test, and NuGet publishing.

- Build + test on push/PR
- Publish NuGet on release/tag

**Depends on:** Foundation.

### 3. Result class
A `Result<T>` / `Result<T, TError>` type for explicit success/failure flow.

**Depends on:** Foundation.

### 4. ParallelFanout class
Utility for fanning out work in parallel.

**Depends on:** Foundation.

### 5. DI Attributes + Source Generator
Attributes that mark services for DI registration, plus a source generator that emits the registration method(s).

- Largest unknown — source generator complexity
- Exercises the source-generators project layout decided in Foundation

**Depends on:** Foundation.

## Order

Foundation → CI/CD → features (Result / ParallelFanout / DI source generator) in any order.

Features could in principle be parallelized, but in practice will be done sequentially.
