using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkReviewMetadataObservation(string Code,JsonNode? Payload,IReadOnlyList<string> Loads,
    int Opens,int Disposals,int Processes,int Reads,int Writes,IReadOnlyList<string> PropertyNames);

public sealed class SdkReviewMetadataFixture
{
    public static readonly string[] Names=["legacy-wire","populated-wire","source-mixed","source-empty","source-malformed",
        "source-non-test","full-inventory","inventory-mismatch","empty-inventory","duplicate-inventory","missing-host",
        "missing-reference","scope-inventory","property-mixed","property-empty","property-error","child-forward",
        "child-inventory-mismatch","child-extra-result","facts-forward"];
    public string Name{get;}
    public SdkSourceEvaluationFixture Source{get;}=new("source-graph");
    public SdkChildFixture Child{get;}=new("native-source-graph");
    public string Extra=>"/fixture/app/Unreferenced/Extra.csproj";
    public string Host=>Source.Request.Run.HostProject;
    public string Tests=>Source.Request.Run.TestsProject;
    public string Library=>"/fixture/app/Library/Library.csproj";
    public string[] Complete=>[Host,Tests,Extra,Library];
    public string[] Graph=>[Host,Tests,Library];
    public SdkReviewMetadataFixture(string name){Name=name;}
    public bool IsSource=>Name.StartsWith("source-",StringComparison.Ordinal)||Name.Contains("inventory",StringComparison.Ordinal)&&!Name.StartsWith("child-",StringComparison.Ordinal)||Name=="missing-host"||Name=="missing-reference";
    public bool IsChild=>Name.StartsWith("child-",StringComparison.Ordinal);
    public string Code=>Name switch
    {
        "inventory-mismatch" or "empty-inventory" or "duplicate-inventory" or "missing-host" or "missing-reference" or "child-inventory-mismatch" or "child-extra-result"=>"SOURCE_INPUTS_UNRESOLVED",
        "scope-inventory"=>"INPUT_SCOPE_VIOLATION","property-error"=>"SDK_API_UNSUPPORTED",_=>"OK"
    };
    public string[]? Inventory=>Name switch
    {
        "full-inventory" or "inventory-mismatch"=>Complete,
        "empty-inventory"=>[],"duplicate-inventory"=>[Host,Tests,Library,Library],
        "missing-host"=>[Tests,Library],"missing-reference"=>[Host,Tests],
        "scope-inventory"=>[Host,Tests,"/private/unapproved.csproj",Library],
        _=>null
    };
    public string[] ExpectedLoads=>Name switch
    {
        "full-inventory"=>Complete,
        "source-mixed" or "source-empty" or "source-malformed" or "source-non-test" or "missing-reference"=>Graph,
        _=>[]
    };
    public (string IsTestProject,string Kind) MetadataFor(string path)=>path==Tests?Name switch
    {
        "source-empty"=>("",""),"source-malformed"=>("not-a-bool","unknown"),"source-non-test"=>("false","none"),_=>("true","mixed")
    }:("false","none");
    public JsonObject Metadata(string path)
    {
        var data=MetadataFor(path);
        return new(){["IsTestProject"]=data.IsTestProject,["TestKind"]=data.Kind};
    }
    public JsonObject SourcePayload(bool extra=false)
    {
        var result=JsonSerializer.SerializeToNode(Source.Expected)!.AsObject();
        foreach(var project in result["Projects"]!.AsArray())project!["TestMetadata"]=Metadata(project["ProjectPath"]!.GetValue<string>());
        if(extra)
            result["Projects"]!.AsArray().Insert(2,JsonSerializer.SerializeToNode(new
            {
                ProjectPath=Extra,TargetPath="/fixture/app/Unreferenced/Extra.dll",
                Inputs=new[]{new{Kind="Project",AbsolutePath=Extra,DefiningProject=Extra}},
                Imports=Array.Empty<string>(),ProjectReferences=Array.Empty<string>(),Issues=Array.Empty<string>(),
                ResolvedCopies=Array.Empty<object>(),TestMetadata=new{IsTestProject="false",TestKind="none"}
            }));
        return result;
    }
    public JsonObject WirePayload(bool populated)
    {
        var result=new JsonObject
        {
            ["Configuration"]=JsonSerializer.SerializeToNode(Source.Configuration),
            ["Request"]=JsonSerializer.SerializeToNode(Source.Request),
            ["Native"]=JsonSerializer.SerializeToNode(Source.Projects[Host]),
            ["Evaluation"]=JsonSerializer.SerializeToNode(Source.Expected)
        };
        // This is the pinned old shape, independent of the new optional serializers.
        result["Configuration"]!.AsObject().Remove("ProjectInventory");result["Request"]!.AsObject().Remove("ProjectInventory");
        result["Native"]!.AsObject().Remove("TestMetadata");
        foreach(var project in result["Evaluation"]!["Projects"]!.AsArray())project!.AsObject().Remove("TestMetadata");
        if(populated)
        {
            result["Configuration"]!["ProjectInventory"]=JsonSerializer.SerializeToNode(Graph);
            result["Request"]!["ProjectInventory"]=JsonSerializer.SerializeToNode(Graph);
            result["Native"]!["TestMetadata"]=Metadata(Host);
            foreach(var project in result["Evaluation"]!["Projects"]!.AsArray())project!["TestMetadata"]=Metadata(project["ProjectPath"]!.GetValue<string>());
        }
        return result;
    }
    public JsonObject ChildRequestPayload()
    {
        var result=JsonSerializer.SerializeToNode(Child.Request)!.AsObject();
        result["Native"]!["Evaluation"]!["ProjectInventory"]=JsonSerializer.SerializeToNode(Graph);
        return result;
    }
    public JsonNode? ExpectedPayload=>Name switch
    {
        "legacy-wire"=>WirePayload(false),"populated-wire"=>WirePayload(true),
        "property-mixed"=>new JsonObject{["IsTestProject"]="true",["TestKind"]="mixed"},
        "property-empty"=>null,
        "child-forward"=>new JsonObject{["Request"]=ChildRequestPayload(),["Evaluation"]=SourcePayload()},
        "child-extra-result"=>new JsonObject{["Request"]=ChildRequestPayload()},
        "facts-forward"=>new JsonObject{["ProjectInventory"]=JsonSerializer.SerializeToNode(new[]{Host,Tests})},
        _ when IsSource&&Code=="OK"=>SourcePayload(Name=="full-inventory"),_=>null
    };
    public SdkReviewMetadataObservation Expected=>new(Code,ExpectedPayload,ExpectedLoads,
        IsSource&&(Code=="OK"||Name=="missing-reference")?1:0,
        IsSource&&(Code=="OK"||Name=="missing-reference")?1:0,
        Name is "child-forward" or "child-extra-result"?1:0,
        Name is "child-forward" or "child-extra-result"?4:0,
        Name is "child-forward" or "child-extra-result"?1:0,
        Name=="property-error"?["IsTestProject"]:Name.StartsWith("property-",StringComparison.Ordinal)?["IsTestProject","AndroidMcpTestKind"]:[]);
}

public static class SdkReviewMetadataCases
{
    public static IEnumerable<object[]> Data=>SdkReviewMetadataFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(SdkReviewMetadataFixture fixture,SdkReviewMetadataObservation actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
}
public sealed class SdkReviewMetadataMockTests
{
    [Theory,MemberData(nameof(SdkReviewMetadataCases.Data),MemberType=typeof(SdkReviewMetadataCases))]
    public void Correct_packet_and_individual_metadata_wire_inventory_and_effect_controls(string name)
    {
        var fixture=new SdkReviewMetadataFixture(name);
        var response=Scripted(fixture);
        SdkReviewMetadataCases.AssertContract(fixture,response);
        var actual=JsonSerializer.SerializeToNode(response)!;
        foreach(var member in ReviewOfficialTransportCases.Leaves(actual).ToArray())
        {
            var wrong=actual.DeepClone();ReviewOfficialTransportCases.Mutate(wrong,member.Path);
            Assert.ThrowsAny<Exception>(()=>Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
                new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(wrong))));
        }
        var extra=actual.DeepClone();extra["UnexpectedAuthority"]=true;
        Assert.ThrowsAny<Exception>(()=>Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
            new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(extra))));
    }
    private static SdkReviewMetadataObservation Scripted(SdkReviewMetadataFixture f)
    {
        var loads=new List<string>();var opens=0;var disposed=0;var processes=0;var reads=0;var writes=0;var properties=new List<string>();
        if(f.IsSource&&(f.Code=="OK"||f.Name=="missing-reference")){opens++;loads.AddRange(f.Name=="full-inventory"?f.Complete:f.Graph);disposed++;}
        if(f.Name is "child-forward" or "child-extra-result"){reads=4;writes++;processes++;}
        if(f.Name.StartsWith("property-",StringComparison.Ordinal)){properties.Add("IsTestProject");if(f.Name!="property-error")properties.Add("AndroidMcpTestKind");}
        return new(f.Code,f.ExpectedPayload,loads,opens,disposed,processes,reads,writes,properties);
    }
}
