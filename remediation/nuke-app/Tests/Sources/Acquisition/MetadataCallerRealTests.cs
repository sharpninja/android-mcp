using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeMetadataCallerProbe(MetadataPlatformCallerInput? platformInput=null,Action<string>? platformObserved=null):IMetadataCallerProbe
{
    public async Task<MetadataCallerObservation> RunAsync(MetadataCallerCase test)
    {
        var zip=LegacyAcquisitionCases.Zip(true);var store=new ObservedStore();var transport=new RangeTransport(test,zip);store.RangeCalls=()=>transport.Calls;
        var receipt=JsonSerializer.SerializeToUtf8Bytes(new{etag="\"one\"",official=true});
        if(test.Change=="bad-json")receipt=Encoding.UTF8.GetBytes("{");
        var xml=Encoding.UTF8.GetBytes($"<repository><remotePackage path=\"platforms;android-{(test.Change=="package"?"wrong":"37.2")}\"><archives><archive><complete><size>{zip.Length}</size></complete></archive></archives></remotePackage></repository>");
        store.Inner.Files[MetadataProofCases.Receipt]=new(MetadataProofCases.Receipt,receipt,"application/json");
        store.Inner.Files[MetadataProofCases.Xml]=new(MetadataProofCases.Xml,xml,"application/xml");
        if(test.Existing){store.Inner.Files["raw/api.xml"]=new("raw/api.xml",LegacyAcquisitionCases.Xml,"application/octet-stream");store.Inner.Files["raw/properties"]=new("raw/properties",LegacyAcquisitionCases.Properties,"application/octet-stream");}
        var original=store.Inner.Files.ToDictionary(item=>item.Key,item=>item.Value.Bytes.ToArray());
        var pins=new List<StoredArtifact>();
        if(test.Change!="missing-pins"){
            pins.Add(new(MetadataProofCases.Receipt,test.Change=="hash"?new string('0',64):ContentHash.Sha256(receipt),receipt.Length,"application/json"));
            pins.Add(new(MetadataProofCases.Xml,ContentHash.Sha256(xml),xml.Length,"application/xml"));
        }
        var inputs=new ObservedInputs(new PinnedArtifactReader(store));var versions=new ObservedVersions(platformObserved);
        var binder=new ObservedBinder(new SnapshotMetadataProofReader(inputs,pins,versions));var factories=0;
        ISourceBatch Batch(SnapshotCallerMode _){factories++;return new SourceBatch(transport,store,new MemoryAcquisitionStore(),new ScriptedLeaseProvider(),new CanonicalJson());}
        SourceSnapshotResult? result=null;string? error=null;
        try{result=await new SourcesSnapshotOperation(new RehydrationPlanner(new CanonicalJson()),store,Batch,test.Change=="no-reader"?null:binder)
            .RunAsync(new(Request(test),Download:test.Download));}
        catch(Exception problem)
        {
            var chain=new List<Exception>();for(var item=problem;item is not null;item=item.InnerException)chain.Add(item);
            error=chain.OfType<TransportException>().FirstOrDefault()?.Code??chain.OfType<PinnedArtifactException>().FirstOrDefault()?.Code??
                (chain.Any(item=>item is FileNotFoundException)?"NOT_FOUND":problem.GetType().Name);
        }
        var rangeGood=transport.AllRanges&&transport.ReadBytes<10000;
        var reused=store.SourceWrites==0||store.AtFirstWrite==transport.Calls;
        var trace=result is null?error is not null:(result.MetadataProofs?.Count??0)==test.Evidence&&(result.MetadataProofs is null||result.MetadataProofs.All(proof=>proof.RecipeSha256==result.RecipeSha256&&
            proof.RowIndex is 0 or 1&&proof.Evidence.SourcePath==(proof.RowIndex==0?"raw/api.xml":"raw/properties")&&
            proof.Evidence.SourceSha256==ContentHash.Sha256(proof.RowIndex==0?LegacyAcquisitionCases.Xml:LegacyAcquisitionCases.Properties)));
        return new(error,inputs.Reads,binder.Calls,versions.Calls,transport.Calls>0,store.SourceWrites,store.Writes,
            store.Inner.Files.ContainsKey("sources/snapshot.json"),binder.Evidence.Count,factories,rangeGood,reused,
            original.All(item=>store.Inner.Files[item.Key].Bytes.Span.SequenceEqual(item.Value)),
            binder.Evidence.All(proof=>proof.IntegrityScope=="local-input-integrity-only"&&!proof.OfficialSourceAuthority),trace,
            binder.Evidence.Any(proof=>proof.WholeArchiveHashVerified));

        RehydrationPlanRequest Request(MetadataCallerCase c)
        {
            JsonObject Row(string path,string member,byte[] bytes)
            {
                var transfer=new JsonObject{["kind"]="bounded-sdk-zip-range-member",["archive_url"]="https://example.test/sdk.zip",["archive_member"]=member,["metadata_receipt"]=MetadataProofCases.Receipt};
                if(c.Change=="declared-hash")transfer["archive_sha256"]=new string('d',64);
                var row=new JsonObject{["path"]=member,["local_path"]=path,["status"]="downloaded",["url"]="https://example.test/member",["revision"]=new string('a',40),["sha256"]=c.Change=="member"&&path=="raw/properties"?new string('0',64):ContentHash.Sha256(bytes),["git_blob"]=ContentHash.GitBlobSha1Legacy(bytes),["size_bytes"]=bytes.Length,["platform_api_version"]="37.2",["transport"]=transfer};
                if(c.Change=="platform-fallback"){row.Remove("platform_api_version");transfer["platform_api_version"]="37.2";}
                if(platformInput is not null)
                {
                    row.Remove("platform_api_version");
                    if(platformInput.Row is not null)row["platform_api_version"]=JsonNode.Parse(platformInput.Row);
                    if(platformInput.LegacyTransport is not null)transfer["platform_api_version"]=JsonNode.Parse(platformInput.LegacyTransport);
                    if(platformInput.NativeTransport is not null)transfer["platform_version"]=platformInput.NativeTransport;
                }
                return row;
            }
            var recipe=new JsonObject{["recipe_version"]=1,["release"]="metadata-fixture",["inventory"]=new JsonObject{["sources"]=new JsonArray(Row("raw/api.xml","data/api-versions.xml",LegacyAcquisitionCases.Xml),Row("raw/properties","source.properties",LegacyAcquisitionCases.Properties))},["producer"]=new JsonObject{["path"]="producer",["files"]=new JsonObject{["tool.cs"]=new string('c',64)}}};
            var bytes=JsonSerializer.SerializeToUtf8Bytes(recipe);var path="rehydration/metadata.json";
            var pin=new{recipe_file=path,sha256=ContentHash.Sha256(bytes),source_rows=2,producer_files=1};
            return new(new(path,bytes,"application/json"),new("rehydration/metadata.lock.json",JsonSerializer.SerializeToUtf8Bytes(pin),"application/json"),
                Profile:c.Generic?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore);
        }
    }
    private sealed class ObservedStore:IArtifactStore
    {
        public MemoryAcquisitionStore Inner{get;}=new();public int Writes{get;private set;}public int SourceWrites{get;private set;}public int AtFirstWrite{get;private set;}
        public Func<int> RangeCalls{get;set;}=()=>0;
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)=>Inner.ReadAsync(path,token);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default){Writes++;if(artifact.RelativePath.StartsWith("raw/",StringComparison.Ordinal)){if(SourceWrites==0)AtFirstWrite=RangeCalls();SourceWrites++;}return Inner.WriteAsync(artifact,token);}
    }
    private sealed class ObservedInputs(IPinnedArtifactReader inner):IPinnedArtifactReader
    {
        public int Reads{get;private set;}
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken token=default){Reads++;return inner.ReadAsync(pin,token);}
    }
    private sealed class ObservedVersions(Action<string>? observe):ISdkArchivePackageVersionValidator
    {
        private readonly SdkArchiveVersionValidator inner=new();public int Calls{get;private set;}
        public long ResolveSize(ReadOnlyMemory<byte> xml,string platform,string? package=null,long? expected=null)=>inner.ResolveSize(xml,platform,package,expected);
        public long ResolvePackageSize(ReadOnlyMemory<byte> xml,string platform,SdkArchivePackageChoice package,long? expected=null){Calls++;observe?.Invoke(platform);return inner.ResolvePackageSize(xml,platform,package,expected);}
    }
    private sealed class ObservedBinder(ISnapshotMetadataProofReader inner):ISnapshotMetadataProofReader
    {
        public int Calls{get;private set;}public List<SnapshotMetadataProofEvidence> Evidence{get;}=[];
        public async Task<SnapshotMetadataProofAdmission> BindAsync(SourcePin pin,RehydrationCanonicalMetadataReference reference,CancellationToken token=default)
        {Calls++;var result=await inner.BindAsync(pin,reference,token);if(result.Evidence is not null)Evidence.Add(result.Evidence);return result;}
    }
    private sealed class RangeTransport(MetadataCallerCase test,byte[] bytes):IByteTransport
    {
        public int Calls{get;private set;}public int ReadBytes{get;private set;}public bool AllRanges{get;private set;}=true;
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken token=default)
        {
            Calls++;var range=request.Range;AllRanges&=range is not null&&request.Url==new Uri("https://example.test/sdk.zip")&&request.ContentEncoding=="identity"&&request.Retry==(test.Generic?TransportRetryPolicy.SingleAttempt:TransportRetryPolicy.CanonicalRestore);
            if(range is null)throw new InvalidOperationException("Whole platform download is forbidden.");
            ReadBytes+=range.Length;
            var etag=test.Generic?null:test.Change=="range"?"\"wrong\"":"\"one\"";
            if(!test.Generic){AllRanges&=request.IfMatch=="\"one\""&&request.ExpectedETag=="\"one\""&&request.ExpectedTotalLength==bytes.Length;}
            return Task.FromResult(new TransportResponse(request.Url,206,bytes.AsMemory((int)range.Offset,range.Length),etag,$"bytes {range.Offset}-{range.Offset+range.Length-1}/{bytes.Length}"));
        }
    }
}
[Trait("Gate","Real")]
public sealed class MetadataCallerRealTests
{
    [Theory,MemberData(nameof(MetadataCallerCases.All),MemberType=typeof(MetadataCallerCases))]
    public Task SameOriginalCallerProofComposition(MetadataCallerCase test)=>MetadataCallerCases.AssertCase(test,new NativeMetadataCallerProbe());
}
