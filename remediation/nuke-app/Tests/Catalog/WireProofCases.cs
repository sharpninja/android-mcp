using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record WireCall(string Operation="current",object? ExpectedRevision=null,string State="unknown",
    JsonObject? ArgumentCodec=null,JsonObject? ResponseCodec=null,JsonObject? Source=null,string? PinnedHash=null,
    string ContractId=WireProofCases.Contract);
public sealed record WireReply(string Outcome,long Revision=0,string? State=null,string? Record=null,string? RecordHash=null,
    bool? SelectedProofUnchanged=null,int RevisionRows=0,string BaseState="unknown",bool ForeignKeysClean=true);
public sealed record WireCase(string Id,WireCall[] Calls,WireReply[] Expected);
public interface IWireCaseProvider:IDisposable { WireReply Execute(WireCall call); }

public static class WireProofCases
{
    public const string Binding="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string Schema="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string Contract="cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    // Literal sorted compact contract body, independent of the native serializer.
    public static string Body(long revision=0,string state="unknown",string codec="{}",string source="{}")=>
        "{\"argument_codec\":"+codec+",\"binding_id\":\""+Binding+"\",\"contract_id\":\""+Contract+
        "\",\"descriptor\":\"a.I\",\"flags\":0,\"interface_hash\":null,\"interface_version\":null,\"oneway\":0,\"proof_source\":"+
        source+",\"proof_state\":\""+state+"\",\"response_codec\":"+codec+",\"revision\":"+revision+
        ",\"schema_hash\":\""+Schema+"\",\"transaction_code\":0}";
    public static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string CodecText="{\"codec_id\":\"fixture-codec-v1\",\"schema_hash\":\""+Schema+"\"}";
    public const string EvidenceText="{\"evidence\":[\"fixture-source\"]}";
    public static WireCall Verified(object? expected=default)=>new("append",expected ?? 0L,"verified",
        JsonNode.Parse(CodecText)!.AsObject(),JsonNode.Parse(CodecText)!.AsObject(),JsonNode.Parse(EvidenceText)!.AsObject());
    public static WireReply Row(long revision=0,string state="unknown",string codec="{}",string source="{}",bool? selected=null)=>
        new("ok",revision,state,Body(revision,state,codec,source),Hash(Body(revision,state,codec,source)),selected,checked((int)revision));
    public static WireReply Error(string outcome,int rows=0)=>new(outcome,RevisionRows:rows);
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<WireCase> Generate()
    {
        var initial=Row();var verified=Row(1,"verified",CodecText,EvidenceText);
        var verifiedHash=verified.RecordHash!;
        const string revoke="{\"reason\":\"fixture ABI proof revoked\"}";
        yield return new("WIRE-01-initial-immutable-unknown",[new()],[initial]);
        yield return new("WIRE-02-verify-then-revoke",[Verified(),new("recheck",PinnedHash:verifiedHash),
            new("append",1L,"unsupported",new(),new(),JsonNode.Parse(revoke)!.AsObject()),new("recheck",PinnedHash:verifiedHash)],
            [verified,verified with{SelectedProofUnchanged=true},Row(2,"unsupported",source:revoke),Row(2,"unsupported",source:revoke,selected:false)]);
        yield return new("WIRE-03-stale-cas-rollback",[Verified(),new("append",0L,ArgumentCodec:new(),ResponseCodec:new(),Source:new()),new()],
            [verified,Error("revision-changed",1),verified]);
        yield return new("WIRE-04-empty-verified-codec-rollback",[Verified(),new("append",1L,"verified",new(),new(),JsonNode.Parse(EvidenceText)!.AsObject()),new()],
            [verified,Error("constraint",1),verified]);
        var wrong=JsonNode.Parse(CodecText)!.AsObject();wrong["schema_hash"]=new string('d',64);
        yield return new("WIRE-05-codec-schema-pin-rollback",[new("append",0L,"verified",wrong,wrong,JsonNode.Parse(EvidenceText)!.AsObject()),new()],
            [Error("constraint"),initial]);
        yield return new("WIRE-06-no-evidence-rollback",[Verified() with{Source=new JsonObject{["evidence"]=new JsonArray()}},new()],
            [Error("constraint"),initial]);
        yield return new("WIRE-07-proof-enum-closed",[new("append",0L,"invented",new(),new(),new()),new()],[Error("constraint"),initial]);
        yield return new("WIRE-08-recheck-must-be-verified",[new("recheck",PinnedHash:initial.RecordHash)],[initial with{SelectedProofUnchanged=false}]);
        yield return new("WIRE-09-recheck-exact-current-hash",[Verified(),new("recheck",PinnedHash:new string('d',64))],
            [verified,verified with{SelectedProofUnchanged=false}]);
        yield return new("WIRE-10-unknown-contract",[new(ContractId:new string('d',64))],[Error("unknown-identity")]);
        foreach(var (suffix,value) in new (string,object?)[]{("negative",-1L),("limit",9007199254740991L),("boolean",true),("float",0.0),("string","0"),("null",null)})
            yield return new("WIRE-11-expected-revision-"+suffix,[new("append",value,ArgumentCodec:new(),ResponseCodec:new(),Source:new()),new()],
                [Error("invalid-expected-revision"),initial]);
        yield return new("WIRE-12-transaction-owned",[new("begin-fixture-transaction"),Verified(),new("rollback-fixture-transaction"),new()],
            [new("ok"),Error("transaction-owned"),new("ok"),initial]);
    }
    public static void AssertCase(WireCase test,IWireCaseProvider provider)
    {
        Assert.Equal(test.Calls.Length,test.Expected.Length);
        for(var index=0;index<test.Calls.Length;index++) Assert.Equal(test.Expected[index],provider.Execute(test.Calls[index]));
    }
}
public sealed class ScriptedWireProvider(WireCase expected,int corruptAt=-1,int field=0):IWireCaseProvider
{
    private int index;
    public WireReply Execute(WireCall call)
    {
        Assert.Same(expected.Calls[index],call);var position=index++;var row=expected.Expected[position];
        if(position!=corruptAt) return row;
        return field switch
        {
            0=>row with{Outcome="wrong"},1=>row with{Revision=row.Revision+1},2=>row with{State="wrong"},
            3=>row with{Record="wrong"},4=>row with{RecordHash="wrong"},5=>row with{SelectedProofUnchanged=row.SelectedProofUnchanged is null ? true : !row.SelectedProofUnchanged},
            6=>row with{RevisionRows=row.RevisionRows+1},7=>row with{BaseState="wrong"},_=>row with{ForeignKeysClean=!row.ForeignKeysClean}
        };
    }
    public void AssertConsumed()=>Assert.Equal(expected.Calls.Length,index);
    public void Dispose() { }
}
[Trait("Gate","Mock")]
public sealed class WireProofMockTests
{
    [Theory,MemberData(nameof(WireProofCases.All),MemberType=typeof(WireProofCases))]
    public void CorrectMockResponsesPass(WireCase test)
    { using var provider=new ScriptedWireProvider(test);WireProofCases.AssertCase(test,provider);provider.AssertConsumed(); }
    [Theory,MemberData(nameof(WireProofCases.All),MemberType=typeof(WireProofCases))]
    public void EveryReplyFieldAtEveryPositionIsDiscriminating(WireCase test)
    {
        for(var position=0;position<test.Calls.Length;position++)
        for(var field=0;field<9;field++)
        { using var provider=new ScriptedWireProvider(test,position,field);Assert.NotNull(Record.Exception(()=>WireProofCases.AssertCase(test,provider))); }
    }
}
