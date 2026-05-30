using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// Cache-safe stand-in for Location (which holds an editor-changing source span).
// All members are equatable value types; a real Location is rebuilt at report time.
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(SyntaxNode? node)
    {
        if (node is null) return null;
        var location = node.GetLocation();
        if (location.SourceTree is null) return null;
        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}
