using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class ParsedProjectionEnvironment
{
    public DocumentationInput Input {get;}
    public DocumentationModel Model {get;}
    public DocumentationProjection Expected {get;}=DocumentationFixture.Current.Correct;
    public byte[] BlueprintBefore {get;}
    public byte[] DesignBefore {get;}
    public RecordingParsedProjectionReader Reader {get;}
    public ParsedProjectionEnvironment(bool opaqueBlueprint=false)
    {
        var f=DocumentationFixture.Current;
        Input=new(f.Input.BlueprintMarkdown with{Bytes=opaqueBlueprint?new byte[]{0}:f.Input.BlueprintMarkdown.Bytes.ToArray()},f.Input.DesignMarkdown with{Bytes=f.Input.DesignMarkdown.Bytes.ToArray()});
        BlueprintBefore=Input.BlueprintMarkdown.Bytes.ToArray();DesignBefore=Input.DesignMarkdown.Bytes.ToArray();
        Model=DocumentationModelReadCases.Correct(new("PARSED",Input.BlueprintMarkdown,f.Blueprint));Reader=new(this);
    }
}
public sealed class RecordingParsedProjectionReader(ParsedProjectionEnvironment e):IDocumentationModelReader
{
    public int Calls {get;private set;}
    public DocumentationModel Read(AndroidMcp.BuildTool.Core.Artifact input){Calls++;Assert.Same(e.Input.BlueprintMarkdown,input);return e.Model;}
}
public sealed class FixedParsedProjectionReader(DocumentationModel model):IDocumentationModelReader
{
    public DocumentationModel Read(AndroidMcp.BuildTool.Core.Artifact input)=>model;
}
public static class ParsedProjectionAssertions
{
    public static void AssertContract(ParsedProjectionEnvironment e,IParsedDocumentationProjector provider)
    {
        var result=provider.Project(e.Input,e.Model);
        foreach(var assertion in DocumentationProjectionAssertions.Create())assertion.Assert(result);
        Assert.Equal(0,e.Reader.Calls);Assert.Equal(e.BlueprintBefore,e.Input.BlueprintMarkdown.Bytes.ToArray());Assert.Equal(e.DesignBefore,e.Input.DesignMarkdown.Bytes.ToArray());
        DocumentationModelReadCases.AssertContract(new("PARSED-SHARED",e.Input.BlueprintMarkdown,DocumentationFixture.Current.Blueprint),new FixedParsedProjectionReader(e.Model));
    }
}
public sealed class ScriptedParsedDocumentationProjector(ParsedProjectionEnvironment e,string mutation=""):IParsedDocumentationProjector
{
    public DocumentationProjection Project(DocumentationInput input){_ =e.Reader.Read(input.BlueprintMarkdown);return e.Expected;}
    public DocumentationProjection Project(DocumentationInput input,DocumentationModel model)
    {
        Assert.Same(e.Input,input);Assert.Same(e.Model,model);
        if(mutation=="outcome")throw new InvalidOperationException("Wrong parsed projection outcome");
        if(mutation=="legacy-entrypoint")return Project(input);
        if(mutation=="extra-parse")_ =e.Reader.Read(input.BlueprintMarkdown);
        if(mutation=="model-mutation")model.Blueprint["schema_version"]="wrong";
        if(mutation=="model-index-mutation")((Dictionary<string,JsonObject>)model.ScreensById).Remove(model.ScreensById.Keys.First());
        if(mutation is "blueprint-bytes" or "design-bytes")
        {
            var artifact=mutation=="blueprint-bytes"?input.BlueprintMarkdown:input.DesignMarkdown;
            Assert.True(MemoryMarshal.TryGetArray(artifact.Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;
        }
        return e.Expected;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationParsedProjectionMockTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CorrectParsedMocksRetainEveryExistingProjectionAssertionWithoutASecondParse(bool opaqueBlueprint)
    {var e=new ParsedProjectionEnvironment(opaqueBlueprint);ParsedProjectionAssertions.AssertContract(e,new ScriptedParsedDocumentationProjector(e));}
    [Fact]
    public void EveryOriginalWrongProjectionRemainsRejectedThroughTheParsedBoundary()
    {
        var e=new ParsedProjectionEnvironment();
        foreach(var assertion in DocumentationProjectionAssertions.Create())
        {
            var wrong=assertion.IncorrectResponse(e.Expected);
            var failure=Assert.Throws<DocumentationContractException>(()=>assertion.Assert(new FixedParsedProjectionProvider(wrong).Project(e.Input,e.Model)));
            Assert.StartsWith(assertion.Id+":",failure.Message,StringComparison.Ordinal);
        }
    }
    [Theory]
    [InlineData("outcome")][InlineData("legacy-entrypoint")][InlineData("extra-parse")][InlineData("model-mutation")][InlineData("model-index-mutation")][InlineData("blueprint-bytes")][InlineData("design-bytes")]
    public void EveryParsingAuthorityAndSourceMutationBypassIsRejected(string mutation)
    {var e=new ParsedProjectionEnvironment();Assert.NotNull(Record.Exception(()=>ParsedProjectionAssertions.AssertContract(e,new ScriptedParsedDocumentationProjector(e,mutation))));}
    private sealed class FixedParsedProjectionProvider(DocumentationProjection result):IParsedDocumentationProjector
    {
        public DocumentationProjection Project(DocumentationInput input)=>result;
        public DocumentationProjection Project(DocumentationInput input,DocumentationModel model)=>result;
    }
}
