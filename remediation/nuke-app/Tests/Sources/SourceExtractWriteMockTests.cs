using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class SourceExtractWriteDependencies
{
    public SourceExtractWriteFixture Fixture { get; }
    private readonly CancellationToken expectedToken;
    private readonly Dictionary<string,byte[]> originalRaw,originalGenerations;
    public Dictionary<string,Artifact> RawFiles { get; }
    public Dictionary<string,Artifact> GenerationFiles { get; }
    public List<string> Events { get; }=[];
    public int Creates,Disposals,Dispatches,RawReads,Lookups,Publications;
    public bool TokenForwarded { get; private set; }=true;
    public SourceExtractWriteDependencies(SourceExtractWriteFixture fixture,CancellationToken token)
    {
        Fixture=fixture;expectedToken=token;RawFiles=fixture.RawFiles.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);GenerationFiles=fixture.GenerationFiles.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        originalRaw=RawFiles.ToDictionary(pair=>pair.Key,pair=>pair.Value.Bytes.ToArray());originalGenerations=GenerationFiles.ToDictionary(pair=>pair.Key,pair=>pair.Value.Bytes.ToArray());
    }
    public bool OriginalsRetained=>originalRaw.All(pair=>RawFiles[pair.Key].Bytes.Span.SequenceEqual(pair.Value))&&originalGenerations.All(pair=>GenerationFiles[pair.Key].Bytes.Span.SequenceEqual(pair.Value));
    public void Check(CancellationToken token){TokenForwarded&=token==expectedToken;}
    public IArtifactStore Raw=>new Store(this,true);
    public IArtifactStore Generations=>new Store(this,false);
    public ISourcesRuntimeFactory Runtimes=>new Factory(this);
    public ISealedSourceCache Cache=>new CacheProvider(this);
    public IArtifactLeaseProvider Leases=>new LeaseProvider(this);
    private sealed class Store(SourceExtractWriteDependencies rig,bool raw):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
        {
            rig.Check(cancellationToken);cancellationToken.ThrowIfCancellationRequested();rig.Events.Add((raw?"raw-read:":"generation-read:")+path);if(raw)rig.RawReads++;
            var files=raw?rig.RawFiles:rig.GenerationFiles;return files.TryGetValue(path,out var artifact)?Task.FromResult<ReadOnlyMemory<byte>>(artifact.Bytes.ToArray()):throw new FileNotFoundException(path);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {
            rig.Check(cancellationToken);cancellationToken.ThrowIfCancellationRequested();Assert.False(raw,"Raw originals cannot be output");
            rig.Events.Add("write:"+artifact.RelativePath);
            if(rig.GenerationFiles.TryGetValue(artifact.RelativePath,out var old)&&!old.Bytes.Span.SequenceEqual(artifact.Bytes.Span))throw new IOException("Retain original artifact");
            rig.GenerationFiles[artifact.RelativePath]=artifact with{Bytes=artifact.Bytes.ToArray()};return Task.FromResult(SourceExtractWriteFixture.Pin(artifact));
        }
    }
    private sealed class Factory(SourceExtractWriteDependencies rig):ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile,SourceJavaAdmission? java=null)
        {Assert.Equal(rig.Fixture.Request.Profile,profile);Assert.Equal(rig.Fixture.Request.Java,java);rig.Creates++;rig.Events.Add("factory-create");return new Runtime(rig);}
    }
    private sealed class Runtime(SourceExtractWriteDependencies rig):ISourcesCacheRuntime
    {
        private bool disposed;
        public IReadOnlySet<string> CacheProducerKinds { get; }=new HashSet<string>([rig.Fixture.Request.Projection.Rows[0].Kind!],StringComparer.Ordinal);
        public SourcesRuntimeConcerns Concerns=>throw new InvalidOperationException("Writer uses provider descriptors and dispatcher, not unrelated concerns");
        public ISourcesExtractionDispatcher Dispatcher=>new ScriptedSourcesExtractionDispatcher([rig.Fixture.Request.Projection.Rows[0].Kind!],(source,kind,scope)=>
        {
            ObjectDisposedException.ThrowIf(disposed,this);var row=rig.Fixture.Request.Projection.Rows.Single(row=>row.LogicalPath==source.RelativePath);
            Assert.Equal(row.Kind,kind);Assert.Equal(row.Context.Scope,scope);Assert.Equal(rig.Fixture.Context,source.Context);Assert.Equal(new byte[]{(byte)'x'},source.Bytes.ToArray());
            rig.Dispatches++;rig.Events.Add("dispatch:"+row.RowIndex);
            var records=JsonNode.Parse(rig.Fixture.Records(rig.Fixture.Case.Id=="alias-conflict"&&row.RowIndex==1))!.AsArray().OfType<JsonObject>().ToArray();
            return new(records,rig.Fixture.Issues,rig.Fixture.Status,rig.Fixture.Metadata(row));
        });
        public void Dispose(){if(disposed)return;disposed=true;rig.Disposals++;rig.Events.Add("runtime-dispose");}
    }
    private sealed class CacheProvider(SourceExtractWriteDependencies rig):ISealedSourceCache
    {
        private void Identity(SourceCacheRequest request)
        {
            var fixture=rig.Fixture;Assert.Equal(fixture.Request.Projection.Rows[0].Kind,request.Kind);Assert.Equal(fixture.Request.Producer.ProducerId,request.ProducerId);
            Assert.Equal(fixture.Request.Profile.Id,request.ProfileId);Assert.Equal(fixture.Request.Projection.RecipeSha256,request.RecipeSha256);Assert.Equal(SourceExtractReadFixture.RawSha,request.SourceSha256);
            Assert.Equal(fixture.Request.Producer.RuntimeIdentity,request.RuntimeIdentity);Assert.Equal(fixture.Request.Producer.ProducerFiles.OrderBy(pair=>pair.Key),request.ProducerFiles!.OrderBy(pair=>pair.Key));
        }
        public Task<SourceCacheLookup> LookupAsync(SourceCacheRequest request,SourceCacheSeal seal,CancellationToken cancellationToken=default)
        {
            rig.Check(cancellationToken);Identity(request);Assert.Equal(rig.Fixture.PriorLookup.Receipt.Path,request.Path);Assert.Equal(rig.Fixture.PriorLookup.Seal,seal);Assert.Empty(request.RecordsJson.ToArray());
            rig.Lookups++;rig.Events.Add("sealed-lookup");return Task.FromResult(rig.Fixture.PriorLookup);
        }
        public async Task<SourceCachePublication> PublishAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
        {
            rig.Check(cancellationToken);Identity(request);Assert.Equal("new-generation/cache/0.json.gz",request.Path);Assert.Equal(rig.Fixture.Records(),request.RecordsJson.ToArray());rig.Publications++;rig.Events.Add("cache-publish");
            if(rig.Fixture.Case.Id=="publisher-fault")throw rig.Fixture.PublisherFault;
            foreach(var artifact in rig.Fixture.PublishedArtifacts)await rig.Generations.WriteAsync(artifact,cancellationToken);
            return rig.Fixture.Publication;
        }
        public Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)=>throw new IOException("Use one sealed publication engine");
        public Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)=>throw new IOException("Never legacy parsed-record Read on a cache hit");
    }
    private sealed class LeaseProvider(SourceExtractWriteDependencies rig):IArtifactLeaseProvider
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken cancellationToken=default)
        {rig.Check(cancellationToken);rig.Events.Add("lease:"+scope);return ValueTask.FromResult<IAsyncDisposable>(new Handle(rig));}
        private sealed class Handle(SourceExtractWriteDependencies rig):IAsyncDisposable
        {public ValueTask DisposeAsync(){rig.Events.Add("lease-dispose");return ValueTask.CompletedTask;}}
    }
}

public sealed class SourceExtractWriteProbe(Func<SourceExtractWriteFixture,SourceExtractWriteDependencies,ISourcesExtractOperation> factory,
    Func<SourceExtractWriteObservation,SourceExtractWriteObservation>? mutation=null):ISourceExtractWriteProbe
{
    public async Task<SourceExtractWriteObservation> WriteAsync(SourceExtractWriteFixture fixture,CancellationToken cancellationToken)
    {
        var rig=new SourceExtractWriteDependencies(fixture,cancellationToken);SourceExtractResult? result=null;Exception? error=null;
        try{result=await factory(fixture,rig).ExtractAsync(fixture.Request,cancellationToken);}catch(Exception failure){error=failure;}
        var observed=new SourceExtractWriteObservation(result,error,rig.GenerationFiles,rig.Events.ToArray(),rig.Creates,rig.Disposals,rig.Dispatches,rig.RawReads,rig.Lookups,rig.Publications,rig.OriginalsRetained,rig.TokenForwarded);
        return mutation?.Invoke(observed)??observed;
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceExtractWriteMockTests
{
    // This scripts stage responses and fixed fixture failures through the real
    // dependency interfaces. It is not a selection, alias or commit algorithm.
    private sealed class ScriptedOperation(SourceExtractWriteFixture data,SourceExtractWriteDependencies rig):ISourcesExtractOperation
    {
        public async Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request,CancellationToken cancellationToken=default)
        {
            Assert.Equal(data.Request,request);rig.Check(cancellationToken);cancellationToken.ThrowIfCancellationRequested();
            if(data.Case.Id=="negative-limit")throw new ArgumentOutOfRangeException(nameof(request.CacheSourceLimit));
            if(data.Case.Id=="unsafe-root")throw new ArgumentException("Unsafe generation root");
            if(data.Case.Id=="prior-pin")
            {await rig.Generations.ReadAsync(request.TrustedPriorIndex!.RelativePath,cancellationToken);throw new SourceExtractException("SOURCE_EXTRACT_INDEX_PIN","Scripted pin rejection");}
            using var runtime=rig.Runtimes.Create(request.Profile,request.Java);var results=new List<SourceExtractRow>();
            if(data.WantedHits>0)
            {
                foreach(var artifact in data.GenerationFiles.Where(pair=>pair.Key.EndsWith("index.json",StringComparison.Ordinal)))await rig.Generations.ReadAsync(artifact.Key,cancellationToken);
                await rig.Cache.LookupAsync(CacheRequest(data.PriorLookup.Receipt.Path,ReadOnlyMemory<byte>.Empty),data.PriorLookup.Seal,cancellationToken);
            }
            foreach(var row in request.Projection.Rows.Take(data.SelectedCount))
            {
                if(data.Case.Resume&&row.RowIndex==0){results.Add(data.ExpectedRows[0]);continue;}
                var bytes=await rig.Raw.ReadAsync("raw/API.xml",cancellationToken);
                if(data.Case.Id=="raw-pin")throw new SourceExtractException("SOURCE_EXTRACT_RAW_PIN","Scripted source pin rejection");
                var extracted=runtime.Dispatcher.Extract(new(row.LogicalPath,bytes,data.Context),row.Kind,row.Context.Scope);
                var expected=data.ExpectedRows[row.RowIndex];results.Add(expected with{RecordsJson=JsonSerializer.SerializeToUtf8Bytes(extracted.Records),Issues=extracted.Issues,Metadata=extracted.Metadata,StructuralStatus=extracted.StructuralStatus});
            }
            if(data.Case.Id=="alias-conflict")throw new SourceExtractException("SOURCE_EXTRACT_ALIAS_CONFLICT","Scripted alias content rejection");
            await using var lease=await rig.Leases.AcquireAsync("sources-extract/"+request.GenerationRoot,cancellationToken);
            if(data.Case.Id=="occupied")throw new ArtifactGenerationException("OUTPUT_EXISTS","Scripted shared Core freshness rejection");
            if(data.WantedPublications>0)
            {
                try{await rig.Cache.PublishAsync(CacheRequest(data.Publication.Receipt.Path,data.Records()),cancellationToken);}
                catch(Exception error){throw new ArtifactGenerationException("PROJECTION_FAILED","Scripted Core materialization failure",error);}
            }
            var canonical=new CanonicalJson();var indexed=results.Select(result=>
            {
                var row=request.Projection.Rows.Single(row=>row.RowIndex==result.RowIndex);
                return new SourceExtractIndexRow(row.RowIndex,row.LogicalPath,row.Kind,row.PhysicalSourceIndex,row.CacheAliasIndex,SourceExtractReadFixture.RawSha,1,
                    canonical.Hash(row.OriginalRow),result.Context,row.Context.Scope,result.StructuralStatus,result.Issues,result.Metadata,result.RawSourceBase64,result.CacheReceipt!,result.CacheSeal!);
            }).ToArray();
            var indexData=new SourceExtractIndex("androidmcp-native-source-index-v1",request.Projection.Release,request.Projection.RecipeSha256,request.Profile.Id,request.Producer,indexed);
            var indexArtifact=new Artifact("new-generation/source-index.json",canonical.Serialize(JsonSerializer.SerializeToElement(indexData)),"application/json");
            var dataPin=await rig.Generations.WriteAsync(indexArtifact,cancellationToken);
            var inputs=new List<StoredArtifact>();
            if(data.WantedExtractions>0)inputs.Add(SourceExtractWriteFixture.Pin(data.RawFiles["raw/API.xml"]) with{RelativePath="source/raw/API.xml"});
            if(data.WantedHits>0)inputs.AddRange(data.GenerationFiles.Select(pair=>SourceExtractWriteFixture.Pin(pair.Value) with{RelativePath="generation/"+pair.Key}));
            var outputs=data.WantedPublications>0?data.PublishedArtifacts.Select(SourceExtractWriteFixture.Pin).Append(dataPin).ToArray():[dataPin];
            var manifest=new SourceExtractGenerationManifest("androidmcp-native-source-generation-v1","new-generation",inputs,outputs,dataPin);
            var committed=await rig.Generations.WriteAsync(new("new-generation/index.json",canonical.Serialize(JsonSerializer.SerializeToElement(manifest)),"application/json"),cancellationToken);
            return new(committed,results,data.SelectedCount,data.SelectedCount>0?1:0,data.WantedHits,data.WantedExtractions);
        }
        private SourceCacheRequest CacheRequest(string path,ReadOnlyMemory<byte> records)=>new(path,data.Request.Projection.Rows[0].Kind!,data.Request.Producer.ProducerId,
            data.Request.Profile.Id,data.Request.Projection.RecipeSha256,SourceExtractReadFixture.RawSha,records,ProducerFiles:data.Request.Producer.ProducerFiles,RuntimeIdentity:data.Request.Producer.RuntimeIdentity);
    }
    private static ISourceExtractWriteProbe Probe(Func<SourceExtractWriteObservation,SourceExtractWriteObservation>? wrong=null)=>new SourceExtractWriteProbe((data,rig)=>new ScriptedOperation(data,rig),wrong);
    [Theory,MemberData(nameof(SourceExtractWriteContractCases.Cases),MemberType=typeof(SourceExtractWriteContractCases))]
    public Task CorrectMocksProveLogicalSelectionAliasSealsContextAndCommitAssertions(string id)=>SourceExtractWriteContractCases.AssertContract(SourceExtractWriteContractCases.Get(id),Probe());
    public static IEnumerable<object[]> Controls=>SourceExtractWriteContractCases.All.SelectMany(test=>new[]{"retention","token","disposal","result","error","commit","lease"}.Select(control=>new object[]{test.Id,control}));
    [Theory,MemberData(nameof(Controls))]
    public async Task EachWrongFailureOwnershipOrCommitResponseIsRejected(string id,string control)
    {
        var test=SourceExtractWriteContractCases.Get(id);
        SourceExtractWriteObservation Wrong(SourceExtractWriteObservation value)=>control switch
        {
            "retention"=>value with{OriginalsRetained=false},"token"=>value with{TokenForwarded=false},"disposal"=>value with{RuntimeDisposals=value.RuntimeDisposals+1},
            "error"=>value with{Error=test.Error is null?new IOException("Invented"):new IOException("Replaced")},
            "commit"=>value with{Events=test.Error is null?value.Events.Concat(["write:incorrect-after-commit"]).ToArray():value.Events.Concat(["write:new-generation/index.json"]).ToArray()},
            "lease"=>value with{Events=value.Events.Any(item=>item.StartsWith("lease:",StringComparison.Ordinal))?value.Events.Where(item=>!item.StartsWith("lease:",StringComparison.Ordinal)).ToArray():value.Events.Concat(["lease:invented"]).ToArray()},
            _=>value with{Result=test.Error is null?null:new SourceExtractResult(new("invented",new('0',64),0,"application/json"),[],0,0,0,0)}
        };
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractWriteContractCases.AssertContract(test,Probe(Wrong))));
    }
    public static IEnumerable<object[]> SuccessControls=>SourceExtractWriteContractCases.All.Where(test=>test.Error is null).SelectMany(test=>new[]{"logical","aliases","hits","extractions","dispatch","lookup","publication","raw","rows","index"}.Select(control=>new object[]{test.Id,control}));
    [Theory,MemberData(nameof(SuccessControls))]
    public async Task EachWrongSelectionCachingOrPublicationCountIsRejected(string id,string control)
    {
        var test=SourceExtractWriteContractCases.Get(id);
        SourceExtractWriteObservation Wrong(SourceExtractWriteObservation value)
        {
            var result=value.Result!;
            return control switch
            {
                "logical"=>value with{Result=result with{SelectedLogicalCacheRows=result.SelectedLogicalCacheRows+1}},"aliases"=>value with{Result=result with{UniqueCacheAliases=result.UniqueCacheAliases+1}},
                "hits"=>value with{Result=result with{CacheHits=result.CacheHits+1}},"extractions"=>value with{Result=result with{RawExtractions=result.RawExtractions+1}},
                "dispatch"=>value with{Dispatches=value.Dispatches+1},"lookup"=>value with{Lookups=value.Lookups+1},"publication"=>value with{Publications=value.Publications+1},
                "raw"=>value with{RawReads=SourceExtractWriteFixture.Create(test).WantedExtractions==0?1:0},
                "rows"=>value with{Result=result with{Rows=result.Rows.Count==0?[new(99,0,new("wrong","wrong","wrong",null),ReadOnlyMemory<byte>.Empty,StructuralParseStatus.Clean,[],null,null,null,null,false)]:[]}},
                _=>value with{Result=result with{Index=result.Index with{Sha256=new('9',64)}}}
            };
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractWriteContractCases.AssertContract(test,Probe(Wrong))));
    }
    public static IEnumerable<object[]> RowControls=>SourceExtractWriteContractCases.All.Where(test=>test.Error is null&&test.Limit!=0).SelectMany(test=>
        new[]{"row","length","context","platform","records","status","issues","metadata","raw","hit","receipt","seal"}.Select(control=>new object[]{test.Id,control}));
    [Theory,MemberData(nameof(RowControls))]
    public async Task EveryIndividuallyWrongReturnedLogicalRowIsRejected(string id,string control)
    {
        var test=SourceExtractWriteContractCases.Get(id);
        SourceExtractWriteObservation Wrong(SourceExtractWriteObservation value)
        {
            var result=value.Result!;var row=result.Rows[0];var changed=control switch
            {
                "row"=>row with{RowIndex=99},"length"=>row with{SourceLength=99},"context"=>row with{Context=row.Context with{SnapshotId="catalog-sid"}},
                "platform"=>row with{Context=row.Context with{PlatformVersion="37.2"}},"records"=>row with{RecordsJson=new byte[]{0xff}},
                "status"=>row with{StructuralStatus=StructuralParseStatus.Unsupported},"issues"=>row with{Issues=[new("invented","Invented",null,null)]},
                "metadata"=>row with{Metadata=new()},"raw"=>row with{RawSourceBase64="wrong"},"hit"=>row with{CacheHit=!row.CacheHit},
                "receipt"=>row with{CacheReceipt=null},_=>row with{CacheSeal=null}
            };
            return value with{Result=result with{Rows=result.Rows.Select((item,index)=>index==0?changed:item).ToArray()}};
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractWriteContractCases.AssertContract(test,Probe(Wrong))));
    }
    public static IEnumerable<object[]> StoredFieldControls
    {
        get
        {
            foreach(var test in SourceExtractWriteContractCases.All.Where(test=>test.Error is null))
            {
                foreach(var name in SourceExtractWriteContractCases.ManifestFields)foreach(var missing in new[]{false,true})yield return [test.Id,"manifest",name,missing];
                foreach(var name in SourceExtractWriteContractCases.IndexFields)foreach(var missing in new[]{false,true})yield return [test.Id,"index",name,missing];
                if(test.Limit!=0)foreach(var name in SourceExtractWriteContractCases.IndexedRowFields)foreach(var missing in new[]{false,true})yield return [test.Id,"row",name,missing];
            }
        }
    }
    [Theory,MemberData(nameof(StoredFieldControls))]
    public async Task EveryMissingOrWrongCommittedManifestIndexAndLogicalRowFieldIsRejected(string id,string section,string name,bool missing)
    {
        var test=SourceExtractWriteContractCases.Get(id);
        SourceExtractWriteObservation Wrong(SourceExtractWriteObservation value)
        {
            var result=value.Result!;var files=value.Files.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            var manifestArtifact=files[result.Index.RelativePath];var manifest=JsonNode.Parse(manifestArtifact.Bytes.Span)!.AsObject();
            var dataPath=manifest["IndexData"]!["RelativePath"]!.GetValue<string>();var dataArtifact=files[dataPath];var index=JsonNode.Parse(dataArtifact.Bytes.Span)!.AsObject();
            var target=section=="manifest"?manifest:section=="index"?index:index["Rows"]![0]!.AsObject();
            if(missing)target.Remove(name);
            else
            {
                var current=target[name];target[name]=current switch
                {JsonObject=>new JsonObject(),JsonArray array=>array.Count==0?new JsonArray(new JsonObject()):new JsonArray(),null=>JsonValue.Create("invented"),_=>current is JsonValue scalar&&scalar.TryGetValue<long>(out var number)?JsonValue.Create(number+1):JsonValue.Create("wrong")};
            }
            var canonical=new CanonicalJson();
            if(section!="manifest")
            {
                dataArtifact=dataArtifact with{Bytes=canonical.Serialize(JsonSerializer.SerializeToElement(index))};files[dataPath]=dataArtifact;
                var updated=SourceExtractWriteFixture.Pin(dataArtifact);var oldPath=dataPath;
                manifest["IndexData"]=JsonSerializer.SerializeToNode(updated);
                foreach(var item in manifest["Outputs"]!.AsArray().OfType<JsonObject>())if(item["RelativePath"]!.GetValue<string>()==oldPath)
                {item["Sha256"]=updated.Sha256;item["Length"]=updated.Length;}
            }
            manifestArtifact=manifestArtifact with{Bytes=canonical.Serialize(JsonSerializer.SerializeToElement(manifest))};files[result.Index.RelativePath]=manifestArtifact;
            return value with{Files=files,Result=result with{Index=SourceExtractWriteFixture.Pin(manifestArtifact)}};
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractWriteContractCases.AssertContract(test,Probe(Wrong))));
    }
}
