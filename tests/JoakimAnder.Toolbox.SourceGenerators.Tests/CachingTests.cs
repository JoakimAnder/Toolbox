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
                [Transient] class Worker { }
            }
            """;

        var first = GeneratorTestHelper.CreateCompilation(source);
        // An unrelated addition that touches no attributed type.
        var second = GeneratorTestHelper.CreateCompilation(source + "\nnamespace App { class Unrelated { void M() { } } }");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
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
