using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// FR-BUILD-004, TEST-BUILD-004: consume the sole Publication projection;
// preflight all selected requirements, preserve aliases, seal only after verification.
public sealed record SourceBatchCase(string Id, SourceBatchRequest Request,
    IReadOnlyDictionary<string, byte[]> Existing, IReadOnlyDictionary<string, byte[]> Bundle,
    string? Error, int Network, int Writes, bool Complete, int ProcessedRows,
    int Sources, int Caches, bool Reused = false);
public sealed record SourceBatchObservation(SourceBatchResult? Result, string? Error,
    int Network, int Writes, bool OriginalsRetained, bool BundleRetained,
    bool ManifestLast, IReadOnlyList<string> AcquiredPaths);
public interface ISourceBatchProbe { Task<SourceBatchObservation> RunAsync(SourceBatchCase test); }

public static class SourceBatchCases
{
    public const string AbcSha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public static readonly byte[] Abc = Encoding.ASCII.GetBytes("abc");
    public static readonly byte[] Gzip = CacheBytes();
    public static readonly CanonicalJson Canonical = new();
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value);
    public static SourcePin Pin(string path = "raw/a", AcquisitionCompatibilityProfile profile = AcquisitionCompatibilityProfile.CanonicalRestore)
        => new(path, new Uri("https://example.test/" + new string('a', 40) + "/a"), new string('a', 40), AbcSha, 3, Compatibility: profile);
    public static RehydrationSourceRow Row(int index, int physical, int? cache = null)
        => new(index, "logical/" + index, "java-source", physical, cache,
            new("repo", "revision", "surface", "scope", "license", Element(new { api = 35 }),
                [Element(new { origin = index })], ["explicit"]), Element(new { row = index, extra = "retained" }));
    public static RehydrationPhysicalSource Physical(int index, SourcePin? pin = null,
        IReadOnlyList<int>? rows = null, IReadOnlyList<SnapshotRequirement>? snapshots = null,
        IReadOnlyList<RehydrationPrerequisite>? prerequisites = null)
        => new(index, pin ?? Pin(), rows ?? [index], snapshots ?? [], prerequisites ?? []);
    public static SourceBatchRequest Request(IReadOnlyList<RehydrationPhysicalSource>? sources = null,
        IReadOnlyList<RehydrationSourceRow>? rows = null, IReadOnlyList<int>? selected = null,
        AcquisitionCompatibilityProfile profile = AcquisitionCompatibilityProfile.CanonicalRestore,
        IReadOnlyList<RehydrationCacheAlias>? caches = null, SourceBatchCachePolicy policy = SourceBatchCachePolicy.PreserveAliases,
        bool resume = true)
        => new("generation-1", "receipts/source.json", new("release", new string('b', 64), profile,
            rows ?? [Row(0, 0)], sources ?? [Physical(0, Pin(profile: profile))], caches ?? [], selected ?? [0], resume), policy);
    public static RehydrationPrerequisite Missing(RehydrationPrerequisiteCode code, string path = "raw/a", int row = 0)
        => new(code, [row], path, "original proof absent");
    public static HistoricalCachePin CachePin => new("cache/a.gz", "java-source", ContentHash.Sha256(Gzip), new string('c', 64), 1, AbcSha, new string('b', 64));
    private static byte[] CacheBytes()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true)) gzip.Write(Encoding.UTF8.GetBytes("{\"symbol\":\"A\"}\n"));
        return output.ToArray();
    }
    public static IEnumerable<object[]> All
    {
        get
        {
            var empty = new Dictionary<string, byte[]>();
            SourceBatchCase C(string id, SourceBatchRequest request, string? error = null, int network = 0,
                int writes = 0, bool complete = false, int processed = 0, int sources = 0, int caches = 0,
                IReadOnlyDictionary<string, byte[]>? existing = null, IReadOnlyDictionary<string, byte[]>? bundle = null, bool reused = false)
                => new(id, request, existing ?? empty, bundle ?? empty, error, network, writes, complete, processed, sources, caches, reused);
            var snap = new SnapshotRequirement("mutable", "raw/a", "bundle/a", AbcSha, 3);
            var required = Pin() with { RequiredLocalSnapshot = "snapshot/a" };
            var archive = Pin() with { Archive = new(new Uri("https://example.test/sdk.zip"), "source.properties", SizeBytes: 100) };
            var alias = new RehydrationCacheAlias(0, "cache/a.gz", CachePin, [0], []);
            var aliased = Request(caches: [alias], rows: [Row(0, 0, 0)]);
            return new[]
            {
                C("download", Request(), network: 1, writes: 2, complete: true, processed: 1, sources: 1),
                C("reuse", Request(), writes: 1, complete: true, processed: 1, sources: 1, existing: new Dictionary<string, byte[]> { ["raw/a"] = Abc }),
                C("aliases-preserved", Request(sources: [Physical(0, rows: [0,1])], rows: [Row(0,0),Row(1,0)]), network:1,writes:2,complete:true,processed:2,sources:1),
                C("limited-selection", Request([Physical(0),Physical(1,Pin("raw/b"))], [Row(0,0),Row(1,1)], [1]), network:1,writes:2,processed:1,sources:1),
                C("empty-selection", Request(selected:[]), writes:1),
                C("bad-existing", Request(), "SOURCE_HASH", existing:new Dictionary<string,byte[]> { ["raw/a"]=Encoding.ASCII.GetBytes("abd") }),
                C("no-resume", Request(resume:false), "BATCH_RESUME", existing:new Dictionary<string,byte[]> { ["raw/a"]=Abc }),
                C("snapshot-missing", Request([Physical(0,required,snapshots:[snap with { BundlePath=null }],prerequisites:[Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot)])]), "BATCH_PREREQUISITE"),
                C("snapshot-existing-target", Request([Physical(0,required,snapshots:[snap with { BundlePath=null }],prerequisites:[Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot)])]), writes:1,complete:true,processed:1,sources:1,existing:new Dictionary<string,byte[]> { ["raw/a"]=Abc }),
                C("snapshot-bundle", Request([Physical(0,required,snapshots:[snap])]), writes:3,complete:true,processed:1,sources:1,bundle:new Dictionary<string,byte[]> { ["bundle/a"]=Abc }),
                C("snapshot-tampered", Request([Physical(0,required,snapshots:[snap])]), "SOURCE_HASH", bundle:new Dictionary<string,byte[]> { ["bundle/a"]=Encoding.ASCII.GetBytes("bad") }),
                C("later-preflight-denial", Request([Physical(0),Physical(1,Pin("raw/b"),prerequisites:[Missing(RehydrationPrerequisiteCode.SourceRevision,"raw/b",1)])],[Row(0,0),Row(1,1)],[0,1]), "BATCH_PREREQUISITE"),
                C("canonical-archive-missing-proof", Request([Physical(0,archive,prerequisites:[Missing(RehydrationPrerequisiteCode.CanonicalArchiveETag)])]), "BATCH_PREREQUISITE"),
                C("canonical-archive-reused-member", Request([Physical(0,archive,prerequisites:[Missing(RehydrationPrerequisiteCode.CanonicalArchiveETag)])]),writes:1,complete:true,processed:1,sources:1,existing:new Dictionary<string,byte[]> { ["raw/a"]=Abc }),
                C("generic-explicit", Request(profile:AcquisitionCompatibilityProfile.GenericRehydrate),network:1,writes:2,complete:true,processed:1,sources:1),
                C("profile-mismatch", Request([Physical(0,Pin(profile:AcquisitionCompatibilityProfile.GenericRehydrate))]),"BATCH_PROFILE"),
                C("archive-profile-mismatch", Request([Physical(0,archive with { Archive=archive.Archive! with {Compatibility=AcquisitionCompatibilityProfile.GenericRehydrate} })]),"BATCH_PROFILE"),
                C("duplicate-selected-index", Request(selected:[0,0]),"BATCH_PROJECTION"),
                C("unknown-selected-index", Request(selected:[5]),"BATCH_PROJECTION"),
                C("row-index-association", Request(rows:[Row(0,4)]),"BATCH_PROJECTION"),
                C("path-collision", Request([Physical(0),Physical(1,Pin("RAW/A"))],[Row(0,0),Row(1,1)],[0,1]),"BATCH_PROJECTION"),
                C("receipt-output-collision", Request() with { ReceiptPath="raw/a" },"BATCH_PROJECTION"),
                C("aliases-data-only", aliased,network:1,writes:2,complete:true,processed:1,sources:1),
                C("historical-import", aliased with {CachePolicy=SourceBatchCachePolicy.ImportLockedHistorical},network:1,writes:3,complete:true,processed:1,sources:1,caches:1,bundle:new Dictionary<string,byte[]> { ["cache/a.gz"]=Gzip }),
                C("historical-missing", aliased with {CachePolicy=SourceBatchCachePolicy.ImportLockedHistorical},"BATCH_PREREQUISITE"),
                C("historical-pin-missing", Request(caches:[alias with {Pin=null,Prerequisites=[Missing(RehydrationPrerequisiteCode.HistoricalCacheRecordCount,"cache/a.gz")]}],rows:[Row(0,0,0)],policy:SourceBatchCachePolicy.ImportLockedHistorical),"BATCH_PREREQUISITE"),
                C("historical-tampered", aliased with {CachePolicy=SourceBatchCachePolicy.ImportLockedHistorical},"CACHE_HASH",bundle:new Dictionary<string,byte[]> { ["cache/a.gz"]=Abc }),
                C("historical-recipe-mismatch", Request(caches:[alias with {Pin=CachePin with {RecipeSha256=new string('d',64)}}],rows:[Row(0,0,0)],policy:SourceBatchCachePolicy.ImportLockedHistorical),"BATCH_PROJECTION"),
                C("second-transfer-failure", Request([Physical(0),Physical(1,Pin("raw/b"))],[Row(0,0),Row(1,1)],[0,1]),"SCRIPTED_HTTP",network:2,writes:1),
                C("sealed-resume",Request(),complete:true,processed:1,sources:1,reused:true),
                C("sealed-missing-output",Request(),"BATCH_SEALED"),
                C("sealed-request-change",Request(),"BATCH_IDENTITY")
            }.Select(test => new object[] { test });
        }
    }
    public static SourceBatchReceipt ExpectedReceipt(SourceBatchCase test)
    {
        var projection=test.Request.Projection;
        var selected=projection.PhysicalSources.Where(x=>projection.SelectedPhysicalSourceIndices.Contains(x.PhysicalSourceIndex)).ToArray();
        var sources=selected.Select(x=>new SourceBatchSource(x.PhysicalSourceIndex,new(new(x.Pin.LocalPath,x.Pin.Sha256,3,"application/octet-stream"),
            test.Existing.ContainsKey(x.Pin.LocalPath)||test.Reused?"reused":"downloaded",x.Pin.Url,x.Pin.Revision,x.Pin.GitBlob))).ToArray();
        var outputs=sources.Select(x=>x.Source.Artifact).ToList();
        if(test.Id=="snapshot-bundle")outputs.Insert(0,new("snapshot/a",AbcSha,3,"application/octet-stream"));
        var caches=projection.CacheAliases.Where(x=>test.Caches>0).Select(x=>new SourceBatchHistoricalCache(x.CacheAliasIndex,x.Pin!,new(x.CachePath,x.Pin!.GZipSha256,Gzip.Length,"application/gzip"))).ToArray();
        outputs.AddRange(caches.Select(x=>x.Artifact));
        return new(test.Request.GenerationId,RequestHash(test.Request),projection,test.Request.CachePolicy,projection.Rows.Count,test.ProcessedRows,test.ProcessedRows,
            projection.PhysicalSources.Count,selected.Length,test.Complete,false,sources,caches,outputs);
    }
    public static string RequestHash(SourceBatchRequest request)=>Canonical.Hash(Element(request));
    public static async Task AssertCase(SourceBatchCase test,ISourceBatchProbe provider)
    {
        var observed=await provider.RunAsync(test);
        Assert.Equal(test.Error,observed.Error);Assert.Equal(test.Network,observed.Network);Assert.Equal(test.Writes,observed.Writes);
        Assert.True(observed.OriginalsRetained);Assert.True(observed.BundleRetained);Assert.True(observed.ManifestLast);
        if(test.Error is not null){Assert.Null(observed.Result);return;}
        var result=Assert.IsType<SourceBatchResult>(observed.Result);var expected=ExpectedReceipt(test);
        Assert.Equal(test.Reused,result.Reused);
        // Compare every receipt property, including detached original rows and unknown container pins.
        Assert.Equal(Canonical.Serialize(Element(expected)),Canonical.Serialize(Element(result.Receipt)));
        Assert.Equal(test.Request.ReceiptPath,result.Manifest.RelativePath);Assert.Equal("application/json",result.Manifest.MediaType);
        var bytes=Canonical.Serialize(Element(result.Receipt));Assert.Equal(ContentHash.Sha256(bytes),result.Manifest.Sha256);Assert.Equal(bytes.Length,result.Manifest.Length);
        Assert.Equal(expected.Sources.Select(x=>x.Source.Artifact.RelativePath),observed.AcquiredPaths);
    }
}
public sealed class ScriptedSourceBatchProbe(string? mutation=null):ISourceBatchProbe
{
    public Task<SourceBatchObservation> RunAsync(SourceBatchCase test)
    {
        var receipt=SourceBatchCases.ExpectedReceipt(test);
        receipt=mutation switch {
            "generation"=>receipt with {GenerationId="wrong"},"identity"=>receipt with {RequestSha256="wrong"},
            "projection"=>receipt with {Projection=receipt.Projection with {Rows=[]}},"policy"=>receipt with {CachePolicy=(SourceBatchCachePolicy)99},
            "totalrows"=>receipt with {TotalRows=-1},"selectedrows"=>receipt with {SelectedRows=-1},"processed"=>receipt with {ProcessedRows=-1},
            "totalphysical"=>receipt with {TotalPhysicalSources=-1},"selectedphysical"=>receipt with {SelectedPhysicalSources=-1},
            "complete"=>receipt with {Complete=!receipt.Complete},"historicalclaim"=>receipt with {HistoricalByteIdentityClaimed=true},
            "sources"=>receipt with {Sources=[new(-1,new(new("wrong","wrong",-1,"wrong"),"wrong",new("https://wrong.test"),"wrong",null))]},
            "caches"=>receipt with {HistoricalCaches=[new(-1,SourceBatchCases.CachePin,new("wrong","wrong",-1,"wrong"))]},
            "outputs"=>receipt with {Outputs=[new("wrong","wrong",-1,"wrong")]},_=>receipt};
        var bytes=SourceBatchCases.Canonical.Serialize(SourceBatchCases.Element(receipt));
        var manifest=new StoredArtifact(mutation=="manifestpath"?"wrong":test.Request.ReceiptPath,
            mutation=="manifesthash"?"wrong":ContentHash.Sha256(bytes),mutation=="manifestlength"?-1:bytes.Length,mutation=="mediatype"?"wrong":"application/json");
        SourceBatchResult? result=test.Error is null||mutation=="error"?new(receipt,manifest,mutation=="reused"?!test.Reused:test.Reused):null;
        return Task.FromResult(new SourceBatchObservation(result,mutation=="error"?null:mutation=="code"?"wrong":test.Error,
            test.Network+(mutation=="network"?1:0),test.Writes+(mutation=="writes"?1:0),mutation!="originals",mutation!="bundle",mutation!="last",
            mutation=="paths"?["wrong"]:receipt.Sources.Select(x=>x.Source.Artifact.RelativePath).ToArray()));
    }
}
[Trait("Gate","Mock")]
public sealed class SourceBatchMockTests
{
    [Theory,MemberData(nameof(SourceBatchCases.All),MemberType=typeof(SourceBatchCases))]
    public Task CorrectMock(SourceBatchCase test)=>SourceBatchCases.AssertCase(test,new ScriptedSourceBatchProbe());
    [Theory,MemberData(nameof(SourceBatchCases.All),MemberType=typeof(SourceBatchCases))]
    public async Task EveryResultAndInteractionDiscriminates(SourceBatchCase test)
    {
        var mutations=test.Error is null
            ?new[]{"generation","identity","projection","policy","totalrows","selectedrows","processed","totalphysical","selectedphysical","complete","historicalclaim","sources","caches","outputs","manifestpath","manifesthash","manifestlength","mediatype","reused","paths","network","writes","originals","bundle","last"}
            :new[]{"error","code","network","writes","originals","bundle","last"};
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourceBatchCases.AssertCase(test,new ScriptedSourceBatchProbe(mutation))));
    }
}
