using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// Staged until the SAME independently mocked predicates have a green gate.
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyCore024MethodRealTests
{
    private static readonly Lazy<Task<Core024Evidence>> Evidence=new(ReadCurrent);
    private static async Task<Core024Evidence> ReadCurrent()
    {
        var workspace=DocumentationFixture.Current.Workspace;
        var design=new FileArtifactStore(Path.Combine(workspace,"remediation/design-c4"),value=>ContentHash.Sha256(value.Span));
        var catalog=new FileArtifactStore(Path.Combine(workspace,"remediation/catalog"),value=>ContentHash.Sha256(value.Span));
        async Task<string> Text(IArtifactStore store,string path)=>Encoding.UTF8.GetString((await store.ReadAsync(path)).Span);
        async Task<JsonObject> Json(IArtifactStore store,string path)=>JsonNode.Parse((await store.ReadAsync(path)).Span)!.AsObject();
        return new(await Text(design,"docs/Android MCP Bridge Design.md"),await Text(design,"architecture/code-diagrams.md"),
            await Json(catalog,"contracts/openapi.json"),await Json(design,"docs/ui-asset-manifest.json"),await Json(design,"contracts/source-pins.json"),
            (await catalog.ReadAsync("SOURCE-METADATA-CONTRACT.md")).ToArray(),(await design.ReadAsync("contracts/SOURCE-METADATA-CONTRACT.md")).ToArray(),
            await Json(design,"contracts/ScreenStateResult.schema.json"),await Text(design,"docs/screens/states/SCR09.S0.svg"));
    }
    [Theory,MemberData(nameof(LegacyCore024MethodMockTests.Methods),MemberType=typeof(LegacyCore024MethodMockTests))]
    public async Task SameOriginalEightMethodsReadCurrentCanonicalFiles(string method)=>LegacyCore024Methods.AssertOriginal(method,await Evidence.Value);
}
