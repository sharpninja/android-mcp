using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationSourcePlanCase(string Id,Artifact Pins,DocumentationModelSourcePlan Expected,string? Error=null)
{public override string ToString()=>Id;}
public static class DocumentationModelSourceCases
{
    public sealed record Contract(DocumentationContractId Id,string File,string CanonicalPin,string CatalogPath,string Media,string Hash);
    public static readonly Contract[] Contracts=
    [
        new(DocumentationContractId.OpenApi,"openapi.json","../catalog/contracts/openapi.json","contracts/openapi.json","application/json","68481e079ba0689b213369a9f6f92414d160c89a765ac87c5cfb853b452336df"),
        new(DocumentationContractId.ApiContract,"API-CONTRACT.md","../catalog/API-CONTRACT.md","API-CONTRACT.md","text/markdown","16ce15df22afa712af09206548419222520f84ccdf6eaaa4a25db08120015a6b"),
        new(DocumentationContractId.SourceMetadataContract,"SOURCE-METADATA-CONTRACT.md","../catalog/SOURCE-METADATA-CONTRACT.md","SOURCE-METADATA-CONTRACT.md","text/markdown","dfda8892af2ce1fdc0cda4d6d6767592599c5c4c0473fad5a12f6a6b67851854")
    ];
    public static DocumentationModelSourcePlan Expected()
    {
        var baseInputs=new[]{("architecture/workspace.dsl","text/plain"),("architecture/code-diagrams.md","text/markdown"),("architecture/module-contract.json","application/json"),("architecture/render-skin.puml","text/plain"),("toolchain-lock.json","application/json"),("renderer-lock.json","application/json"),("docs/ui-blueprint.md","text/markdown"),("docs/Android MCP Bridge Design.md","text/markdown"),("contracts/source-pins.json","application/json")};
        return new(baseInputs.Select(pair=>new DocumentationModelSourceSpec("project:"+pair.Item1,DocumentationInputRole.ProjectRequired,null,pair.Item1,pair.Item2,null))
            .Concat(Contracts.Select(contract=>new DocumentationModelSourceSpec("snapshot:"+contract.Id,DocumentationInputRole.ContractSnapshotRequired,contract.Id,"contracts/"+contract.File,contract.Media,contract.Hash)))
            .Concat(Contracts.Select(contract=>new DocumentationModelSourceSpec("canonical:"+contract.Id,DocumentationInputRole.CanonicalContractOptional,contract.Id,contract.CatalogPath,contract.Media,contract.Hash))).ToArray());
    }
    public static Artifact Original()
    {
        // Native objects serialize authored input data; neither a production planner nor source reader supplies expected descriptors.
        var root=new JsonObject{["scope"]="Design interfaces only; no source/API surface/dataset",["files"]=new JsonArray(Contracts.Select(contract=>(JsonNode)new JsonObject{["file"]=contract.File,["canonical_relative_path"]=contract.CanonicalPin,["sha256"]=contract.Hash}).ToArray())};
        return new("contracts/source-pins.json",Encoding.UTF8.GetBytes(root.ToJsonString()),"application/json");
    }
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var original=Original();var expected=Expected();
            DocumentationSourcePlanCase Change(string id,Action<JsonObject> mutate)
            {var root=JsonNode.Parse(original.Bytes.Span)!.AsObject();mutate(root);return new(id,original with{Bytes=Encoding.UTF8.GetBytes(root.ToJsonString())},expected,"MODEL_SOURCE_PINS");}
            yield return [new DocumentationSourcePlanCase("SOURCES-EXACT",original,expected)];
            yield return [Change("SOURCES-MISSING-FILES",root=>root.Remove("files"))];
            yield return [Change("SOURCES-FILES-NOT-ARRAY",root=>root["files"]="not-an-array")];
            yield return [Change("SOURCES-MISSING-ENTRY",root=>root["files"]!.AsArray().RemoveAt(0))];
            yield return [Change("SOURCES-DUPLICATE-ENTRY",root=>root["files"]!.AsArray().Add(root["files"]![0]!.DeepClone()))];
            yield return [Change("SOURCES-UNKNOWN-ENTRY",root=>root["files"]!.AsArray().Add(new JsonObject{["file"]="unexpected.md",["canonical_relative_path"]="../catalog/unexpected.md",["sha256"]=Contracts[0].Hash}))];
            for(var index=0;index<Contracts.Length;index++)
            {
                var selected=index;
                yield return [Change("SOURCES-CANONICAL-SUBSTITUTION-"+index,root=>root["files"]![selected]!["canonical_relative_path"]="../catalog/unapproved.md")];
                yield return [Change("SOURCES-FILE-SUBSTITUTION-"+index,root=>root["files"]![selected]!["file"]="../"+Contracts[selected].File)];
                yield return [Change("SOURCES-HASH-MALFORMED-"+index,root=>root["files"]![selected]!["sha256"]="invalid")];
            }
            yield return [Change("SOURCES-ABSOLUTE",root=>root["files"]![0]!["canonical_relative_path"]="/catalog/contracts/openapi.json")];
            yield return [Change("SOURCES-PREFIX-ESCAPE",root=>root["files"]![0]!["canonical_relative_path"]="../catalog/../secrets/openapi.json")];
            yield return [Change("SOURCES-CASE-SUBSTITUTION",root=>root["files"]![0]!["file"]="OpenApi.json")];
            yield return [Change("SOURCES-NULL-HASH",root=>root["files"]![0]!["sha256"]=null)];
            yield return [new DocumentationSourcePlanCase("SOURCES-DUPLICATE-JSON",original with{Bytes=Encoding.UTF8.GetBytes("{\"files\":[],\"files\":[]}")},expected,"MODEL_SOURCE_PINS")];
            yield return [new DocumentationSourcePlanCase("SOURCES-BAD-UTF8",original with{Bytes=new byte[]{0xff,0xfe}},expected,"MODEL_SOURCE_PINS")];
            yield return [new DocumentationSourcePlanCase("SOURCES-WRONG-ARTIFACT",original with{RelativePath="../contracts/source-pins.json"},expected,"MODEL_SOURCE_PINS")];
            yield return [new DocumentationSourcePlanCase("SOURCES-WRONG-MEDIA",original with{MediaType="text/plain"},expected,"MODEL_SOURCE_PINS")];
        }
    }
    public static void AssertPlan(DocumentationSourcePlanCase test,IDocumentationModelSourcePlanner provider)
    {
        if(test.Error is not null){var error=Assert.Throws<DocumentationModelSourceException>(()=>provider.Plan(test.Pins));Assert.Equal(test.Error,error.Code);return;}
        var actual=provider.Plan(test.Pins);Assert.Equal(test.Expected.Sources,actual.Sources);
        Assert.Equal(12,actual.Sources.Count(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional));
        Assert.Equal(3,actual.Sources.Count(source=>source.Role==DocumentationInputRole.CanonicalContractOptional));
        Assert.All(actual.Sources,source=>Assert.True(ArtifactPaths.IsRelative(source.RelativePath)));
    }
    public static IEnumerable<object[]> DescriptorControls=>Enumerable.Range(0,15).SelectMany(index=>new[]{"id","role","contract","path","media","hash"}.Select(memberName=>new object[]{index,memberName}));
}
public sealed class ScriptedDocumentationModelSourcePlanner(DocumentationModelSourcePlan response,string? error=null):IDocumentationModelSourcePlanner
{
    public DocumentationModelSourcePlan Plan(Artifact sourcePins)=>error is null?response:throw new DocumentationModelSourceException("Scripted rejection");
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationModelSourceMockTests
{
    [Theory,MemberData(nameof(DocumentationModelSourceCases.Cases),MemberType=typeof(DocumentationModelSourceCases))]
    public void CorrectSourcePlanAndRejectionMocksPass(DocumentationSourcePlanCase test)=>DocumentationModelSourceCases.AssertPlan(test,new ScriptedDocumentationModelSourcePlanner(test.Expected,test.Error));
    [Theory,MemberData(nameof(DocumentationModelSourceCases.Cases),MemberType=typeof(DocumentationModelSourceCases))]
    public void EveryWrongSourcePlanOutcomeIsRejected(DocumentationSourcePlanCase test)=>Assert.NotNull(Record.Exception(()=>DocumentationModelSourceCases.AssertPlan(test,new ScriptedDocumentationModelSourcePlanner(test.Expected,test.Error is null?"MODEL_SOURCE_PINS":null))));
    [Theory,MemberData(nameof(DocumentationModelSourceCases.DescriptorControls),MemberType=typeof(DocumentationModelSourceCases))]
    public void EveryDescriptorAuthorityFieldHasAnIndividualNegativeControl(int index,string field)
    {
        var test=new DocumentationSourcePlanCase("FIELD",DocumentationModelSourceCases.Original(),DocumentationModelSourceCases.Expected());var wrong=test.Expected.Sources.ToArray();var source=wrong[index];
        wrong[index]=field switch
        {
            "id"=>source with{ArtifactId="unknown"},
            "role"=>source with{Role=(DocumentationInputRole)(((int)source.Role+1)%3)},
            "contract"=>source with{ContractId=source.ContractId==DocumentationContractId.OpenApi?DocumentationContractId.ApiContract:DocumentationContractId.OpenApi},
            "path"=>source with{RelativePath="../unapproved"},
            "media"=>source with{MediaType="application/octet-stream"},
            "hash"=>source with{ExpectedSha256=source.ExpectedSha256 is null?new string('1',64):null},
            _=>throw new InvalidOperationException()
        };
        Assert.NotNull(Record.Exception(()=>DocumentationModelSourceCases.AssertPlan(test,new ScriptedDocumentationModelSourcePlanner(new(wrong)))));
    }
}
