using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign024MethodRealTests
{
    private static readonly Lazy<Task<Design024Evidence>> Current=new(ReadCurrent);
    private static async Task<Design024Evidence> ReadCurrent()
    {
        var root=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
        var source=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        async Task<string> Text(string path)=>Encoding.UTF8.GetString((await source.ReadAsync(path)).Span);
        return new(await Text("architecture/code-diagrams.md"),await Text("architecture/workspace.dsl"),
            JsonNode.Parse((await source.ReadAsync("architecture/module-contract.json")).Span)!.AsObject());
    }
    [Theory,MemberData(nameof(LegacyDesign024MethodMockTests.Methods),MemberType=typeof(LegacyDesign024MethodMockTests))]
    public async Task SameSevenOriginalPredicatesReadCurrentCanonicalSources(string method)=>LegacyDesign024Methods.AssertOriginal(method,await Current.Value);
}
