using System.Collections.ObjectModel;

namespace AndroidMcp.BuildTool.Tests.Documentation;

/// <summary>Exact data-fixture carriers only. Current canonical sources never pass through this map.</summary>
public sealed class DocumentationFixtureCarrierMap:IDocumentationFixtureCarrierMap
{
    private static readonly IReadOnlyDictionary<string,string> Paths=new ReadOnlyDictionary<string,string>(new[]
    {
        "fixtures/current-tooling.code-diagrams.md","fixtures/current-tooling.design.md","fixtures/current-tooling.README.md",
        "fixtures/pre-nuke-tooling.code-diagrams.md","fixtures/pre-nuke-tooling.design.md","fixtures/pre-nuke-tooling.README.md",
        "ui-blueprint.pre-health-migration.md"
    }.ToDictionary(path=>path,path=>path+".fixture",StringComparer.Ordinal));
    public string Resolve(string virtualFixturePath)=>Paths.TryGetValue(virtualFixturePath,out var physical)
        ?physical:throw new KeyNotFoundException("No admitted data-fixture carrier: "+virtualFixturePath);
}
