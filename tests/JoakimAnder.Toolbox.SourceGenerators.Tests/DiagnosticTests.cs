using System.Linq;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;
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
    public void Abstract_class_with_repeated_same_lifetime_attribute_reports_TBX1002_once()
    {
        // The structural check is per-type, not per-attribute — repeating the same
        // [Singleton(Key=...)] on a class would otherwise produce N identical errors.
        var outcome = Run("[Singleton(Key = \"a\")] [Singleton(Key = \"b\")] abstract class Base { }");
        Assert.Single(outcome.GeneratorDiagnostics.Where(d => d.Id == "TBX1002"));
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
        const string source = "namespace App { using JoakimAnder.Toolbox.DependencyInjection; [Scoped] class A { } }";
        var compilation = GeneratorTestHelper.CreateCompilation(source, extraReferences: System.Array.Empty<MetadataReference>());
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DependencyInjection.DependencyInjectionGenerator().AsSourceGenerator() });
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "TBX1004");
    }
}
