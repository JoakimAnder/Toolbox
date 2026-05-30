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

    [Fact]
    public void No_extensions_file_emitted_when_nothing_is_attributed()
    {
        var outcome = GeneratorTestHelper.Run("// empty");
        Assert.False(outcome.HasGeneratedSource("AttributedServices.g.cs"));
    }
}
