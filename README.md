# JoakimAnder.Toolbox

A collection of helper classes and utilities I've grown tired of copy-pasting between projects.

[![NuGet](https://img.shields.io/nuget/v/JoakimAnder.Toolbox.svg)](https://www.nuget.org/packages/JoakimAnder.Toolbox/)
[![CI](https://github.com/JoakimAnder/Toolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/JoakimAnder/Toolbox/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Installation

```sh
dotnet add package JoakimAnder.Toolbox
```

Requires .NET 10 or later.

## What's in the box

### ParallelFanout — `JoakimAnder.Toolbox.Threading.FanOut`

Run several async operations that must *all* succeed, and cancel the rest the instant one
fails — so a long-running sibling isn't left burning time and resources after the outcome
is already decided.

```csharp
using JoakimAnder.Toolbox.Threading;

var (user, orders) = await new FanOut()
    .Add(ct => GetUserAsync(ct))
    .Add(ct => GetOrdersAsync(ct))
    .WhenAll(cancellationToken);
```

If `GetUserAsync` throws, the token handed to `GetOrdersAsync` is cancelled and the original
exception is rethrown unwrapped. Operations are factories (`ct => …`) so the token can be
threaded into each one. A static `FanOut.WhenAll(op1, op2, …)` is also available for quick one-liners — both typed
(returning a tuple) and `void`, for arities 2–8, plus an
`IEnumerable<Func<CancellationToken, Task>>` overload for dynamic counts.

See [docs/ROADMAP.md](docs/ROADMAP.md) for the rest of the planned features (Result, DI source generator).

## Project structure

- `src/JoakimAnder.Toolbox` — the library
- `src/JoakimAnder.Toolbox.SourceGenerators` — source generators, packed into the main NuGet
- `tests/` — xUnit test projects
- `examples/` — runnable usage examples

## Building locally

```sh
dotnet build
dotnet test
```

## Versioning

Versions are derived from git tags via [MinVer](https://github.com/adamralph/minver).
Tag a commit `v1.2.3` to release `1.2.3` to NuGet.

## License

MIT — see [LICENSE](LICENSE).

## AI usage

See [AI_DISCLOSURE.md](AI_DISCLOSURE.md).
