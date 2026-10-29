using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record MetadataCall(string Operation,JsonObject? Document=null,string Key="fg:location",bool Owner=true,
    string? ETag=null,string? IfNoneMatch="*",string Reason="fixture owner mapping",string Reader="reader-A",
    bool TrustedImport=true,string? Resolution=null,string? ExpectedBase=null);
public sealed record MetadataReply(int Status,long Generation=0,int RevisionRows=0,int Resources=0,int Revision=0,
    string? State=null,string? Origin=null,string? BaseHash=null,string? ContentHash=null,string? ETag=null,string? Document=null,string? Reason=null);
public sealed record MetadataCrudCase(string Id,MetadataCall[] Calls,MetadataReply[] Expected);
public interface IMetadataCaseProvider:IDisposable { MetadataReply Execute(MetadataCall call); }

public static class MetadataCrudCases
{
    // Independent canonical fixture text from the original group() input. Expected
    // hashes use standard SHA256 over this literal, never a production serializer.
    public const string BaseDocument="{\"dependencies\":[],\"description\":\"Fixture group, no OS invocation\",\"estimated_tokens\":0,\"estimator_version\":null,\"evidence\":[],\"group_id\":\"fg:location\",\"labels\":[\"location\"],\"members\":[],\"required_type_ids\":[],\"schema_version\":\"1.0.0\",\"title\":\"Location\",\"unknown_reasons\":[\"No source membership established\"]}";
    public static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Variant(string title="Location",string description="Fixture group, no OS invocation")=>
        BaseDocument.Replace("\"title\":\"Location\"","\"title\":\""+title+"\"",StringComparison.Ordinal)
            .Replace("\"description\":\"Fixture group, no OS invocation\"","\"description\":\""+description+"\"",StringComparison.Ordinal);
    public static JsonObject Group(string title="Location",string description="Fixture group, no OS invocation")=>JsonNode.Parse(Variant(title,description))!.AsObject();
    public static string Tag(long generation,int revision,string state="active",string? baseHash=null,string? document=null)=>
        $"\"meta1:{generation}:functional-group:{Hash("fg:location")}:{baseHash ?? "local"}:{revision}:{state}:{Hash(document ?? BaseDocument)}\"";
    public static MetadataReply Row(long generation=1,int revision=1,string state="active",string origin="local",string? baseHash=null,
        string? document=null,string reason="fixture owner mapping")=>new(200,generation,revision,1,revision,state,origin,baseHash,Hash(document ?? BaseDocument),
            Tag(generation,revision,state,baseHash,document),document ?? BaseDocument,reason);
    public static MetadataReply Error(int status,long generation=0,int rows=0,int resources=0)=>new(status,generation,rows,resources);
    public static MetadataCall Create()=>new("create",Group());
    public static IEnumerable<object[]> All=>Generate().Select(x=>new object[]{x});
    public static IEnumerable<MetadataCrudCase> Generate()
    {
        yield return new("META-01-owner-before-preconditions",[Create() with{Owner=false,IfNoneMatch=null},new("get")],[Error(403),Error(404)]);
        yield return new("META-02-create-header-order",[Create() with{IfNoneMatch=null},Create() with{IfNoneMatch="bad"},Create(),Create()],
            [Error(428),Error(400),Row(),Error(412,1,1,1)]);
        var updated=Variant(description:"new local mapping");
        yield return new("META-03-cas-and-cursor",[Create(),new("capture-cursor"),new("put",Group(description:"new local mapping"),ETag:Tag(1,1),Reason:"fixture update"),new("put",Group(),ETag:Tag(1,1),Reason:"stale"),new("continue-cursor"),new("get")],
            [Row(),new(200,1,1,1),Row(2,2,document:updated,reason:"fixture update"),Error(412,2,2,1),Error(410,2,2,1),Row(2,2,document:updated,reason:"fixture update")]);
        yield return new("META-04-reader-isolation-and-retry",[Create(),new("capture-cursor"),new("continue-cursor",Reader:"reader-B"),new("continue-cursor"),new("continue-cursor")],
            [Row(),new(200,1,1,1),Error(410,1,1,1),new(200,1,1,1),new(200,1,1,1)]);
        yield return new("META-05-local-tombstone-idempotence-restore",[Create(),new("delete",ETag:Tag(1,1),Reason:"fixture delete"),new("delete",ETag:Tag(2,2,"deleted"),Reason:"fixture retry"),new("put",Group(description:"new local mapping"),ETag:Tag(2,2,"deleted"),Reason:"fixture restore")],
            [Row(),Row(2,2,"deleted",reason:"fixture delete"),Row(2,2,"deleted",reason:"fixture delete"),Row(3,3,document:updated,reason:"fixture restore")]);
        var baseHash=Hash(BaseDocument);
        yield return new("META-06-import-suppression-and-immutable-base",[new("import",Group(),Reason:"trusted immutable fixture import"),new("delete",ETag:Tag(1,1,baseHash:baseHash),Reason:"fixture suppress"),new("delete-base-sql"),new("get")],
            [Row(origin:"imported",baseHash:baseHash,reason:"trusted immutable fixture import"),Row(2,2,"suppressed","overlay",baseHash,reason:"fixture suppress"),Error(409,2,2,1),Row(2,2,"suppressed","overlay",baseHash,reason:"fixture suppress")]);
        var edit=Variant("Local");var next=Variant("Next source");var nextHash=Hash(next);
        yield return new("META-07-import-conflict-explicit-rebase",[new("import",Group(),Reason:"trusted immutable fixture import"),new("put",Group("Local"),ETag:Tag(1,1,baseHash:baseHash),Reason:"fixture overlay"),new("import",Group("Next source"),Reason:"trusted immutable fixture import"),new("put",Group("Local"),ETag:Tag(3,3,"conflict",nextHash,edit),Reason:"no rebase"),new("put",Group("Local"),ETag:Tag(3,3,"conflict",nextHash,edit),Reason:"fixture rebase",Resolution:"rebase",ExpectedBase:nextHash)],
            [Row(origin:"imported",baseHash:baseHash,reason:"trusted immutable fixture import"),Row(2,2,origin:"overlay",baseHash:baseHash,document:edit,reason:"fixture overlay"),Row(3,3,"conflict","overlay",nextHash,edit,"trusted immutable fixture import"),Error(409,3,3,1),Row(4,4,origin:"overlay",baseHash:nextHash,document:edit,reason:"fixture rebase")]);
        var invalid=Group();invalid["authority_granted"]=true;
        yield return new("META-08-schema-rollback",[Create(),new("put",invalid,ETag:Tag(1,1),Reason:"invalid"),new("get")],[Row(),Error(422,1,1,1),Row()]);
        yield return new("META-09-trusted-import-only",[new("import",Group(),TrustedImport:false),new("get")],[Error(403),Error(404)]);
        yield return new("META-10-empty-reason-rollback",[Create() with{Reason="   "},new("get")],[Error(422),Error(404)]);
        var dependency=Group();dependency["dependencies"]=new JsonArray(new JsonObject{["group_id"]="fg:absent",["revision"]=1,["content_hash"]=new string('a',64)});
        yield return new("META-11-missing-group-pin-no-partial-row",[new("create",dependency),new("get")],[Error(422),Error(404)]);
        var member=Group();member["members"]=new JsonArray(new JsonObject{["binding_id"]=new string('a',64),["contract_hash"]=new string('b',64),["lifecycle_unit_id"]=null,["role"]="operation"});
        yield return new("META-12-missing-trusted-catalog-reference",[new("create",member),new("get")],[Error(422),Error(404)]);
        yield return new("META-13-strong-etag-order",[new("delete",ETag:null),new("delete",ETag:"*"),new("delete",ETag:Tag(1,1))],[Error(428),Error(400),Error(404)]);
    }
    public static void AssertCase(MetadataCrudCase test,IMetadataCaseProvider provider)
    {
        Assert.Equal(test.Calls.Length,test.Expected.Length);
        for(var index=0;index<test.Calls.Length;index++) Assert.Equal(test.Expected[index],provider.Execute(test.Calls[index]));
    }
}
public sealed class ScriptedMetadataProvider(MetadataCrudCase expected,int corruptAt=-1,int field=0):IMetadataCaseProvider
{
    private int index;
    public MetadataReply Execute(MetadataCall call)
    {
        Assert.Same(expected.Calls[index],call);var row=expected.Expected[index];var position=index++;
        if(position!=corruptAt) return row;
        return field switch
        {
            0=>row with{Status=row.Status+1},1=>row with{Generation=row.Generation+1},2=>row with{RevisionRows=row.RevisionRows+1},
            3=>row with{Resources=row.Resources+1},4=>row with{Revision=row.Revision+1},5=>row with{State="wrong"},6=>row with{Origin="wrong"},
            7=>row with{BaseHash="wrong"},8=>row with{ContentHash="wrong"},9=>row with{ETag="wrong"},10=>row with{Document="wrong"},_=>row with{Reason="wrong"}
        };
    }
    public void AssertConsumed()=>Assert.Equal(expected.Calls.Length,index);
    public void Dispose() { }
}
[Trait("Gate","Mock")]
public sealed class MetadataCrudMockTests
{
    [Theory,MemberData(nameof(MetadataCrudCases.All),MemberType=typeof(MetadataCrudCases))]
    public void CorrectMocksPass(MetadataCrudCase test)
    { using var fake=new ScriptedMetadataProvider(test);MetadataCrudCases.AssertCase(test,fake);fake.AssertConsumed(); }
    [Theory,MemberData(nameof(MetadataCrudCases.All),MemberType=typeof(MetadataCrudCases))]
    public void EveryFieldAtEveryStepIsDiscriminating(MetadataCrudCase test)
    {
        for(var position=0;position<test.Calls.Length;position++)
        for(var field=0;field<12;field++)
        { using var fake=new ScriptedMetadataProvider(test,position,field);Assert.NotNull(Record.Exception(()=>MetadataCrudCases.AssertCase(test,fake))); }
    }
}
