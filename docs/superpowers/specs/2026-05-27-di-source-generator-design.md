# DI Attributes + Source Generator — Design Spec

**Date:** 2026-05-27
**Sub-project:** DI Attributes + Source Generator (#5, see [docs/ROADMAP.md](../../ROADMAP.md))
**Status:** Approved, ready for implementation planning.

## Goal

Let a consumer mark a class for dependency-injection registration with an attribute, and have a Roslyn source generator emit the `IServiceCollection` extension method that performs the registration. No hand-written `services.AddX<…>()` lists, no runtime reflection or assembly scanning — the registrations are generated at build time from the attributes and are fully AOT/trim-clean.

```csharp
[Singleton(typeof(IClock))] class Clock : IClock { }
[Scoped]                    class Cache { }

// elsewhere:
builder.Services.AddAttributedServices();   // generated
```

## The gap

Registering services by hand is repetitive and drifts out of sync with the code: you add a class, forget the `AddScoped<…>` line, and discover it at runtime. The common alternatives each have a cost:

- **Reflection scanning** (Scrutor-style) works but runs at startup, defeats trimming/AOT, and hides registration in convention.
- **Hand-written registration** is explicit but boilerplate-heavy and easy to forget.

A source generator gives the explicitness of hand-written registration with the no-forgetting property of scanning, at zero runtime cost. This sub-project is also the roadmap's deliberate exercise of the source-generator project layout decided in Foundation — the [ParallelFanout spec](2026-05-26-parallelfanout-design.md) explicitly reserved Roslyn generators for here, "where they legitimately emit code into consumer compilations based on consumer attributes."

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Attribute API shape | Per-lifetime attributes: `[Singleton]`, `[Scoped]`, `[Transient]` | Reads naturally, lifetime unmissable at the call site, no enum import |
| Attribute hosting | **Injected** `internal sealed` into each consumer via `RegisterPostInitializationOutput` | Keeps the runtime library free of these types; trim/AOT-lean; you mark your own assembly's classes |
| Service-type resolution | **Explicit or self**: positional `Type? serviceType`; omit ⇒ register as the concrete type | No inference magic — fully predictable; simplifies the generator (no interface analysis / disambiguation) |
| Registration grouping | Optional `Group` ⇒ one method per group; default ⇒ `AddAttributedServices` | User-chosen `AddAttributed{Group}Services` convention; lets a consumer split registration into named buckets |
| Keyed services | Optional `Key` ⇒ `AddKeyed{Singleton,Scoped,Transient}` | In scope for v1; `.NET 10` target supports keyed DI |
| Emit target namespace | `JoakimAnder.Toolbox.DependencyInjection` | Clear provenance; explicit `using` at the call site is acceptable |
| Emit kind | `internal static partial class AttributedServiceCollectionExtensions` | Internal matches the internal attributes; `partial` lets a consumer extend it |
| Registration call style | Plain `Add*` / `AddKeyed*` | Matches the explicit intent; `TryAdd` deliberately cut (see Out of scope) |
| Self + interface both | Not done — only the stated service type is registered | Cut in scoping; keeps emit one-line-per-registration |
| Multiplicity | Attributes are `AllowMultiple = true`, `Inherited = false` | One class can register into several groups / as several types by repeating the attribute; subclasses don't silently inherit intent |
| Generator API | `IIncrementalGenerator` + `ForAttributeWithMetadataName` | Modern, incremental, purpose-built for post-init attribute discovery; good IDE perf in consumer projects |
| Pipeline data model | All-scalar equatable `readonly record struct` | Correct incremental caching; no `ISymbol`/syntax/`Compilation` leaks |
| Emission style | Raw string templates | Output is fixed-shape and regular; readable and snapshot-testable, unlike `SyntaxFactory` verbosity |
| Output ordering | Sorted before emit (group, impl FQN, service type, key) | Byte-identical output regardless of file-discovery order (`Deterministic=true`) |
| Project layout | One folder per generator feature under `SourceGenerators/` | Multi-generator-ready; a future generator is a new sibling folder |

### Why `ForAttributeWithMetadataName` over alternatives

`FAWMN` is the supported pairing with post-init-emitted attributes and is far cheaper than a hand-rolled `CreateSyntaxProvider` predicate. A classic `ISourceGenerator` + `ISyntaxReceiver` was rejected: it has no incremental caching, so it re-runs on every keystroke in the consumer's IDE — the exact failure mode the incremental pipeline exists to avoid. `SyntaxFactory`-based emission was rejected as needless verbosity for output this regular.

### The caching discipline (the one real risk of this approach)

An incremental generator memoizes each pipeline stage by `.Equals()` on its input. If an `ISymbol`, `SyntaxNode`, `Location`, or `Compilation` reaches the cached model, equality becomes identity-based, every keystroke produces a fresh non-equal instance, the cache always misses, and the generator re-runs fully (and pins the compilation in memory). It does this **silently** — output stays correct, only IDE perf degrades. Mitigations, applied once:

1. The transform projects symbols to **strings/enums immediately**; nothing Roslyn-typed escapes it.
2. The model is an all-scalar `readonly record struct` ⇒ value equality for free (no array members, which would silently break record equality via reference comparison).
3. Diagnostics travel as a `DiagnosticInfo` value type carrying a `LocationInfo` (file path + span as ints); a real `Location` is rebuilt only at report time.
4. A **caching regression test** asserts the second run reports `Cached`/`Unchanged` steps — making the discipline enforceable, not aspirational.

## Project layout

All generator code lives in the existing `src/JoakimAnder.Toolbox.SourceGenerators` (netstandard2.0, `IsRoslynComponent`, already packed into the NuGet under `analyzers/dotnet/cs`). One folder per generator feature so the project is multi-generator-ready:

```
src/JoakimAnder.Toolbox.SourceGenerators/
  DependencyInjection/
    DependencyInjectionGenerator.cs   // [Generator] IIncrementalGenerator
    Model/
      ServiceRegistration.cs          // equatable record (all-scalar)
      Lifetime.cs                     // enum: Singleton/Scoped/Transient
      LocationInfo.cs                 // cache-safe location for diagnostics
      DiagnosticInfo.cs               // cache-safe diagnostic payload
    Emit/
      AttributeSource.cs              // const strings for the post-init attributes
      RegistrationEmitter.cs          // builds the partial-class text
    Diagnostics/
      DiagnosticDescriptors.cs        // TBX1001…TBX1004
```

`src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs` is **deleted** (its comment says "until the DI source generator lands"). Anything later shared across generators (e.g. `LocationInfo`) graduates to a top-level `Common/` folder only when a second generator needs it — not created speculatively.

**Namespace convention** — folder mirrors namespace, and the two `DependencyInjection`s are deliberately distinct roots:

| | Namespace |
|---|---|
| The generator's own internal code | `JoakimAnder.Toolbox.SourceGenerators.DependencyInjection` (+ `.Model`, `.Emit`, `.Diagnostics`) |
| The code emitted into consumers (attributes + extension class) | `JoakimAnder.Toolbox.DependencyInjection` |

## Public API (the consumer's surface)

### Attributes (injected via `RegisterPostInitializationOutput`)

Emitted verbatim into every consumer compilation as `<auto-generated>`, nullable-enabled, `internal sealed`, in `JoakimAnder.Toolbox.DependencyInjection`:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class SingletonAttribute : Attribute
{
    public SingletonAttribute(Type? serviceType = null) => ServiceType = serviceType;
    public Type? ServiceType { get; }
    public string? Group { get; set; }
    public string? Key { get; set; }
}
// ScopedAttribute, TransientAttribute — identical shape, different name
```

Three separate source constants (not one parameterized template) so the emitted attributes read like ordinary hand-written code and are trivially snapshot-tested.

### Generated entry points

One `internal static partial class AttributedServiceCollectionExtensions` containing a `public static IServiceCollection AddAttributed{Group}Services(this IServiceCollection services)` method per distinct group; the default (no `Group`) is `AddAttributedServices`. Each returns `services` for chaining. A method exists **iff** its group has at least one registration; if no class is attributed at all, no extensions file is emitted (only the attribute file).

## Pipeline

```
RegisterPostInitializationOutput ──► emits the 3 attribute definitions (runs once)

ForAttributeWithMetadataName × 3  ──► one provider per attribute, e.g.
  (Singleton / Scoped / Transient)    "JoakimAnder.Toolbox.DependencyInjection.SingletonAttribute"
        │  transform: (INamedTypeSymbol + AttributeData) ─► all-scalar Result (+ validation)
        ▼
  IncrementalValuesProvider<Result>   Result carries a ServiceRegistration OR a DiagnosticInfo
        │  merge the 3 providers, then .Collect()
        ▼
RegisterSourceOutput ──► report diagnostics; sort; group; emit one "AttributedServices.g.cs"
```

A small separate provider yields a `bool` "is `IServiceCollection` referenced?" (not the `Compilation` itself) to drive `TBX1004` without harming caching.

Because `AllowMultiple = true`, FAWMN hands the transform all matching attribute instances on a symbol; each produces one `ServiceRegistration`. A class bearing both `[Singleton]` and `[Scoped]` is seen by two different providers and yields one registration each.

### Data model

```csharp
internal enum Lifetime { Singleton, Scoped, Transient }

internal readonly record struct ServiceRegistration(
    string  ImplementationType,  // global::-qualified FQN
    string? ServiceType,         // global::-qualified FQN; null ⇒ register as self
    Lifetime Lifetime,
    string? Group,               // null/empty ⇒ default AddAttributedServices
    string? Key);                // null ⇒ non-keyed
```

All-scalar ⇒ value equality for free, no custom comparer.

## Emitted code

Given consumer code:

```csharp
namespace MyApp;
[Singleton(typeof(IClock))]                 class Clock : IClock { }
[Scoped]                                    class Cache { }
[Singleton(typeof(ICache), Key = "redis")]  class RedisCache : ICache { }
[Scoped(typeof(IOrders), Group = "Web")]    class OrderService : IOrders { }
```

The generator emits `AttributedServices.g.cs`:

```csharp
// <auto-generated/>
#nullable enable
namespace JoakimAnder.Toolbox.DependencyInjection;

using global::Microsoft.Extensions.DependencyInjection;

internal static partial class AttributedServiceCollectionExtensions
{
    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddAttributedServices(
        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        services.AddScoped<global::MyApp.Cache>();
        services.AddSingleton<global::MyApp.IClock, global::MyApp.Clock>();
        services.AddKeyedSingleton<global::MyApp.ICache, global::MyApp.RedisCache>("redis");
        return services;
    }

    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddAttributedWebServices(
        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        services.AddScoped<global::MyApp.IOrders, global::MyApp.OrderService>();
        return services;
    }
}
```

Emission rules:

- **Every type name is `global::`-qualified** — generated-code hygiene against namespace collisions.
- **Self vs. mapped:** no `serviceType` ⇒ single-arg generic (`AddScoped<Cache>()`); `serviceType` given ⇒ two-arg (`AddSingleton<IClock, Clock>()`).
- **Keyed:** switches to `AddKeyed*` with the key as a string literal; keyed + self ⇒ `AddKeyedScoped<Foo>("k")`.
- **Group** ⇒ its own method, name spliced verbatim (`Web` ⇒ `AddAttributedWebServices`).
- Registrations sorted (group, impl FQN, service type, key) for deterministic output.

## Diagnostics

Category `JoakimAnder.Toolbox.DependencyInjection`, pointing at the offending attribute via a rebuilt `Location`:

| ID | Severity | When |
|---|---|---|
| `TBX1001` | Error | Stated `typeof(...)` is **not assignable** from the marked class |
| `TBX1002` | Error | Marked class is **abstract or static** — the container can't construct it as an implementation |
| `TBX1003` | Error | `Group` value is **not a valid C# identifier** (it's spliced into a method name) |
| `TBX1004` | Warning | Attributes used but **`IServiceCollection` isn't referenced**; generated method wouldn't compile, so emission is skipped |

A class hitting `TBX1001`/`TBX1002` is skipped (no registration emitted) so the consumer sees the clear diagnostic rather than a downstream `CS` cascade.

## Testing

In the existing `tests/JoakimAnder.Toolbox.SourceGenerators.Tests` (already wired with `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit`); xUnit + built-in assertions per repo convention. Replaces the placeholder `SmokeTests.cs`.

**Generation correctness:**
- Post-init: the three attributes are emitted, `internal sealed`, correct namespace/usage.
- Each lifetime ⇒ correct `Add{Singleton,Scoped,Transient}` call.
- Self vs. mapped: one-arg vs. two-arg generic.
- Keyed: `AddKeyed*` with the literal; keyed-self variant.
- Groups: distinct group ⇒ its own method; default ⇒ `AddAttributedServices`; method exists iff group non-empty.
- `AllowMultiple`: one class with two attributes ⇒ registered in both targets.
- Determinism: shuffled declaration order ⇒ byte-identical output.

**Diagnostics:** one focused test per `TBX1001`–`TBX1004`; assert the offending class is skipped, not cascaded.

**Caching regression:** drive the generator twice via `GeneratorDriver`; assert tracked steps report `IncrementalStepRunReason.Cached`/`Unchanged`.

**Edge cases:** no attributed types ⇒ no extensions file (only the attribute file); class with `[Singleton]` and `[Scoped]` ⇒ one registration each; nested / file-scoped namespaces resolve to correct `global::` FQNs.

## Example

One snippet in `examples/JoakimAnder.Toolbox.Examples` (add a `Microsoft.Extensions.DependencyInjection` package reference): a couple of attributed classes, then `new ServiceCollection().AddAttributedServices().BuildServiceProvider()`, resolving one service to show it wired up — including one grouped and one keyed registration to exercise those paths.

## Scope

**In scope:**
- `DependencyInjectionGenerator` + model/emit/diagnostics under `src/.../SourceGenerators/DependencyInjection/`.
- Three injected `internal` attributes via post-init.
- `FAWMN` × 3 pipeline, all-scalar equatable model, sorted deterministic emit.
- Grouped `AddAttributed*Services` extension methods; explicit-or-self service type; keyed services.
- Four diagnostics (`TBX1001`–`TBX1004`).
- Delete `SourceGenerators/Placeholder.cs`; replace the test-project `SmokeTests.cs`.
- xUnit tests including the caching regression; one runnable example.
- Update `README.md` ("What's in the box") and `CHANGELOG.md`.

**Out of scope (YAGNI / explicitly cut):**
- `TryAdd` semantics and dual self+interface registration (cut in scoping).
- Auto-inferring the service type from implemented interfaces (cut — always explicit-or-self).
- A single `[Service(lifetime)]` attribute / lifetime enum (cut — per-lifetime attributes).
- Configurable method-name prefix (fixed `AddAttributed…Services`).
- Open generics, factory/lambda registrations, decorators, conditional registration.
- Multiple service types in one attribute (repeat the attribute instead).
- Assembly / referenced-assembly scanning (attributes are `internal`; current compilation only).
- Constructor/property/method injection of any kind.

**Non-goals:**
- Replacing a full DI container or Scrutor-style runtime scanning.
- Any runtime-reflection registration path.

## Open questions

None at design time. One implementation detail is left to execution: whether the three `FAWMN` providers each carry their `Lifetime` as a constant or share one transform parameterized by lifetime — purely internal, no behavior or output impact.
