using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>One extraction authority for the existing DSL native views and code-image references.</summary>
public sealed class ArchitectureViewIndexReader : IArchitectureViewIndexReader
{
    private static readonly Regex NativeView=new(@"(?m)^\s*(?:(?:systemContext|container|component|dynamic)\s+\S+|deployment\s+\S+\s+(?:""[^""]*""|\S+))\s+([A-Za-z_][A-Za-z0-9_]*)\s+""[^""]*""\s*\{",RegexOptions.CultureInvariant);
    private static readonly Regex ImageView=new(@"(?m)^\s*image\s+\.\./docs/diagrams/([a-z-]+)\.svg\s*$",RegexOptions.CultureInvariant);
    public ArchitectureViewIndex Read(Artifact workspaceDsl)
    {
        var text=Encoding.UTF8.GetString(workspaceDsl.Bytes.Span);
        var native=NativeView.Matches(text).Select(match=>match.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();
        var images=ImageView.Matches(text).Select(match=>"docs/diagrams/"+match.Groups[1].Value+".svg")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(Array.AsReadOnly(native),Array.AsReadOnly(images));
    }
}
