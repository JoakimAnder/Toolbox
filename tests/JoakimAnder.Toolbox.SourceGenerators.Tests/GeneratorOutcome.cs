using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

// CA1812 can't see the `new GeneratorOutcome(...)` in GeneratorTestHelper.Run — the analyzer
// doesn't trace positional-record construction reliably across files in this scenario.
#pragma warning disable CA1812
internal sealed record GeneratorOutcome(
    GeneratorDriverRunResult Result,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompileErrors)
{
    public string GeneratedSource(string hintNameContains) =>
        Result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName.Contains(hintNameContains, StringComparison.Ordinal))
            .SourceText.ToString();

    public bool HasGeneratedSource(string hintNameContains) =>
        Result.Results.SelectMany(r => r.GeneratedSources)
            .Any(s => s.HintName.Contains(hintNameContains, StringComparison.Ordinal));
}
#pragma warning restore CA1812
