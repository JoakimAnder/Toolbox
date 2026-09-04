using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class AttributeEmissionTests
{
    [Fact]
    public void Emits_the_three_marker_attributes()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        var src = outcome.GeneratedSource("Attributes.g.cs");

        Assert.Contains("internal sealed class SingletonAttribute", src, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ScopedAttribute", src, StringComparison.Ordinal);
        Assert.Contains("internal sealed class TransientAttribute", src, StringComparison.Ordinal);
        Assert.Contains("AllowMultiple = true", src, StringComparison.Ordinal);
        Assert.Contains("Inherited = false", src, StringComparison.Ordinal);
        Assert.Contains("ServiceType { get; }", src, StringComparison.Ordinal);
        Assert.Contains("string? Group { get; set; }", src, StringComparison.Ordinal);
        Assert.Contains("string? Key { get; set; }", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Emitted_attributes_carry_doc_comments()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        var src = outcome.GeneratedSource("Attributes.g.cs");

        Assert.Contains("/// <summary>", src, StringComparison.Ordinal);
        Assert.Contains("/// <param name=\"serviceType\">", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_attributes_compile_clean()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        Assert.Empty(outcome.CompileErrors);
    }

    [Fact]
    public void No_extensions_file_emitted_when_nothing_is_attributed()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        Assert.False(outcome.HasGeneratedSource("AttributedServices.g.cs"));
    }
}
