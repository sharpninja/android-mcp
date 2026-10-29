using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign022MethodRealTests
{
    private static readonly Lazy<Task<Design022Evidence>> Current=new(ReadCurrent);
    private static async Task<Design022Evidence> ReadCurrent()
    {
        var root=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
        var source=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        async Task<Artifact> Read(string path,string media)=>new(path,await source.ReadAsync(path),media);
        async Task<string> Text(string path)=>Encoding.UTF8.GetString((await source.ReadAsync(path)).Span);
        var design=await Read(DocumentationAssetPaths.Design,"text/markdown");
        var blueprint=await Read(DocumentationAssetPaths.Blueprint,"text/markdown");
        var projection=new DocumentationProjector(new DocumentationModelReader()).Project(new(blueprint,design));
        return new(Encoding.UTF8.GetString(design.Bytes.Span),await Text("architecture/code-diagrams.md"),await Text("architecture/workspace.dsl"),
            projection,LegacyDesign022DeckParser.Read(DocumentationFixture.Content(projection,"docs/storyboards.html")));
    }
    [Theory,MemberData(nameof(LegacyDesign022MethodMockTests.Methods),MemberType=typeof(LegacyDesign022MethodMockTests))]
    public async Task SameSevenOriginalPredicatesReadCurrentSourcesAndNativeGeneratedOutput(string method)=>LegacyDesign022Methods.AssertOriginal(method,await Current.Value);
}

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign022DeckParserRealTests
{
    [Theory,MemberData(nameof(LegacyDeckParserCases.Cases),MemberType=typeof(LegacyDeckParserCases))]
    public void SameOriginalCallbackCasesReadNativeHtml(LegacyDeckParserCase test)=>LegacyDeckParserCases.AssertObservation(test,LegacyDesign022DeckParser.Read);
}
