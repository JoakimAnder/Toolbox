using System.Collections.Generic;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;
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

        var hasServiceCollection = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") is not null);

        var all = singletons.Collect().Combine(scoped.Collect()).Combine(transient.Collect())
            .Combine(hasServiceCollection);

        context.RegisterSourceOutput(all, static (spc, data) =>
        {
            var results = data.Left;
            var hasDi = data.Right;

            var registrations = new List<ServiceRegistration>();
            // Dedup across providers — TBX1002 (abstract/static) is per-class, so the same
            // type's diagnostic fires once per FAWMN provider it appears in. DiagnosticInfo
            // value-equality covers it because TBX1002's LocationInfo points at the class
            // declaration (the same syntax node from each provider's perspective).
            var reported = new HashSet<DiagnosticInfo>();
            foreach (var arr in results.Left.Left) Collect(arr, spc, registrations, reported);
            foreach (var arr in results.Left.Right) Collect(arr, spc, registrations, reported);
            foreach (var arr in results.Right) Collect(arr, spc, registrations, reported);

            if (registrations.Count == 0) return;

            if (!hasDi)
            {
                spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingServiceCollection, location: null));
                return;
            }

            spc.AddSource(RegistrationEmitter.HintName, RegistrationEmitter.Emit(registrations));
        });
    }

    private static void Collect(
        EquatableArray<RegistrationResult> results,
        SourceProductionContext spc,
        List<ServiceRegistration> sink,
        HashSet<DiagnosticInfo> reported)
    {
        foreach (var result in results.AsImmutableArray())
        {
            if (result.Diagnostic is { } diagnostic)
            {
                if (reported.Add(diagnostic))
                    spc.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
            else if (result.Registration is { } registration)
                sink.Add(registration);
        }
    }

    private static IncrementalValuesProvider<EquatableArray<RegistrationResult>> Discover(
        IncrementalGeneratorInitializationContext context, string metadataName, Lifetime lifetime, string trackingName) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName(
                metadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => Parser.Parse(ctx, lifetime))
            .WithTrackingName(trackingName);
}
