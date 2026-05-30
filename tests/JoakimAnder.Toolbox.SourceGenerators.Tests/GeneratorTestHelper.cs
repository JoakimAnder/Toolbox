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
