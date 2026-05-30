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
