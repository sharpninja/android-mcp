using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewReceiptCollectorNativeTests
{
    [Theory,MemberData(nameof(ReviewReceiptCollectorCases.Data),MemberType=typeof(ReviewReceiptCollectorCases))]
    public Task Same_original_and_caller_packets_bind_to_shared_services(string name)=>ReceiptCollectorNativeBinding.Run(name,false);
}
public sealed class ReviewReceiptCollectorFileCallerTests
{
    [Theory,MemberData(nameof(ReviewReceiptCollectorCases.Data),MemberType=typeof(ReviewReceiptCollectorCases))]
    public Task Same_packets_bind_to_actual_pinned_local_request_and_native_artifact_store(string name)=>ReceiptCollectorNativeBinding.Run(name,true);
}
internal static class ReceiptCollectorNativeBinding
{
    internal static async Task Run(string name,bool files)
    {
        var fixture=new ReceiptCollectorFixture(name);IArtifactStore inner;
        if(files)
        {
            var root=Path.Combine(Path.GetTempPath(),"receipt-collector-tests",Guid.NewGuid().ToString("N"));
            inner=new FileArtifactStore(root,ReviewFixture.Hash);
            foreach(var item in fixture.Values)await inner.WriteAsync(new(item.Key,item.Value,"application/octet-stream"));
        }
        else{var memory=new ReviewMemoryStore();foreach(var item in fixture.Values)memory.Values[item.Key]=item.Value;inner=memory;}
        var tracked=new CollectorTrackingStore(inner);ReceiptCollectorObservation actual;
        try
        {
            var result=await new ReviewReceiptCollector(new PinnedArtifactReader(tracked),tracked,new CanonicalJson(),new ReviewClock(),new ReviewAssessService(ReviewFixture.Hash)).CollectRequestAsync(fixture.Request);
            var bytes=await inner.ReadAsync(result.Audit.RelativePath);Assert.Equal(result.Audit.Sha256,ReviewFixture.Hash(bytes));
            var body=JsonNode.Parse(bytes.Span)!;var rows=body["rounds"]!;
            Assert.Equal("1970-01-01T00:00:00.0000000+00:00",body["audited_at"]!.GetValue<string>());
            actual=new("collected",Encoding.UTF8.GetString(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(rows))),tracked.Writes.Count,tracked.ForbiddenReads,body["source"] is not null||body["accepted_complete"] is not null,0);
            Assert.Equal(rows.AsArray().Count,result.Rounds);Assert.Contains(fixture.Request,result.Evidence);
        }
        catch(ReviewException error){actual=new(error.Code,"[]",tracked.Writes.Count,tracked.ForbiddenReads,false,0);}
        catch(PinnedArtifactException error){actual=new(error.Code,"[]",tracked.Writes.Count,tracked.ForbiddenReads,false,0);}
        ReviewReceiptCollectorCases.AssertContract(name,actual);
        foreach(var item in fixture.Values.Where(x=>!x.Key.Contains("PROFILE-MERGED",StringComparison.Ordinal)))
            Assert.Equal(item.Value,(await inner.ReadAsync(item.Key)).ToArray());
    }
}
internal sealed class CollectorTrackingStore(IArtifactStore inner):IArtifactStore
{
    internal List<string> Reads {get;}=[];
    internal List<string> Writes {get;}=[];
    internal int ForbiddenReads=>Reads.Count(x=>x.Contains("PROFILE-MERGED",StringComparison.Ordinal));
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken token=default)
    {Reads.Add(relativePath);return inner.ReadAsync(relativePath,token);}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)
    {Writes.Add(artifact.RelativePath);return inner.WriteAsync(artifact,token);}
}
internal sealed class ReceiptCollectorFixture
{
    internal Dictionary<string,byte[]> Values {get;}=new(StringComparer.Ordinal);
    internal StoredArtifact Request {get;}
    internal ReceiptCollectorFixture(string name)
    {
        var rounds=new List<ReviewReceiptRound>();
        if(name is "batch-order" or "batch-failure")rounds.Add(Round("round-002",name=="batch-failure"?"prompt-hash":"complete"));
        rounds.Add(Round("round-001",name));
        var body=JsonSerializer.SerializeToNode(new ReviewReceiptCollectRequest(rounds,"evidence/review-receipt-audit.json"))!.AsObject();
        if(name=="outer-unknown-field")body["unknown"]="not-a-supported-caller-field";
        var path=name=="outer-private-path"?"private/PROFILE-MERGED.md":"collector-request.json";
        Request=Put(path,Encoding.UTF8.GetBytes(body.ToJsonString()),"application/json");
        if(name=="outer-pin-hash")Request=Request with{Sha256=new string('0',64)};
    }
    private StoredArtifact Put(string path,byte[] bytes,string media="application/json")
    {Values[path]=bytes;return new(path,ReviewFixture.Hash(bytes),bytes.Length,media);}
    private StoredArtifact Json(string path,object body)=>Put(path,JsonSerializer.SerializeToUtf8Bytes(body));
    private ReviewReceiptRound Round(string round,string name)
    {
        var directory="reviews/"+round;var logical=name=="invalid-input-path"?"../escape.md":name=="private-input-path"?"private/PROFILE-MERGED.md":"design.md";
        var frozen=Put(directory+"/input/"+(name=="invalid-input-path"?"design.md":logical),Encoding.UTF8.GetBytes("exact synthetic evidence 雪\n"),"text/plain");
        var row=new JsonObject{[name=="file-path-fallback"?"path":"file"]=logical,["sha256"]=name=="input-hash"?new string('0',64):frozen.Sha256};
        var manifest=new JsonObject{["files"]=new JsonArray(row)};
        if(name=="duplicate-manifest-input")manifest["files"]!.AsArray().Add(row.DeepClone());
        var manifestPin=Put(directory+"/input-manifest.json",Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        const string prompt="FULL exact prompt 雪\nwith preserved blank lines\n\n";
        var request=JsonSerializer.Serialize(new{prompt,prompt_sha256=name=="prompt-hash"?new string('0',64):ReviewFixture.Hash(Encoding.UTF8.GetBytes(prompt)),
            manifest_sha256=name=="manifest-hash"?new string('0',64):manifestPin.Sha256,requested_model="historical-requested-model",requested_effort="historical-requested-effort"});
        if(name=="invalid-request")request="invalid-request-json";
        if(name=="first-request-line")request+="\nthis later line is ignored by the original caller";
        var requestPin=Put(directory+"/request.jsonl",Encoding.UTF8.GetBytes(request+"\n"),"application/x-ndjson");
        if(name=="changed-independent-pin")requestPin=requestPin with{Sha256=new string('0',64)};
        var events=new List<string>();
        if(name=="first-init")events.Add(JsonSerializer.Serialize(new{type="system",subtype="init",model="first-recorded-model",tools=new[]{"FirstTool"}}));
        events.Add(JsonSerializer.Serialize(new{type="system",subtype="init",model="recorded-model",tools=new[]{"ReadOnly"}}));
        events.Add(JsonSerializer.Serialize(new{type="assistant",message=new{content=new[]{new{type=name=="hidden"?"thinking":"text",text="synthetic visible fragment"}}}}));
        if(name=="invalid-visible")events.Add("invalid-visible-json");
        var visible=Put(directory+"/response.visible.jsonl",Encoding.UTF8.GetBytes(string.Join('\n',events)+"\n"),"application/x-ndjson");
        StoredArtifact? completion=null;
        if(name!="unfinished")completion=name=="empty-completion"?Json(directory+"/completion.json",new{}):Json(directory+"/completion.json",new{visible_response_sha256=name=="response-hash"?new string('0',64):visible.Sha256,exit_code=name=="nonzero-exit"?7:0});
        var reports=new List<StoredArtifact>();
        if(name!="unfinished")
        {
            if(name=="derived-priority")reports.Add(Json(directory+"/review-report.reassembled.json",new{report_complete=true}));
            var reportPath=directory+"/"+(name=="derived-fallback"?"review-report.reassembled.json":"review-report.json");
            reports.Add(name=="invalid-derived"?Put(reportPath,Encoding.UTF8.GetBytes("invalid-derived-json")):Json(reportPath,new{report_complete=name!="derived-priority"}));
        }
        if(name=="missing-artifact")Values.Remove(manifestPin.RelativePath);
        return new(directory,requestPin,manifestPin,visible,[new(logical,frozen)],completion,reports);
    }
}
