using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Emit;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

[Generator(LanguageNames.CSharp)]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(AttributeSource.HintName, AttributeSource.Source));
    }
}
