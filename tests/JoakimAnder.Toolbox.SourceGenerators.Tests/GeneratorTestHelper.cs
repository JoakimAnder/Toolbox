using System;
using System.Collections.Generic;
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

        var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        var runResult = updatedDriver.GetRunResult();
        return new GeneratorOutcome(runResult, runResult.Diagnostics, compileErrors);
    }

    public static CSharpCompilation CreateCompilation(string source, IEnumerable<MetadataReference>? extraReferences = null)
    {
        var coreDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        MetadataReference[] coreReferences =
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(coreDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(coreDir, "netstandard.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(coreDir, "System.Collections.dll")),
        };

        var diReference = MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location);

        var references = extraReferences is null
            ? coreReferences.Append(diReference)
            : coreReferences.Concat(extraReferences);

        return CSharpCompilation.Create(
            assemblyName: "Tests.Generated",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
