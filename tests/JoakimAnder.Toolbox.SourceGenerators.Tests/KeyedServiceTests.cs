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
