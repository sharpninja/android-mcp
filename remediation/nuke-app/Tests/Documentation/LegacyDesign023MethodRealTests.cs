using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign023MethodRealTests
{
    private static async Task<Design023Evidence> Read()
    {
        var store=new FileArtifactStore(DocumentationFixture.Current.Workspace,value=>ContentHash.Sha256(value.Span));
        async Task<string> Text(string path)=>Encoding.UTF8.GetString((await store.ReadAsync(path)).Span);
        async Task<JsonObject> Json(string path)=>JsonNode.Parse((await store.ReadAsync(path)).Span)!.AsObject();
        const string root="remediation/design-c4/";
        return new(await Text(root+"docs/Android MCP Bridge Design.md"),await Json("remediation/catalog/contracts/openapi.json"),
            await Json(root+"architecture/arc-upstream-facts.json"),await Text("remediation/catalog/API-CONTRACT.md"),
            await Text("remediation/nuke-planning/docs/fixtures/gradle-original/build.gradle.kts.fixture"),
            await Json(root+"architecture/native-identities.json"),await Text(root+"architecture/code-diagrams.md"),await Text(root+"architecture/workspace.dsl"));
    }
    [Theory,MemberData(nameof(LegacyDesign023MethodMockTests.Methods),MemberType=typeof(LegacyDesign023MethodMockTests))]
    public async Task SameNineOriginalPredicatesBindNativeStoresWithRetainedGradleSyntaxInput(string method)
        =>LegacyDesign023Methods.AssertOriginal(method,await Read());
}
