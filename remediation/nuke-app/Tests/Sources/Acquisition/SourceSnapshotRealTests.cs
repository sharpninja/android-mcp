using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceSnapshotProbe:ISourceSnapshotProbe
{
    public async Task<SourceSnapshotObservation> RunAsync(SourceSnapshotCase test)
    {
        var store=new CountingStore();
        if(test.Change is "existing-first" or "existing-all")store.Inner.Files["raw/first"]=new("raw/first",SourceSnapshotCases.First,"application/octet-stream");
        if(test.Change=="corrupt-first")store.Inner.Files["raw/first"]=new("raw/first",new byte[]{0},"application/octet-stream");
        if(test.Change=="existing-all")store.Inner.Files["raw/second"]=new("raw/second",SourceSnapshotCases.Second,"application/octet-stream");
        var original=store.Inner.Files.ToDictionary(item=>item.Key,item=>item.Value.Bytes.ToArray());
        var transport=new FixtureTransport(test);var clock=new ObservedDelay();var batches=0;
        var planner=new CountingPlanner(new RehydrationPlanner(new CanonicalJson()),test.Change=="projection");
        ISourceBatch Create(SnapshotCallerMode mode)
        {
            batches++;var wire=mode==SnapshotCallerMode.Fetch?new SnapshotFetchTransport(transport):(IByteTransport)transport;
            return new SourceBatch(wire,store,new MemoryAcquisitionStore(),new ScriptedLeaseProvider(),new CanonicalJson(),mode==SnapshotCallerMode.Fetch?1d:0d,clock);
        }
        using var cancellation=new CancellationTokenSource();if(test.Change=="cancel")cancellation.Cancel();
        SourceSnapshotResult? result=null;string? error=null;
        try{result=await new SourcesSnapshotOperation(planner,store,Create).RunAsync(new(Recipe(test),
            test.Mode=="Invalid"?(SnapshotCallerMode)999:Enum.Parse<SnapshotCallerMode>(test.Mode),test.Download),cancellation.Token);}
        catch(TransportException problem){error=problem.Code;}
        catch(OperationCanceledException){error="CANCELLED";}
        var output=store.Inner.Files.Where(item=>item.Key.StartsWith("raw/",StringComparison.Ordinal)).ToDictionary(item=>item.Key,item=>item.Value.Bytes.ToArray());
        Assert.Equal(test.Mode=="Fetch"&&test.Download&&test.Error is null?test.Downloaded:0,clock.Calls);
        return new(error,result?.ExitCode,result?.Counts.DownloadedRows,result?.Counts.BlobGroups,result?.Counts.Existing,result?.Counts.MissingBefore,
            result?.Counts.Downloaded,batches,transport.Urls.Count,store.Writes,planner.Calls,
            original.All(item=>store.Inner.Files[item.Key].Bytes.Span.SequenceEqual(item.Value)),result?.MissingPaths??[],transport.Urls,output,
            store.Inner.Files.ContainsKey("sources/snapshot.json"));
    }
    private static RehydrationPlanRequest Recipe(SourceSnapshotCase test)
    {
        JsonObject Row(string path,byte[] bytes,string url,string? status)
        {
            var result=new JsonObject{["path"]=path,["local_path"]=path,["url"]=url,["revision"]=new string('a',40),["sha256"]=ContentHash.Sha256(bytes),["git_blob"]=ContentHash.GitBlobSha1Legacy(bytes),["size_bytes"]=bytes.Length,["transport"]=new JsonObject{["kind"]="gitiles-text"}};
            if(status is not null)result["status"]=test.Change=="none"?"not-downloaded":status;
            return result;
        }
        var first=Row("raw/first",SourceSnapshotCases.First,SourceSnapshotCases.FirstUrl,"downloaded");
        if(test.Change=="missing-git")first.Remove("git_blob");
        if(test.Change=="fetch-transport")first["transport"]=new JsonObject{["kind"]="sdk-zip-member",["archive_url"]="https://example.test/source.zip",["archive_member"]="source.txt",["archive_sha256"]=new string('d',64)};
        if(test.Change=="fetch-bundle"){first["transport"]!["immutable_url"]=false;first["transport"]!["required_local_snapshot"]="bundle/first";}
        var rows=new JsonArray(
            Row("raw/first",SourceSnapshotCases.First,"https://example.test/nondownloaded-first","unavailable"),first,
            Row("raw/duplicate",SourceSnapshotCases.First,"https://example.test/duplicate-must-not-win","downloaded"),
            Row("raw/second",SourceSnapshotCases.Second,SourceSnapshotCases.SecondUrl,"downloaded"),
            Row("raw/pending",SourceSnapshotCases.Second,"https://example.test/pending","pending"));
        if(test.Change=="missing-status")rows[4]!.AsObject().Remove("status");
        if(test.Change?.StartsWith("implicit",StringComparison.Ordinal)==true)
            foreach(var row in rows){var item=row!.AsObject();item.Remove("transport");item["url"]=item["url"]!.GetValue<string>().Replace("?format=TEXT","",StringComparison.Ordinal);}
        var recipe=new JsonObject{["recipe_version"]=1,["release"]="snapshot-fixture",["inventory"]=new JsonObject{["sources"]=rows},
            ["producer"]=new JsonObject{["path"]="producer",["files"]=new JsonObject{["tool.cs"]=new string('c',64)}}};
        var bytes=JsonSerializer.SerializeToUtf8Bytes(recipe);var path="rehydration/snapshot.json";
        var recipeLock=new JsonObject{["recipe_file"]=path,["sha256"]=test.Change=="lock"?new string('0',64):ContentHash.Sha256(bytes),["source_rows"]=rows.Count,["producer_files"]=1};
        return new(new(path,bytes,"application/json"),new("rehydration/snapshot.lock.json",JsonSerializer.SerializeToUtf8Bytes(recipeLock),"application/json"),Limit:test.Change=="limit"?1:null,
            Profile:test.Change=="fetch-profile"?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore);
    }
    private sealed class CountingPlanner(IRehydrationSnapshotPlanner inner,bool corruptProjection):IRehydrationSnapshotPlanner
    {
        public int Calls{get;private set;}
        public RehydrationPlanResult Plan(RehydrationPlanRequest request)=>inner.Plan(request);
        public RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request)
        {Calls++;var result=inner.PlanSnapshot(request);return corruptProjection?result with{SnapshotProjection=result.SnapshotProjection! with{Release="wrong"}}:result;}
    }
    private sealed class CountingStore:IArtifactStore
    {
        public MemoryAcquisitionStore Inner{get;}=new();public int Writes{get;private set;}
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)=>Inner.ReadAsync(path,cancellationToken);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default){Writes++;return Inner.WriteAsync(artifact,cancellationToken);}
    }
    private sealed class ObservedDelay:ITransportDelay
    {
        public int Calls{get;private set;}
        public Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken=default){Assert.Equal(TimeSpan.FromSeconds(1),delay);Calls++;return Task.CompletedTask;}
    }
    private sealed class FixtureTransport(SourceSnapshotCase test):IByteTransport
    {
        public List<string> Urls{get;}=[];
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {
            Urls.Add(request.Url.AbsoluteUri);
            Assert.Equal(test.Mode=="Fetch"?SnapshotFetchTransport.Retry:TransportRetryPolicy.CanonicalRestore,request.Retry);
            Assert.Null(request.Range);Assert.Null(request.ExpectedSha256);Assert.Null(request.ToolTransfer);
            Assert.Equal(new string('a',40),request.Revision);
            var bytes=request.Url.AbsoluteUri==SourceSnapshotCases.FirstUrl?SourceSnapshotCases.First:SourceSnapshotCases.Second;
            if(test.Change=="transfer"&&Urls.Count==2)bytes=[0];
            var wire=Convert.ToBase64String(bytes);
            if(test.Change=="implicit-whitespace")wire=" \t"+wire+"\r\n";
            if(test.Change=="implicit-junk")wire=wire.Insert(2,"!");
            if(test.Change=="implicit-padding")wire="YQ=";
            return Task.FromResult(new TransportResponse(request.Url,200,System.Text.Encoding.ASCII.GetBytes(wire)));
        }
    }
}
[Trait("Gate","Real")]
public sealed class SourceSnapshotRealTests
{
    [Theory,MemberData(nameof(SourceSnapshotCases.All),MemberType=typeof(SourceSnapshotCases))]
    public Task SameOriginalCallerCases(SourceSnapshotCase test)=>SourceSnapshotCases.AssertCase(test,new NativeSourceSnapshotProbe());
}
