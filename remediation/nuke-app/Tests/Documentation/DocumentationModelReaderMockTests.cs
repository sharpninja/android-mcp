using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationModelReadCase(string Id,Artifact Input,JsonObject Expected,Type? ErrorType=null,string? ErrorMessage=null)
{public override string ToString()=>Id;}
public static class DocumentationModelReadCases
{
    public static IEnumerable<object[]> Data
    {
        get
        {
            var fixture=DocumentationFixture.Current;var original=fixture.Input.BlueprintMarkdown with{Bytes=fixture.Input.BlueprintMarkdown.Bytes.ToArray()};
            var text=Encoding.UTF8.GetString(original.Bytes.Span);var expected=(JsonObject)fixture.Blueprint.DeepClone();
            Artifact Text(string value)=>original with{Bytes=Encoding.UTF8.GetBytes(value)};
            Artifact Rewrite(Action<JsonObject> mutate){var json=(JsonObject)expected.DeepClone();mutate(json);return Text(Regex.Replace(text,"```json ui-blueprint-v2\\r?\\n[\\s\\S]*?\\r?\\n```",_=>"```json ui-blueprint-v2\n"+json.ToJsonString()+"\n```"));}
            return new DocumentationModelReadCase[]
            {
                new("MODEL-CANONICAL",original,expected),
                new("MODEL-CRLF",Text(text.Replace("\n","\r\n",StringComparison.Ordinal)),expected),
                new("MODEL-SURROUNDING-MARKDOWN",Text("# Before\n"+text+"\n# After\n"),expected),
                new("MODEL-MISSING-BLOCK",Text("# No canonical block\n"),expected,typeof(InvalidDataException),"Exactly one canonical ui-blueprint-v2 block is required"),
                new("MODEL-DUPLICATE-BLOCK",Text(text+"\n"+text),expected,typeof(InvalidDataException),"Exactly one canonical ui-blueprint-v2 block is required"),
                new("MODEL-JSON-ERROR",Text("```json ui-blueprint-v2\n{\n```"),expected,typeof(JsonException)),
                new("MODEL-SCHEMA-ERROR",Rewrite(node=>node["schema_version"]="9.0.0"),expected,typeof(InvalidDataException),"Unsupported UI blueprint schema"),
                new("MODEL-DUPLICATE-SCREEN",Rewrite(node=>node["screens"]!.AsArray().Add(node["screens"]![0]!.DeepClone())),expected,typeof(ArgumentException))
            }.Select(test=>new object[]{test});
        }
    }
    public static DocumentationModel Correct(DocumentationModelReadCase test)
    {
        var json=(JsonObject)test.Expected.DeepClone();var screens=json["screens"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
        return new(json,screens,json["storyboards"]!.AsArray().Select(n=>n!.AsObject()).ToArray(),screens.ToDictionary(s=>s["id"]!.GetValue<string>(),StringComparer.Ordinal));
    }
    public static void AssertContract(DocumentationModelReadCase test,IDocumentationModelReader provider)
    {
        var before=test.Input.Bytes.ToArray();DocumentationModel? model=null;var error=Record.Exception(()=>model=provider.Read(test.Input));
        Assert.Equal(before,test.Input.Bytes.ToArray());
        if(test.ErrorType is not null){Assert.NotNull(error);Assert.IsAssignableFrom(test.ErrorType,error);if(test.ErrorMessage is not null)Assert.Equal(test.ErrorMessage,error.Message);Assert.Null(model);return;}
        Assert.Null(error);Assert.NotNull(model);Assert.True(JsonNode.DeepEquals(test.Expected,model.Blueprint));
        Assert.Equal(11,model.Screens.Count);Assert.Equal(6,model.Storyboards.Count);Assert.Equal(88,model.Screens.Sum(s=>s["controls"]!.AsObject().Count));
        Assert.Equal(76,model.Screens.Sum(s=>s["states"]!.AsObject().Count));Assert.Equal(107,model.Storyboards.Sum(s=>s["steps"]!.AsArray().Count));
        Assert.Equal(model.Screens.Select(s=>s["id"]!.GetValue<string>()).Order(StringComparer.Ordinal),model.ScreensById.Keys.Order(StringComparer.Ordinal));
        for(var index=0;index<model.Screens.Count;index++){Assert.Same(model.Blueprint["screens"]![index],model.Screens[index]);Assert.Same(model.Screens[index],model.ScreensById[model.Screens[index]["id"]!.GetValue<string>()]);}
        for(var index=0;index<model.Storyboards.Count;index++)Assert.Same(model.Blueprint["storyboards"]![index],model.Storyboards[index]);
    }
}
public sealed class ScriptedDocumentationModelReader(DocumentationModelReadCase test,string mutation=""):IDocumentationModelReader
{
    public DocumentationModel Read(Artifact input)
    {
        Assert.Same(test.Input,input);
        if(mutation=="input-bytes"){Assert.True(MemoryMarshal.TryGetArray(input.Bytes,out var segment));segment.Array![segment.Offset]^=1;}
        if(test.ErrorType is not null)throw mutation=="outcome"?new InvalidOperationException("Wrong failure"):(Exception)Activator.CreateInstance(test.ErrorType,mutation=="error-message"?"Wrong message":test.ErrorMessage??"Scripted invalid JSON")!;
        if(mutation=="outcome")throw new InvalidOperationException("Wrong failure");
        var result=DocumentationModelReadCases.Correct(test);
        if(mutation=="blueprint")result.Blueprint["schema_version"]="wrong";
        if(mutation=="screens")result=result with{Screens=result.Screens.Skip(1).ToArray()};
        if(mutation=="stories")result=result with{Storyboards=result.Storyboards.Skip(1).ToArray()};
        if(mutation=="screen-copy")result=result with{Screens=result.Screens.Select(s=>(JsonObject)s.DeepClone()).ToArray()};
        if(mutation=="story-copy")result=result with{Storyboards=result.Storyboards.Select(s=>(JsonObject)s.DeepClone()).ToArray()};
        if(mutation=="index"){var values=result.ScreensById.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);values.Remove(values.Keys.First());result=result with{ScreensById=values};}
        if(mutation=="index-copy")result=result with{ScreensById=result.ScreensById.ToDictionary(p=>p.Key,p=>(JsonObject)p.Value.DeepClone(),StringComparer.Ordinal)};
        return result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationModelReaderMockTests
{
    [Theory,MemberData(nameof(DocumentationModelReadCases.Data),MemberType=typeof(DocumentationModelReadCases))]
    public void CorrectCanonicalModelReaderMocksPass(DocumentationModelReadCase test)=>DocumentationModelReadCases.AssertContract(test,new ScriptedDocumentationModelReader(test));
    [Theory,MemberData(nameof(DocumentationModelReadCases.Data),MemberType=typeof(DocumentationModelReadCases))]
    public void EachWrongReaderOutcomeIsRejected(DocumentationModelReadCase test)=>Assert.NotNull(Record.Exception(()=>DocumentationModelReadCases.AssertContract(test,new ScriptedDocumentationModelReader(test,"outcome"))));
    [Theory]
    [InlineData("blueprint")][InlineData("screens")][InlineData("stories")][InlineData("screen-copy")][InlineData("story-copy")][InlineData("index")][InlineData("index-copy")]
    public void EachAuthorityDriftIsIndividuallyRejected(string mutation){var test=(DocumentationModelReadCase)DocumentationModelReadCases.Data.First()[0];Assert.NotNull(Record.Exception(()=>DocumentationModelReadCases.AssertContract(test,new ScriptedDocumentationModelReader(test,mutation))));}
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void SourceMutationIsRejectedOnBothOutcomes(bool failure){var test=DocumentationModelReadCases.Data.Select(row=>(DocumentationModelReadCase)row[0]).First(test=>(test.ErrorType is not null)==failure);Assert.NotNull(Record.Exception(()=>DocumentationModelReadCases.AssertContract(test,new ScriptedDocumentationModelReader(test,"input-bytes"))));}
    [Fact]
    public void WrongSpecificFailureMessageIsRejected(){var test=DocumentationModelReadCases.Data.Select(row=>(DocumentationModelReadCase)row[0]).First(test=>test.ErrorMessage is not null);Assert.NotNull(Record.Exception(()=>DocumentationModelReadCases.AssertContract(test,new ScriptedDocumentationModelReader(test,"error-message"))));}
}
