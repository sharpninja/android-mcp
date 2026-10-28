using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SourceSeedCase(string Id,string? Error,SourceSeedCounts Counts,IReadOnlyList<string> StoreReads,
    IReadOnlyList<string> BundleReads,IReadOnlyList<string> Writes);
public sealed record SourceSeedObservation(SourceSeedResult? Result,Exception? Error,IReadOnlyList<string> StoreReads,
    IReadOnlyList<string> BundleReads,IReadOnlyList<string> Writes,int HttpCalls,bool OriginalsRetained,bool BundleRetained,
    bool ManifestLast,bool TokenForwarded);
public interface ISourceSeedProbe
{
    Task<SourceSeedObservation> RunAsync(SourceSeedFixture fixture,CancellationToken cancellationToken);
}
public sealed class SourceSeedFixture
{
    public const string ReceiptPath="sources/seed.json";
    public required SourceSeedCase Case{get;init;}
    public required SourceBatchSeedRequest Request{get;init;}
    public required SourceSeedResult Expected{get;init;}
    public required IReadOnlyDictionary<string,byte[]> Existing{get;init;}
    public required IReadOnlyDictionary<string,byte[]> Bundle{get;init;}
    public static SourceSeedFixture Create(SourceSeedCase test)
    {
        var pin=SourceBatchCases.Pin() with{RequiredLocalSnapshot="snapshots/a"};
        if(test.Id=="absent-no-snapshot")pin=pin with{RequiredLocalSnapshot=null};
        if(test.Id=="size-pin")pin=pin with{SizeBytes=4};
        if(test.Id=="git-pin")pin=pin with{GitBlob=new string('0',40)};
        var rows=test.Id=="logical-aliases"?new[]{SourceBatchCases.Row(0,0,0),SourceBatchCases.Row(1,0,0)}:new[]{SourceBatchCases.Row(0,0,0)};
        if(test.Id=="original-row-data-only")rows=[rows[0] with{OriginalRow=SourceBatchCases.Element(new{local_path="untrusted/ignored",transport=new{required_local_snapshot="untrusted/ignored-snapshot"}})}];
        var source=SourceBatchCases.Physical(0,pin,rows.Select(row=>row.RowIndex).ToArray(),
            pin.RequiredLocalSnapshot is null?[]:[new("logical/0",pin.LocalPath,pin.RequiredLocalSnapshot,pin.Sha256,3)],
            pin.RequiredLocalSnapshot is null?[]:[SourceBatchCases.Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot)]);
        var physical=new List<RehydrationPhysicalSource>{source};
        if(test.Id is "mixed" or "late-corruption-preflight")
        {physical.Add(SourceBatchCases.Physical(1,SourceBatchCases.Pin("raw/b")));rows=[rows[0],SourceBatchCases.Row(1,1)];}
        var alias=new RehydrationCacheAlias(0,"cache/unadmitted.gz",null,rows.Where(row=>row.CacheAliasIndex==0).Select(row=>row.RowIndex).ToArray(),
            [SourceBatchCases.Missing(RehydrationPrerequisiteCode.HistoricalCacheRecordCount,"cache/unadmitted.gz")]);
        var request=SourceBatchCases.Request(physical,rows,physical.Select(item=>item.PhysicalSourceIndex).ToArray(),caches:[alias]) with{ReceiptPath=ReceiptPath};
        var priorRequest=request;
        var existing=new Dictionary<string,byte[]>();var bundle=new Dictionary<string,byte[]>();
        switch(test.Id)
        {
            case "local-import":case "size-pin":case "git-pin":case "logical-aliases":case "original-row-data-only":case "mixed":bundle["raw/a"]=SourceBatchCases.Abc;break;
            case "snapshot-import":bundle["snapshots/a"]=SourceBatchCases.Abc;break;
            case "ordered-local-first":bundle["raw/a"]=SourceBatchCases.Abc;bundle["snapshots/a"]=[0];break;
            case "corrupt-first-no-fallback":bundle["raw/a"]=[0];bundle["snapshots/a"]=SourceBatchCases.Abc;break;
            case "reused-target":existing["raw/a"]=SourceBatchCases.Abc;bundle["raw/a"]=[0];bundle["snapshots/a"]=[0];break;
            case "corrupt-target":existing["raw/a"]=[0];bundle["raw/a"]=SourceBatchCases.Abc;break;
            case "late-corruption-preflight":bundle["raw/a"]=SourceBatchCases.Abc;existing["raw/b"]=[0];break;
            case "partial-selection":request=request with{Projection=request.Projection with{SelectedPhysicalSourceIndices=[]}};break;
            case "historical-policy":request=request with{CachePolicy=SourceBatchCachePolicy.ImportLockedHistorical};break;
            case "sealed-replay":case "sealed-changed-request":existing["raw/a"]=SourceBatchCases.Abc;break;
            case "sealed-absent-stays-absent":bundle["raw/a"]=SourceBatchCases.Abc;break;
        }
        var sealedRun=test.Id.StartsWith("sealed-",StringComparison.Ordinal);
        var originallyAbsent=test.Id is "absent" or "absent-no-snapshot" or "sealed-absent-stays-absent";
        var entries=new List<SourceSeedEntry>();
        foreach(var item in physical)
        {
            var status=originallyAbsent||item.PhysicalSourceIndex==1?SourceSeedStatus.Absent:test.Id=="reused-target"?SourceSeedStatus.Reused:SourceSeedStatus.Imported;
            var artifact=status==SourceSeedStatus.Absent?null:new StoredArtifact(item.Pin.LocalPath,item.Pin.Sha256,3,"application/octet-stream");
            entries.Add(new(item.PhysicalSourceIndex,status,artifact,status==SourceSeedStatus.Imported?test.Id=="snapshot-import"?"snapshots/a":item.Pin.LocalPath:null));
        }
        var originalCounts=new SourceSeedCounts(entries.Count(entry=>entry.Status==SourceSeedStatus.Imported),entries.Count(entry=>entry.Status==SourceSeedStatus.Reused),entries.Count(entry=>entry.Status==SourceSeedStatus.Absent));
        var receipt=new SourceSeedReceipt(SourceBatchCases.RequestHash(priorRequest),priorRequest,entries,originalCounts,
            entries.Where(entry=>entry.Artifact is not null).Select(entry=>entry.Artifact!).ToArray(),originalCounts.Absent==0,false);
        var bytes=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt));
        var manifest=new StoredArtifact(ReceiptPath,ContentHash.Sha256(bytes),bytes.Length,"application/json");
        if(sealedRun)existing[ReceiptPath]=bytes;
        if(test.Id=="sealed-changed-request")request=request with{GenerationId="different-generation"};
        return new(){Case=test,Request=new(request),Expected=new(receipt,manifest,test.Counts,sealedRun),Existing=existing,Bundle=bundle};
    }
}
public static class SourceSeedCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            var receipt=SourceSeedFixture.ReceiptPath;
            SourceSeedCase C(string id,string? error=null,int imported=0,int reused=0,int absent=0,
                string[]? store=null,string[]? bundle=null,string[]? writes=null)=>new(id,error,new(imported,reused,absent),store??[receipt,"raw/a"],bundle??[],writes??[]);
            return new[]{
                C("local-import",imported:1,bundle:["raw/a"],writes:["raw/a",receipt]),
                C("snapshot-import",imported:1,bundle:["raw/a","snapshots/a"],writes:["raw/a",receipt]),
                C("ordered-local-first",imported:1,bundle:["raw/a"],writes:["raw/a",receipt]),
                C("corrupt-first-no-fallback","SOURCE_HASH",bundle:["raw/a"]),
                C("reused-target",reused:1,writes:[receipt]),C("corrupt-target","SOURCE_HASH"),
                C("absent",absent:1,bundle:["raw/a","snapshots/a"],writes:[receipt]),
                C("absent-no-snapshot",absent:1,bundle:["raw/a"],writes:[receipt]),
                C("size-pin","SOURCE_SIZE",bundle:["raw/a"]),C("git-pin","SOURCE_GIT_BLOB",bundle:["raw/a"]),
                C("logical-aliases",imported:1,bundle:["raw/a"],writes:["raw/a",receipt]),
                C("original-row-data-only",imported:1,bundle:["raw/a"],writes:["raw/a",receipt]),
                C("mixed",imported:1,absent:1,store:[receipt,"raw/a","raw/b"],bundle:["raw/a","raw/b"],writes:["raw/a",receipt]),
                C("late-corruption-preflight","SOURCE_HASH",store:[receipt,"raw/a","raw/b"],bundle:["raw/a"]),
                C("partial-selection","SEED_SELECTION",store:[]),C("historical-policy","SEED_CACHE_POLICY",store:[]),
                C("sealed-replay",reused:1),C("sealed-absent-stays-absent",absent:1,store:[receipt]),
                C("sealed-missing-output","BATCH_SEALED"),C("sealed-changed-request","SEED_IDENTITY",store:[receipt]),
                C("cancelled","OperationCanceledException",store:[])
            }.Select(test=>new object[]{test});
        }
    }
    public static async Task AssertCase(SourceSeedCase test,ISourceSeedProbe probe)
    {
        var fixture=SourceSeedFixture.Create(test);using var cancellation=new CancellationTokenSource();if(test.Id=="cancelled")cancellation.Cancel();
        var actual=await probe.RunAsync(fixture,cancellation.Token);
        // Duplicate reads by shared verification/write helpers are allowed; candidate order and read scope remain exact.
        Assert.Equal(test.StoreReads.Order(),actual.StoreReads.Distinct(StringComparer.Ordinal).Order());Assert.Equal(test.BundleReads,actual.BundleReads);Assert.Equal(test.Writes,actual.Writes);
        Assert.Equal(0,actual.HttpCalls);Assert.True(actual.OriginalsRetained);Assert.True(actual.BundleRetained);Assert.True(actual.ManifestLast);Assert.True(actual.TokenForwarded);
        if(test.Error is not null)
        {
            Assert.Null(actual.Result);
            if(test.Error=="OperationCanceledException")Assert.IsAssignableFrom<OperationCanceledException>(actual.Error);else Assert.Equal(test.Error,Assert.IsType<TransportException>(actual.Error).Code);
            return;
        }
        Assert.Null(actual.Error);var result=Assert.IsType<SourceSeedResult>(actual.Result);
        Assert.Equal(fixture.Expected.Manifest,result.Manifest);Assert.Equal(test.Counts,result.CurrentCounts);Assert.Equal(fixture.Expected.Reused,result.Reused);
        Assert.Equal(SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(fixture.Expected.Receipt)),SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(result.Receipt)));
        Assert.False(result.Receipt.HistoricalByteIdentityClaimed);
    }
}
public sealed class ScriptedSourceSeedProbe(string? mutation=null):ISourceSeedProbe
{
    public Task<SourceSeedObservation> RunAsync(SourceSeedFixture fixture,CancellationToken cancellationToken)
    {
        Exception? error=fixture.Case.Error switch{null=>null,"OperationCanceledException"=>new OperationCanceledException(cancellationToken),_=>new TransportException(mutation=="error-code"?"wrong":fixture.Case.Error,"Expected seed refusal")};
        SourceSeedResult? result=error is null?fixture.Expected:null;
        if(mutation=="error-omitted")error=null;if(mutation=="error-type")error=new IOException();
        if(mutation=="result-added")result=fixture.Expected;if(mutation=="result-omitted")result=null;
        if(result is not null)
        {
            var receipt=result.Receipt;var entry=receipt.Entries[0];var artifact=entry.Artifact??new StoredArtifact("wrong",new string('0',64),0,"wrong");
            var wrongEntry=mutation switch{
                "entry-index"=>entry with{PhysicalSourceIndex=99},"entry-status"=>entry with{Status=(SourceSeedStatus)99},"entry-candidate"=>entry with{BundleCandidate="wrong"},
                "entry-path"=>entry with{Artifact=artifact with{RelativePath="wrong"}},"entry-hash"=>entry with{Artifact=artifact with{Sha256="wrong"}},
                "entry-length"=>entry with{Artifact=artifact with{Length=99}},"entry-media"=>entry with{Artifact=artifact with{MediaType="wrong"}},_=>entry};
            receipt=receipt with{Entries=receipt.Entries.Select((value,index)=>index==0?wrongEntry:value).ToArray()};
            receipt=mutation switch{
                "identity"=>receipt with{RequestSha256="wrong"},"request"=>receipt with{Acquisition=receipt.Acquisition with{GenerationId="wrong"}},
                "rows"=>receipt with{Acquisition=receipt.Acquisition with{Projection=receipt.Acquisition.Projection with{Rows=[]}}},
                "entries"=>receipt with{Entries=[]},"outputs"=>receipt with{Outputs=[new("wrong","wrong",99,"wrong")]},
                "original-imported"=>receipt with{OriginalCounts=receipt.OriginalCounts with{Imported=99}},"original-reused"=>receipt with{OriginalCounts=receipt.OriginalCounts with{Reused=99}},"original-absent"=>receipt with{OriginalCounts=receipt.OriginalCounts with{Absent=99}},
                "complete"=>receipt with{RawComplete=!receipt.RawComplete},"historical"=>receipt with{HistoricalByteIdentityClaimed=true},_=>receipt};
            result=result with{Receipt=receipt};
            result=mutation switch{
                "current-imported"=>result with{CurrentCounts=result.CurrentCounts with{Imported=99}},"current-reused"=>result with{CurrentCounts=result.CurrentCounts with{Reused=99}},"current-absent"=>result with{CurrentCounts=result.CurrentCounts with{Absent=99}},
                "reused"=>result with{Reused=!result.Reused},"manifest-path"=>result with{Manifest=result.Manifest with{RelativePath="wrong"}},"manifest-hash"=>result with{Manifest=result.Manifest with{Sha256="wrong"}},
                "manifest-length"=>result with{Manifest=result.Manifest with{Length=99}},"manifest-media"=>result with{Manifest=result.Manifest with{MediaType="wrong"}},_=>result};
        }
        return Task.FromResult(new SourceSeedObservation(result,error,mutation=="store-reads"?["untrusted/ignored"]:fixture.Case.StoreReads,
            mutation=="bundle-reads"?["wrong"]:fixture.Case.BundleReads,mutation=="writes"?["wrong"]:fixture.Case.Writes,mutation=="http"?1:0,
            mutation!="originals",mutation!="bundle",mutation!="last",mutation!="token"));
    }
}
[Trait("Gate","Mock")]
public sealed class SourceSeedMockTests
{
    [Theory,MemberData(nameof(SourceSeedCases.All),MemberType=typeof(SourceSeedCases))]
    public Task CorrectOrderedLocalSeedAndInvocationCounters(SourceSeedCase test)=>SourceSeedCases.AssertCase(test,new ScriptedSourceSeedProbe());
    [Theory,MemberData(nameof(SourceSeedCases.All),MemberType=typeof(SourceSeedCases))]
    public async Task WrongCountersPinsOrderingEffectsAndReceiptMutationsRejected(SourceSeedCase test)
    {
        var mutations=new List<string>{"store-reads","bundle-reads","writes","http","originals","bundle","last","token"};
        if(test.Error is not null){mutations.AddRange(["error-omitted","error-type","result-added"]);if(test.Error!="OperationCanceledException")mutations.Add("error-code");}
        else mutations.AddRange(["error-type","result-omitted","identity","request","rows","entries","outputs","original-imported","original-reused","original-absent","complete","historical",
            "entry-index","entry-status","entry-candidate","entry-path","entry-hash","entry-length","entry-media","current-imported","current-reused","current-absent","reused","manifest-path","manifest-hash","manifest-length","manifest-media"]);
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourceSeedCases.AssertCase(test,new ScriptedSourceSeedProbe(mutation))));
    }
}
