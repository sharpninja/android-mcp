using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign020MethodRealTests
{
    private static readonly Lazy<Task<OriginalDesignTextEvidence>> Current=new(ReadCurrent);
    private static async Task<OriginalDesignTextEvidence> ReadCurrent()
    {
        var source=new FileArtifactStore(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4"),value=>ContentHash.Sha256(value.Span));
        async Task<string> Text(string path)=>Encoding.UTF8.GetString((await source.ReadAsync(path)).Span);
        var manifest=await source.ReadAsync("docs/ui-asset-manifest.json");
        return new(await Text("docs/Android MCP Bridge Design.md"),await Text("architecture/code-diagrams.md"),await Text("architecture/workspace.dsl"),
            new([new Artifact("docs/ui-asset-manifest.json",manifest,"application/json")]));
    }
    [Theory,MemberData(nameof(LegacyDesign020MethodMockTests.Methods),MemberType=typeof(LegacyDesign020MethodMockTests))]
    public async Task SameOriginalEightMethodsReadCurrentCanonicalFiles(string method)=>LegacyDesign020Methods.AssertOriginal(method,await Current.Value);
}
