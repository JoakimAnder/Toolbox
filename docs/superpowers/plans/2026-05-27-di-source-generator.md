# DI Attributes + Source Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Roslyn incremental source generator that turns `[Singleton]`/`[Scoped]`/`[Transient]` attributes on consumer classes into `IServiceCollection.AddAttributed*Services()` extension methods.

**Architecture:** A single `IIncrementalGenerator` in the existing `JoakimAnder.Toolbox.SourceGenerators` project. It injects the three attribute definitions into each consumer via `RegisterPostInitializationOutput`, discovers usages with `ForAttributeWithMetadataName`, projects each usage to an all-scalar equatable model (so incremental caching is correct), then emits one grouped extension class. Validation surfaces as diagnostics `TBX1001`–`TBX1004`.

**Tech Stack:** C# (netstandard2.0 generator, net10.0 tests/examples), Microsoft.CodeAnalysis.CSharp 4.11.0, xUnit + `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit`, `Microsoft.Extensions.DependencyInjection(.Abstractions)` 10.0.0.

**Design spec:** [docs/superpowers/specs/2026-05-27-di-source-generator-design.md](../specs/2026-05-27-di-source-generator-design.md)

---

## File structure

Generator project — `src/JoakimAnder.Toolbox.SourceGenerators/`:

| File | Responsibility |
|---|---|
| `DependencyInjection/DependencyInjectionGenerator.cs` | `[Generator]` entry point; wires the incremental pipeline |
| `DependencyInjection/Parser.cs` | Transform: `GeneratorAttributeSyntaxContext` → `RegistrationResult` (scalar projection + validation) |
| `DependencyInjection/Model/Lifetime.cs` | `enum Lifetime { Singleton, Scoped, Transient }` |
| `DependencyInjection/Model/ServiceRegistration.cs` | All-scalar equatable `readonly record struct` |
| `DependencyInjection/Model/RegistrationResult.cs` | Equatable carrier of `ServiceRegistration?` + `DiagnosticInfo?` |
| `DependencyInjection/Model/LocationInfo.cs` | Cache-safe location (rebuilds `Location` at report time) |
| `DependencyInjection/Model/DiagnosticInfo.cs` | Cache-safe diagnostic payload |
| `DependencyInjection/Model/EquatableArray.cs` | `ImmutableArray<T>` wrapper with structural equality |
| `DependencyInjection/Emit/AttributeSource.cs` | `const string` of the three attribute definitions |
| `DependencyInjection/Emit/RegistrationEmitter.cs` | Builds the `AttributedServiceCollectionExtensions` text |
| `DependencyInjection/Diagnostics/DiagnosticDescriptors.cs` | `TBX1001`–`TBX1004` descriptors |
| `Polyfills/IsExternalInit.cs` | netstandard2.0 polyfill enabling `record`/`init` |
| ~~`Placeholder.cs`~~ | **Deleted** |

Test project — `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/`:

| File | Responsibility |
|---|---|
| `GeneratorTestHelper.cs` | Drives the generator over source, exposes generated text + diagnostics |
| `AttributeEmissionTests.cs` | Post-init attribute output |
| `RegistrationTests.cs` | Core emit: lifetimes, self/mapped, grouping, determinism, AllowMultiple |
| `KeyedServiceTests.cs` | Keyed registrations |
| `DiagnosticTests.cs` | `TBX1001`–`TBX1004` |
| `CachingTests.cs` | Incremental caching regression |
| ~~`SmokeTests.cs`~~ | **Deleted** |

Other:
- `Directory.Packages.props` — add `Microsoft.Extensions.DependencyInjection(.Abstractions)` versions.
- `tests/.../*.Tests.csproj` — add `Microsoft.Extensions.DependencyInjection.Abstractions` reference.
- `examples/.../*.csproj` — add `Microsoft.Extensions.DependencyInjection` ref + analyzer reference to the generator.
- `examples/.../Program.cs`, `README.md`, `CHANGELOG.md` — docs/usage.

---

### Task 1: Generator skeleton, attribute injection, and test harness

**Goal:** A registered incremental generator that injects the three attribute definitions into consumers, plus a driver-based test harness, with the post-init output covered by a test.

**Files:**
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/Polyfills/IsExternalInit.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Emit/AttributeSource.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs`
- Delete: `src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs`
- Modify: `Directory.Packages.props`
- Modify: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/GeneratorTestHelper.cs`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/AttributeEmissionTests.cs`
- Delete: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/SmokeTests.cs`

**Acceptance Criteria:**
- [ ] Building the solution runs the generator with no errors/warnings.
- [ ] A test asserts the generator emits an attribute source containing `internal sealed class SingletonAttribute`, `ScopedAttribute`, `TransientAttribute`, each with `AllowMultiple = true`, `Inherited = false`, a `Type? serviceType = null` constructor, and `Group`/`Key` string properties.
- [ ] `Placeholder.cs` and `SmokeTests.cs` are gone.

**Verify:** `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests` → all pass.

**Steps:**

- [ ] **Step 1: Add package versions.** In `Directory.Packages.props`, add inside the existing `<ItemGroup>`:

```xml
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

- [ ] **Step 2: Reference Abstractions from the test project.** In `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj`, add to the package `<ItemGroup>`:

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
```

- [ ] **Step 3: Add the IsExternalInit polyfill** (netstandard2.0 has no `init`/`record` support without it). Create `Polyfills/IsExternalInit.cs`:

```csharp
namespace System.Runtime.CompilerServices
{
    // Enables `init` accessors and records on netstandard2.0.
    internal static class IsExternalInit { }
}
```

- [ ] **Step 4: Write the attribute source.** Create `DependencyInjection/Emit/AttributeSource.cs`:

```csharp
namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;

internal static class AttributeSource
{
    public const string HintName = "JoakimAnder.Toolbox.DependencyInjection.Attributes.g.cs";

    public const string SingletonMetadataName = "JoakimAnder.Toolbox.DependencyInjection.SingletonAttribute";
    public const string ScopedMetadataName = "JoakimAnder.Toolbox.DependencyInjection.ScopedAttribute";
    public const string TransientMetadataName = "JoakimAnder.Toolbox.DependencyInjection.TransientAttribute";

    public const string Source = """
        // <auto-generated/>
        #nullable enable
        namespace JoakimAnder.Toolbox.DependencyInjection
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            internal sealed class SingletonAttribute : global::System.Attribute
            {
                public SingletonAttribute(global::System.Type? serviceType = null) => ServiceType = serviceType;
                public global::System.Type? ServiceType { get; }
                public string? Group { get; set; }
                public string? Key { get; set; }
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            internal sealed class ScopedAttribute : global::System.Attribute
            {
                public ScopedAttribute(global::System.Type? serviceType = null) => ServiceType = serviceType;
                public global::System.Type? ServiceType { get; }
                public string? Group { get; set; }
                public string? Key { get; set; }
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            internal sealed class TransientAttribute : global::System.Attribute
            {
                public TransientAttribute(global::System.Type? serviceType = null) => ServiceType = serviceType;
                public global::System.Type? ServiceType { get; }
                public string? Group { get; set; }
                public string? Key { get; set; }
            }
        }
        """;
}
```

- [ ] **Step 5: Write the generator skeleton.** Create `DependencyInjection/DependencyInjectionGenerator.cs`:

```csharp
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

[Generator(LanguageNames.CSharp)]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(AttributeSource.HintName, AttributeSource.Source));
    }
}
```

- [ ] **Step 6: Delete the placeholders.**

```bash
git rm src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs
git rm tests/JoakimAnder.Toolbox.SourceGenerators.Tests/SmokeTests.cs
```

- [ ] **Step 7: Write the test harness.** Create `GeneratorTestHelper.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

internal sealed record GeneratorOutcome(
    GeneratorDriverRunResult Result,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompileErrors)
{
    public string GeneratedSource(string hintNameContains) =>
        Result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName.Contains(hintNameContains))
            .SourceText.ToString();

    public bool HasGeneratedSource(string hintNameContains) =>
        Result.Results.SelectMany(r => r.GeneratedSources).Any(s => s.HintName.Contains(hintNameContains));
}

internal static class GeneratorTestHelper
{
    public static GeneratorOutcome Run(string source)
    {
        var compilation = CreateCompilation(source);

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DependencyInjectionGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        var ran = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var compileErrors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorOutcome(ran.GetRunResult(), ran.GetRunResult().Diagnostics, compileErrors);
    }

    public static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .Append(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location))
            .Distinct();

        return CSharpCompilation.Create(
            assemblyName: "Tests.Generated",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
```

- [ ] **Step 8: Write the post-init test.** Create `AttributeEmissionTests.cs`:

```csharp
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class AttributeEmissionTests
{
    [Fact]
    public void Emits_the_three_marker_attributes()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        var src = outcome.GeneratedSource("Attributes.g.cs");

        Assert.Contains("internal sealed class SingletonAttribute", src);
        Assert.Contains("internal sealed class ScopedAttribute", src);
        Assert.Contains("internal sealed class TransientAttribute", src);
        Assert.Contains("AllowMultiple = true", src);
        Assert.Contains("Inherited = false", src);
        Assert.Contains("ServiceType { get; }", src);
        Assert.Contains("string? Group { get; set; }", src);
        Assert.Contains("string? Key { get; set; }", src);
    }

    [Fact]
    public void Generated_attributes_compile_clean()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        Assert.Empty(outcome.CompileErrors);
    }
}
```

- [ ] **Step 9: Run tests.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests`. Expected: PASS.

- [ ] **Step 10: Commit.**

```bash
git add -A
git commit -m "feat(di): inject marker attributes via source generator post-init"
```

```json:metadata
{"files": ["src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Emit/AttributeSource.cs", "src/JoakimAnder.Toolbox.SourceGenerators/Polyfills/IsExternalInit.cs", "tests/JoakimAnder.Toolbox.SourceGenerators.Tests/GeneratorTestHelper.cs", "tests/JoakimAnder.Toolbox.SourceGenerators.Tests/AttributeEmissionTests.cs", "Directory.Packages.props"], "verifyCommand": "dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests", "acceptanceCriteria": ["Generator builds clean", "Post-init emits the three attributes with correct shape", "Placeholder.cs and SmokeTests.cs deleted"]}
```

---

### Task 2: Core discovery and emit (lifetimes, self/mapped, grouping, determinism)

**Goal:** Discover attributed classes, project them to an equatable model, and emit grouped `AddAttributed*Services` methods for the three lifetimes with self and mapped registration.

**Files:**
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/Lifetime.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/ServiceRegistration.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/EquatableArray.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Parser.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Emit/RegistrationEmitter.cs`
- Modify: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/RegistrationTests.cs`

**Acceptance Criteria:**
- [ ] `[Singleton]`/`[Scoped]`/`[Transient]` with no `serviceType` emit `Add{Lifetime}<global::Ns.Impl>()`.
- [ ] With `typeof(IFoo)` they emit `Add{Lifetime}<global::Ns.IFoo, global::Ns.Impl>()`.
- [ ] `Group = "Web"` produces a separate `AddAttributedWebServices`; no group produces `AddAttributedServices`; a method exists only if its group has registrations.
- [ ] A class with two attributes (e.g. two groups) registers once in each target method.
- [ ] Shuffling declaration order produces byte-identical generated source.
- [ ] Generated source compiles clean.

**Verify:** `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing test.** Create `RegistrationTests.cs`:

```csharp
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class RegistrationTests
{
    const string Prelude = "namespace App { using JoakimAnder.Toolbox.DependencyInjection; ";

    static string Generated(string body) =>
        GeneratorTestHelper.Run(Prelude + body + " }").GeneratedSource("AttributedServices.g.cs");

    [Fact]
    public void Self_registration_uses_single_type_argument()
    {
        var src = Generated("[Scoped] class Cache { }");
        Assert.Contains("AddScoped<global::App.Cache>()", src);
    }

    [Fact]
    public void Mapped_registration_uses_two_type_arguments()
    {
        var src = Generated("interface IClock { } [Singleton(typeof(IClock))] class Clock : IClock { }");
        Assert.Contains("AddSingleton<global::App.IClock, global::App.Clock>()", src);
    }

    [Fact]
    public void Transient_lifetime_maps_to_AddTransient()
    {
        var src = Generated("[Transient] class Worker { }");
        Assert.Contains("AddTransient<global::App.Worker>()", src);
    }

    [Fact]
    public void Group_emits_a_separate_method()
    {
        var src = Generated("[Scoped(Group = \"Web\")] class A { } [Scoped] class B { }");
        Assert.Contains("AddAttributedWebServices(", src);
        Assert.Contains("AddAttributedServices(", src);
    }

    [Fact]
    public void Multiple_attributes_register_in_each_group()
    {
        var src = Generated("[Scoped] [Scoped(Group = \"Web\")] class A { }");
        Assert.Contains("AddAttributedServices(", src);
        Assert.Contains("AddAttributedWebServices(", src);
    }

    [Fact]
    public void Output_is_deterministic_regardless_of_declaration_order()
    {
        var a = Generated("[Scoped] class A { } [Singleton] class B { } [Transient] class C { }");
        var b = Generated("[Transient] class C { } [Scoped] class A { } [Singleton] class B { }");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Generated_registrations_compile_clean()
    {
        var outcome = GeneratorTestHelper.Run(Prelude + "interface IClock { } [Singleton(typeof(IClock))] class Clock : IClock { } }");
        Assert.Empty(outcome.CompileErrors);
    }
}
```

- [ ] **Step 2: Run to confirm failure.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter RegistrationTests`. Expected: FAIL (no `AttributedServices.g.cs` produced yet).

- [ ] **Step 3: Add the `Lifetime` enum.** Create `Model/Lifetime.cs`:

```csharp
namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

internal enum Lifetime
{
    Singleton,
    Scoped,
    Transient,
}
```

- [ ] **Step 4: Add the model.** Create `Model/ServiceRegistration.cs`:

```csharp
namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// All members are scalar so record-struct value equality is correct — required for
// incremental caching. No ISymbol / SyntaxNode / Location is allowed here.
internal readonly record struct ServiceRegistration(
    string ImplementationType,  // global::-qualified fully-qualified name
    string? ServiceType,        // global::-qualified FQN; null => register as self
    Lifetime Lifetime,
    string? Group,              // null/empty => default AddAttributedServices
    string? Key);               // null => non-keyed
```

- [ ] **Step 5: Add `EquatableArray<T>`** (netstandard2.0-safe: no `System.HashCode`, no `Span.SequenceEqual`). Create `Model/EquatableArray.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// ImmutableArray<T> compares by underlying reference, which silently breaks
// incremental caching. This wrapper gives structural value equality.
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array) => _array = array;

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public bool Equals(EquatableArray<T> other)
    {
        var a = AsImmutableArray();
        var b = other.AsImmutableArray();
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in AsImmutableArray())
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

- [ ] **Step 6: Add the parser/transform.** Create `Parser.cs`:

```csharp
using System.Collections.Immutable;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

internal static class Parser
{
    public static EquatableArray<ServiceRegistration> Parse(GeneratorAttributeSyntaxContext context, Lifetime lifetime)
    {
        var implementation = (INamedTypeSymbol)context.TargetSymbol;
        var implName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var builder = ImmutableArray.CreateBuilder<ServiceRegistration>(context.Attributes.Length);
        foreach (var attribute in context.Attributes)
        {
            string? serviceType = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol service)
            {
                serviceType = service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            builder.Add(new ServiceRegistration(
                implName,
                serviceType,
                lifetime,
                NamedString(attribute, "Group"),
                NamedString(attribute, "Key")));
        }

        return new EquatableArray<ServiceRegistration>(builder.ToImmutable());
    }

    private static string? NamedString(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
            if (arg.Key == name && arg.Value.Value is string s && s.Length > 0)
                return s;
        return null;
    }
}
```

- [ ] **Step 7: Add the emitter.** Create `Emit/RegistrationEmitter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;

internal static class RegistrationEmitter
{
    public const string HintName = "AttributedServices.g.cs";

    public static string Emit(IReadOnlyList<ServiceRegistration> registrations)
    {
        var sorted = registrations
            .OrderBy(r => r.Group ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.ImplementationType, StringComparer.Ordinal)
            .ThenBy(r => r.ServiceType ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.Key ?? "", StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace JoakimAnder.Toolbox.DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine("    internal static partial class AttributedServiceCollectionExtensions");
        sb.AppendLine("    {");

        var groups = sorted
            .GroupBy(r => r.Group ?? "")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            var methodName = "AddAttributed" + groups[i].Key + "Services";
            sb.AppendLine($"        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection {methodName}(");
            sb.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
            sb.AppendLine("        {");
            foreach (var reg in groups[i])
                sb.AppendLine("            " + Line(reg));
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Line(ServiceRegistration reg)
    {
        var method = reg.Lifetime switch
        {
            Lifetime.Singleton => reg.Key is null ? "AddSingleton" : "AddKeyedSingleton",
            Lifetime.Scoped => reg.Key is null ? "AddScoped" : "AddKeyedScoped",
            _ => reg.Key is null ? "AddTransient" : "AddKeyedTransient",
        };

        var typeArgs = reg.ServiceType is null
            ? $"<{reg.ImplementationType}>"
            : $"<{reg.ServiceType}, {reg.ImplementationType}>";

        var args = reg.Key is null ? "" : SymbolDisplay.FormatLiteral(reg.Key, quote: true);

        return $"services.{method}{typeArgs}({args});";
    }
}
```

- [ ] **Step 8: Wire the pipeline.** Replace the body of `DependencyInjectionGenerator.Initialize` so the file reads:

```csharp
using System.Collections.Generic;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

[Generator(LanguageNames.CSharp)]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(AttributeSource.HintName, AttributeSource.Source));

        var singletons = Discover(context, AttributeSource.SingletonMetadataName, Lifetime.Singleton, "Singletons");
        var scoped = Discover(context, AttributeSource.ScopedMetadataName, Lifetime.Scoped, "Scoped");
        var transient = Discover(context, AttributeSource.TransientMetadataName, Lifetime.Transient, "Transient");

        var all = singletons.Collect().Combine(scoped.Collect()).Combine(transient.Collect());

        context.RegisterSourceOutput(all, static (spc, data) =>
        {
            var registrations = new List<ServiceRegistration>();
            foreach (var arr in data.Left.Left) registrations.AddRange(arr.AsImmutableArray());
            foreach (var arr in data.Left.Right) registrations.AddRange(arr.AsImmutableArray());
            foreach (var arr in data.Right) registrations.AddRange(arr.AsImmutableArray());

            if (registrations.Count == 0) return;

            spc.AddSource(RegistrationEmitter.HintName, RegistrationEmitter.Emit(registrations));
        });
    }

    private static IncrementalValuesProvider<EquatableArray<ServiceRegistration>> Discover(
        IncrementalGeneratorInitializationContext context, string metadataName, Lifetime lifetime, string trackingName) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName(
                metadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => Parser.Parse(ctx, lifetime))
            .WithTrackingName(trackingName);
}
```

- [ ] **Step 9: Run tests.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests`. Expected: PASS.

- [ ] **Step 10: Commit.**

```bash
git add -A
git commit -m "feat(di): emit grouped AddAttributed*Services from lifetime attributes"
```

```json:metadata
{"files": ["src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/Lifetime.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/ServiceRegistration.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/EquatableArray.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Parser.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Emit/RegistrationEmitter.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs", "tests/JoakimAnder.Toolbox.SourceGenerators.Tests/RegistrationTests.cs"], "verifyCommand": "dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests", "acceptanceCriteria": ["Self and mapped registration emit correct arity", "All three lifetimes map correctly", "Groups emit separate methods", "AllowMultiple registers in each group", "Output deterministic regardless of order", "Generated code compiles"]}
```

---

### Task 3: Keyed services

**Goal:** A `Key` on any attribute emits the matching `AddKeyed*` call with the key as a string-literal argument.

**Files:**
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/KeyedServiceTests.cs`

**Acceptance Criteria:**
- [ ] `[Singleton(typeof(ICache), Key = "redis")]` emits `AddKeyedSingleton<global::App.ICache, global::App.RedisCache>("redis")`.
- [ ] Keyed self registration emits `AddKeyed{Lifetime}<global::App.Impl>("k")`.
- [ ] A keyed registration with a `Group` lands in that group's method.
- [ ] Generated keyed code compiles clean.

**Verify:** `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter KeyedServiceTests` → all pass.

> The emitter from Task 2 already branches on `reg.Key`. This task verifies that path end-to-end; no production change is expected. If a test fails, fix `RegistrationEmitter.Line` / `Parser` accordingly.

**Steps:**

- [ ] **Step 1: Write the tests.** Create `KeyedServiceTests.cs`:

```csharp
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class KeyedServiceTests
{
    const string Prelude = "namespace App { using JoakimAnder.Toolbox.DependencyInjection; ";

    static GeneratorOutcome Run(string body) => GeneratorTestHelper.Run(Prelude + body + " }");
    static string Generated(string body) => Run(body).GeneratedSource("AttributedServices.g.cs");

    [Fact]
    public void Keyed_mapped_registration_emits_AddKeyed_with_literal()
    {
        var src = Generated("interface ICache { } [Singleton(typeof(ICache), Key = \"redis\")] class RedisCache : ICache { }");
        Assert.Contains("AddKeyedSingleton<global::App.ICache, global::App.RedisCache>(\"redis\")", src);
    }

    [Fact]
    public void Keyed_self_registration_emits_single_type_argument()
    {
        var src = Generated("[Transient(Key = \"fast\")] class Worker { }");
        Assert.Contains("AddKeyedTransient<global::App.Worker>(\"fast\")", src);
    }

    [Fact]
    public void Keyed_registration_respects_group()
    {
        var src = Generated("[Scoped(Key = \"k\", Group = \"Web\")] class A { }");
        Assert.Contains("AddAttributedWebServices(", src);
        Assert.Contains("AddKeyedScoped<global::App.A>(\"k\")", src);
    }

    [Fact]
    public void Keyed_generated_code_compiles_clean()
    {
        var outcome = Run("interface ICache { } [Singleton(typeof(ICache), Key = \"redis\")] class RedisCache : ICache { }");
        Assert.Empty(outcome.CompileErrors);
    }
}
```

- [ ] **Step 2: Run tests.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter KeyedServiceTests`. Expected: PASS. If any fail, adjust `RegistrationEmitter.Line` until green.

- [ ] **Step 3: Commit.**

```bash
git add -A
git commit -m "test(di): cover keyed service registration"
```

```json:metadata
{"files": ["tests/JoakimAnder.Toolbox.SourceGenerators.Tests/KeyedServiceTests.cs"], "verifyCommand": "dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter KeyedServiceTests", "acceptanceCriteria": ["Keyed mapped emits AddKeyed* with literal", "Keyed self uses single type arg", "Keyed respects group", "Keyed code compiles"]}
```

---

### Task 4: Diagnostics (TBX1001–TBX1004)

**Goal:** Report `TBX1001` (service type not assignable), `TBX1002` (abstract/static implementation), `TBX1003` (invalid group identifier), and `TBX1004` (no `IServiceCollection` reference), skipping the offending registration rather than cascading into `CS` errors.

**Files:**
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/LocationInfo.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/DiagnosticInfo.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/RegistrationResult.cs`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Diagnostics/DiagnosticDescriptors.cs`
- Modify: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Parser.cs`
- Modify: `src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/DiagnosticTests.cs`

**Acceptance Criteria:**
- [ ] `[Singleton(typeof(IFoo))]` on a class not implementing `IFoo` → `TBX1001`, no registration emitted for it.
- [ ] `[Scoped]` on an `abstract` or `static` class → `TBX1002`, skipped.
- [ ] `Group = "not valid"` (non-identifier) → `TBX1003`, skipped.
- [ ] Attributes used in a compilation with no `IServiceCollection` reference → `TBX1004` (warning), no extension file emitted.
- [ ] A valid sibling registration in the same compilation still emits.

**Verify:** `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter DiagnosticTests` → all pass.

**Steps:**

- [ ] **Step 1: Write the tests.** Create `DiagnosticTests.cs`:

```csharp
using System.Linq;
using JoakimAnder.Toolbox.SourceGenerators.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class DiagnosticTests
{
    const string Prelude = "namespace App { using JoakimAnder.Toolbox.DependencyInjection; ";

    static GeneratorOutcome Run(string body) => GeneratorTestHelper.Run(Prelude + body + " }");

    [Fact]
    public void Non_assignable_service_type_reports_TBX1001()
    {
        var outcome = Run("interface IFoo { } [Singleton(typeof(IFoo))] class Bar { }");
        Assert.Contains(outcome.GeneratorDiagnostics, d => d.Id == "TBX1001");
        Assert.False(outcome.HasGeneratedSource("AttributedServices.g.cs"));
    }

    [Fact]
    public void Abstract_implementation_reports_TBX1002()
    {
        var outcome = Run("[Scoped] abstract class Base { }");
        Assert.Contains(outcome.GeneratorDiagnostics, d => d.Id == "TBX1002");
    }

    [Fact]
    public void Static_implementation_reports_TBX1002()
    {
        var outcome = Run("[Scoped] static class Helpers { }");
        Assert.Contains(outcome.GeneratorDiagnostics, d => d.Id == "TBX1002");
    }

    [Fact]
    public void Invalid_group_identifier_reports_TBX1003()
    {
        var outcome = Run("[Scoped(Group = \"not valid\")] class A { }");
        Assert.Contains(outcome.GeneratorDiagnostics, d => d.Id == "TBX1003");
    }

    [Fact]
    public void Valid_sibling_still_registers_when_one_is_invalid()
    {
        var outcome = Run("interface IFoo { } [Singleton(typeof(IFoo))] class Bad { } [Scoped] class Good { }");
        Assert.Contains(outcome.GeneratorDiagnostics, d => d.Id == "TBX1001");
        Assert.Contains("AddScoped<global::App.Good>()", outcome.GeneratedSource("AttributedServices.g.cs"));
    }

    [Fact]
    public void Missing_service_collection_reference_reports_TBX1004()
    {
        // Compile WITHOUT the DI Abstractions reference present.
        var source = Prelude + "[Scoped] class A { } }";
        var tree = CSharpSyntaxTree.ParseText(source);
        var corelib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("NoDi", new[] { tree }, new[] { corelib },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            new[] { new DependencyInjection.DependencyInjectionGenerator().AsSourceGenerator() });
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "TBX1004");
    }
}
```

- [ ] **Step 2: Run to confirm failure.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter DiagnosticTests`. Expected: FAIL.

- [ ] **Step 3: Add `LocationInfo`.** Create `Model/LocationInfo.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// Cache-safe stand-in for Location (which holds an editor-changing source span).
// All members are equatable value types; a real Location is rebuilt at report time.
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(SyntaxNode? node)
    {
        if (node is null) return null;
        var location = node.GetLocation();
        if (location.SourceTree is null) return null;
        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}
```

- [ ] **Step 4: Add `DiagnosticInfo`.** Create `Model/DiagnosticInfo.cs`:

```csharp
using System.Linq;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs.AsImmutableArray().Cast<object>().ToArray());
}
```

- [ ] **Step 5: Add `RegistrationResult`.** Create `Model/RegistrationResult.cs`:

```csharp
namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// Exactly one of the two is non-null. Both members are equatable => cache-safe.
internal readonly record struct RegistrationResult(ServiceRegistration? Registration, DiagnosticInfo? Diagnostic)
{
    public static RegistrationResult Ok(ServiceRegistration registration) => new(registration, null);
    public static RegistrationResult Error(DiagnosticInfo diagnostic) => new(null, diagnostic);
}
```

- [ ] **Step 6: Add the descriptors.** Create `Diagnostics/DiagnosticDescriptors.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string Category = "JoakimAnder.Toolbox.DependencyInjection";

    public static readonly DiagnosticDescriptor NotAssignable = new(
        "TBX1001", "Service type not assignable from implementation",
        "'{0}' does not implement or inherit the service type '{1}'", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotConstructible = new(
        "TBX1002", "Marked type cannot be used as an implementation",
        "'{0}' is abstract or static and cannot be registered as a service implementation", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidGroup = new(
        "TBX1003", "Group name is not a valid identifier",
        "Group '{0}' on '{1}' is not a valid C# identifier and cannot be used in a method name", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingServiceCollection = new(
        "TBX1004", "IServiceCollection is not referenced",
        "Attributed services were found but Microsoft.Extensions.DependencyInjection is not referenced; no registration method was generated", Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
```

- [ ] **Step 7: Rewrite the parser with validation.** Replace `Parser.cs` entirely:

```csharp
using System.Collections.Immutable;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

internal static class Parser
{
    public static EquatableArray<RegistrationResult> Parse(GeneratorAttributeSyntaxContext context, Lifetime lifetime)
    {
        var implementation = (INamedTypeSymbol)context.TargetSymbol;
        var implName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var builder = ImmutableArray.CreateBuilder<RegistrationResult>(context.Attributes.Length);
        foreach (var attribute in context.Attributes)
        {
            var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax());

            if (implementation.IsAbstract || implementation.IsStatic)
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.NotConstructible, location,
                    new EquatableArray<string>(ImmutableArray.Create(implName)))));
                continue;
            }

            INamedTypeSymbol? serviceSymbol = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol service)
            {
                serviceSymbol = service;
            }

            if (serviceSymbol is not null && !IsAssignable(implementation, serviceSymbol))
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.NotAssignable, location,
                    new EquatableArray<string>(ImmutableArray.Create(
                        implName, serviceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))));
                continue;
            }

            var group = NamedString(attribute, "Group");
            if (group is not null && !SyntaxFacts.IsValidIdentifier(group))
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidGroup, location,
                    new EquatableArray<string>(ImmutableArray.Create(group, implName)))));
                continue;
            }

            builder.Add(RegistrationResult.Ok(new ServiceRegistration(
                implName,
                serviceSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                lifetime,
                group,
                NamedString(attribute, "Key"))));
        }

        return new EquatableArray<RegistrationResult>(builder.ToImmutable());
    }

    private static bool IsAssignable(INamedTypeSymbol implementation, INamedTypeSymbol service)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, service)) return true;
        foreach (var iface in implementation.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(iface, service)) return true;
        for (var baseType = implementation.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (SymbolEqualityComparer.Default.Equals(baseType, service)) return true;
        return false;
    }

    private static string? NamedString(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
            if (arg.Key == name && arg.Value.Value is string s && s.Length > 0)
                return s;
        return null;
    }
}
```

- [ ] **Step 8: Update the pipeline** to split diagnostics from registrations and add the `IServiceCollection`-presence check. Replace `DependencyInjectionGenerator.cs`:

```csharp
using System.Collections.Generic;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

[Generator(LanguageNames.CSharp)]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(AttributeSource.HintName, AttributeSource.Source));

        var singletons = Discover(context, AttributeSource.SingletonMetadataName, Lifetime.Singleton, "Singletons");
        var scoped = Discover(context, AttributeSource.ScopedMetadataName, Lifetime.Scoped, "Scoped");
        var transient = Discover(context, AttributeSource.TransientMetadataName, Lifetime.Transient, "Transient");

        var hasServiceCollection = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") is not null);

        var all = singletons.Collect().Combine(scoped.Collect()).Combine(transient.Collect())
            .Combine(hasServiceCollection);

        context.RegisterSourceOutput(all, static (spc, data) =>
        {
            var results = data.Left;
            var hasDi = data.Right;

            var registrations = new List<ServiceRegistration>();
            foreach (var arr in results.Left.Left) Collect(arr, spc, registrations);
            foreach (var arr in results.Left.Right) Collect(arr, spc, registrations);
            foreach (var arr in results.Right) Collect(arr, spc, registrations);

            if (registrations.Count == 0) return;

            if (!hasDi)
            {
                spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingServiceCollection, location: null));
                return;
            }

            spc.AddSource(RegistrationEmitter.HintName, RegistrationEmitter.Emit(registrations));
        });
    }

    private static void Collect(EquatableArray<RegistrationResult> results, SourceProductionContext spc, List<ServiceRegistration> sink)
    {
        foreach (var result in results.AsImmutableArray())
        {
            if (result.Diagnostic is { } diagnostic)
                spc.ReportDiagnostic(diagnostic.ToDiagnostic());
            else if (result.Registration is { } registration)
                sink.Add(registration);
        }
    }

    private static IncrementalValuesProvider<EquatableArray<RegistrationResult>> Discover(
        IncrementalGeneratorInitializationContext context, string metadataName, Lifetime lifetime, string trackingName) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName(
                metadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => Parser.Parse(ctx, lifetime))
            .WithTrackingName(trackingName);
}
```

- [ ] **Step 9: Run tests.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests`. Expected: PASS (all suites — confirm Task 2/3 tests still green after the `RegistrationResult` change).

- [ ] **Step 10: Commit.**

```bash
git add -A
git commit -m "feat(di): report TBX1001-TBX1004 diagnostics and skip invalid registrations"
```

```json:metadata
{"files": ["src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/LocationInfo.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/DiagnosticInfo.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Model/RegistrationResult.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Diagnostics/DiagnosticDescriptors.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/Parser.cs", "src/JoakimAnder.Toolbox.SourceGenerators/DependencyInjection/DependencyInjectionGenerator.cs", "tests/JoakimAnder.Toolbox.SourceGenerators.Tests/DiagnosticTests.cs"], "verifyCommand": "dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests", "acceptanceCriteria": ["TBX1001 on non-assignable type", "TBX1002 on abstract/static", "TBX1003 on invalid group", "TBX1004 when DI not referenced", "Valid sibling still registers"]}
```

---

### Task 5: Incremental caching regression test

**Goal:** Prove the generator caches correctly — an unrelated source edit must not re-run the discovery transforms.

**Files:**
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/CachingTests.cs`

**Acceptance Criteria:**
- [ ] After running the driver over a compilation and then over a copy with an unrelated method added, every output of the `Singletons`, `Scoped`, and `Transient` tracked steps reports `Cached` or `Unchanged`.

**Verify:** `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter CachingTests` → pass.

**Steps:**

- [ ] **Step 1: Write the test.** Create `CachingTests.cs`:

```csharp
using System.Linq;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class CachingTests
{
    [Fact]
    public void Unrelated_edit_does_not_rerun_discovery()
    {
        const string source = """
            namespace App
            {
                using JoakimAnder.Toolbox.DependencyInjection;
                interface IClock { }
                [Singleton(typeof(IClock))] class Clock : IClock { }
                [Scoped] class Cache { }
            }
            """;

        var first = GeneratorTestHelper.CreateCompilation(source);
        // An unrelated addition that touches no attributed type.
        var second = GeneratorTestHelper.CreateCompilation(source + "\nnamespace App { class Unrelated { void M() { } } }");

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DependencyInjectionGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(first);
        driver = driver.RunGenerators(second);

        var steps = driver.GetRunResult().Results.Single().TrackedSteps;

        foreach (var name in new[] { "Singletons", "Scoped", "Transient" })
        {
            Assert.True(steps.ContainsKey(name), $"missing tracked step '{name}'");
            Assert.All(steps[name].SelectMany(s => s.Outputs), output =>
                Assert.True(
                    output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"step '{name}' reran with reason {output.Reason}"));
        }
    }
}
```

- [ ] **Step 2: Run the test.** Run: `dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter CachingTests`. Expected: PASS. If it fails with `Modified`/`New`, a non-equatable value leaked into the model — re-check that `Parser` returns only scalars/`EquatableArray`.

- [ ] **Step 3: Commit.**

```bash
git add -A
git commit -m "test(di): assert incremental caching survives unrelated edits"
```

```json:metadata
{"files": ["tests/JoakimAnder.Toolbox.SourceGenerators.Tests/CachingTests.cs"], "verifyCommand": "dotnet test tests/JoakimAnder.Toolbox.SourceGenerators.Tests --filter CachingTests", "acceptanceCriteria": ["Discovery steps report Cached/Unchanged after an unrelated edit"]}
```

---

### Task 6: Runnable example and documentation

**Goal:** A working example in the examples app plus README and CHANGELOG updates.

**Files:**
- Modify: `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj`
- Modify: `examples/JoakimAnder.Toolbox.Examples/Program.cs`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

**Acceptance Criteria:**
- [ ] The example registers attributed services via `AddAttributedServices()`, builds a provider, resolves a service, and prints a line proving it resolved — including one grouped and one keyed registration.
- [ ] `dotnet run --project examples/JoakimAnder.Toolbox.Examples` runs the DI example without error.
- [ ] README has a DI section under "What's in the box"; CHANGELOG has an entry.

**Verify:** `dotnet run --project examples/JoakimAnder.Toolbox.Examples` → prints the DI demo output with no exception.

**Steps:**

- [ ] **Step 1: Reference DI + the analyzer in the example project.** In `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj`, add a package reference and an analyzer reference to the generator (analyzers do not flow transitively through a normal ProjectReference):

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox.SourceGenerators\JoakimAnder.Toolbox.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
```

- [ ] **Step 2: Read the current example.** Run: `cat examples/JoakimAnder.Toolbox.Examples/Program.cs` to see the existing FanOut demo and match its style.

- [ ] **Step 3: Append a DI demo** to `Program.cs`. Add the using and a demo block (adapt placement to the file's existing structure):

```csharp
using JoakimAnder.Toolbox.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

// --- DI attributes + source generator demo ---
var services = new ServiceCollection()
    .AddAttributedServices()
    .AddAttributedWebServices()
    .BuildServiceProvider();

Console.WriteLine($"IClock -> {services.GetRequiredService<IClock>().GetType().Name}");
Console.WriteLine($"keyed 'redis' ICache -> {services.GetRequiredKeyedService<ICache>("redis").GetType().Name}");
Console.WriteLine($"web IGreeter -> {services.GetRequiredService<IGreeter>().GetType().Name}");

interface IClock { }
[Singleton(typeof(IClock))] class SystemClock : IClock { }

interface ICache { }
[Singleton(typeof(ICache), Key = "redis")] class RedisCache : ICache { }

interface IGreeter { }
[Scoped(typeof(IGreeter), Group = "Web")] class Greeter : IGreeter { }
```

- [ ] **Step 4: Run the example.** Run: `dotnet run --project examples/JoakimAnder.Toolbox.Examples`. Expected: prints `IClock -> SystemClock`, the keyed cache line, and the web greeter line, with no exception.

- [ ] **Step 5: Update the README.** In `README.md`, under "What's in the box", add a section mirroring the FanOut entry's style:

```markdown
### DI registration — attributes + source generator

Mark a class with `[Singleton]`, `[Scoped]`, or `[Transient]` and the generator emits the
`IServiceCollection` registration for you — no reflection, no hand-maintained `AddX` lists.

```csharp
using JoakimAnder.Toolbox.DependencyInjection;

[Singleton(typeof(IClock))] class SystemClock : IClock { }
[Scoped] class OrderService { }

// in Program.cs:
builder.Services.AddAttributedServices();
```

Omit the service type to register the concrete type itself. Use `Group = "..."` to emit a
separate `AddAttributed<Group>Services()` method, and `Key = "..."` for keyed services. The
attributes are generated into your assembly, so there is no extra runtime dependency.
```

- [ ] **Step 6: Update the CHANGELOG.** Run `cat CHANGELOG.md` to match its style, then add an entry (under `## [Unreleased]` / `### Added`, creating the headings if absent):

```markdown
### Added
- DI registration source generator: `[Singleton]`/`[Scoped]`/`[Transient]` attributes that emit grouped `AddAttributed*Services` extension methods, with explicit-or-self service types and keyed-service support.
```

- [ ] **Step 7: Full build + test.** Run: `dotnet build && dotnet test`. Expected: all pass.

- [ ] **Step 8: Commit.**

```bash
git add -A
git commit -m "docs(di): add runnable example, README section, and CHANGELOG entry"
```

```json:metadata
{"files": ["examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj", "examples/JoakimAnder.Toolbox.Examples/Program.cs", "README.md", "CHANGELOG.md"], "verifyCommand": "dotnet run --project examples/JoakimAnder.Toolbox.Examples", "acceptanceCriteria": ["Example registers, resolves, and prints services incl. grouped + keyed", "Example runs without error", "README and CHANGELOG updated"]}
```

---

## Self-review

**Spec coverage:** Attributes injected via post-init (Task 1) ✓; per-lifetime `[Singleton]`/`[Scoped]`/`[Transient]` (Tasks 1–2) ✓; explicit-or-self service type (Task 2) ✓; grouped `AddAttributed*Services` (Task 2) ✓; keyed services (Task 3) ✓; emit namespace + `internal static partial class` + `global::` qualification + sorting/determinism (Task 2) ✓; `TBX1001`–`TBX1004` (Task 4) ✓; equatable model + caching discipline + caching test (Tasks 2, 5) ✓; project layout under `DependencyInjection/` + delete `Placeholder.cs` + replace `SmokeTests.cs` (Tasks 1–2) ✓; tests, example, README, CHANGELOG (Tasks 1–6) ✓.

**Type consistency:** `EquatableArray<RegistrationResult>` is the transform output from Task 4 onward (Task 2 used `EquatableArray<ServiceRegistration>`; Task 4 explicitly replaces `Parser.cs` and the pipeline together, and Step 9 re-runs the full suite to catch fallout). `RegistrationEmitter.Emit(IReadOnlyList<ServiceRegistration>)`, `HintName`, and the `Add*`/`AddKeyed*` method names are stable across Tasks 2–6. Tracking names `Singletons`/`Scoped`/`Transient` are defined in Task 2 and consumed in Task 5.

**Placeholder scan:** none — every code step shows complete content.
