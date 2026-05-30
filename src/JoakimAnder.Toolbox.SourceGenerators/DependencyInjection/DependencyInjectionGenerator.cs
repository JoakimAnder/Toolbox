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
