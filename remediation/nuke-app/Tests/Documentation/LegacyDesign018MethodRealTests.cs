using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign018MethodRealTests
{
    // Native bindings read current canonical text. Historical fixture text is
    // the independent mock oracle, never a substitute for current source here.
    private static readonly Lazy<Task<OriginalDesignTextEvidence>> Current=new(ReadCurrentAsync);
    private static async Task<OriginalDesignTextEvidence> ReadCurrentAsync()
    {
        var root=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
        var source=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        async Task<string> Read(string path)
        {var bytes=await source.ReadAsync(path);return Encoding.UTF8.GetString(bytes.Span);}
        return new(await Read("docs/Android MCP Bridge Design.md"),await Read("architecture/code-diagrams.md"),
            await Read("architecture/workspace.dsl"),DocumentationFixture.Current.Correct);
    }
    [Theory,MemberData(nameof(LegacyDesign018MethodMockTests.Methods),MemberType=typeof(LegacyDesign018MethodMockTests))]
    public async Task SameOriginalPredicatesReadCurrentCanonicalSources(string method)=>LegacyDesign018Methods.AssertOriginal(method,await Current.Value);
}
