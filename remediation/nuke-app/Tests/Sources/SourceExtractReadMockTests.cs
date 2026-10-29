using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class SourceExtractReadDependencies(SourceExtractReadFixture fixture, CancellationToken token)
{
    public List<string> Events { get; } = [];
    public int Creates, Disposals, RawReads, Dispatches, Lookups, Writes;
    public bool TokenForwarded { get; private set; } = true;
    public void Check(CancellationToken observed) { TokenForwarded &= observed == token; }
    public IArtifactStore Raw => new Store(this, fixture, true);
    public IArtifactStore Generations => new Store(this, fixture, false);
    public ISourcesRuntimeFactory Runtimes => new Factory(this, fixture);
    public ISealedSourceCache Cache => new CacheProvider(this, fixture);
    private sealed class Store(SourceExtractReadDependencies rig, SourceExtractReadFixture data, bool raw) : IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            rig.Check(cancellationToken); rig.Events.Add(raw ? "raw-read" : "generation-read:" + path); if(raw)rig.RawReads++;
            if(!data.Files.TryGetValue(path,out var file))throw new FileNotFoundException("Literal fixture file is absent",path);
            return Task.FromResult<ReadOnlyMemory<byte>>(file.Bytes.ToArray());
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        { rig.Check(cancellationToken); rig.Writes++; throw new IOException("Reader/resolver cannot publish generation artifacts"); }
    }
    private sealed class Factory(SourceExtractReadDependencies rig, SourceExtractReadFixture data) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile,SourceJavaAdmission? java=null)
        { Assert.Equal(data.Request.Profile,profile); Assert.Equal(data.Request.Java,java);rig.Creates++;rig.Events.Add("factory-create");return new Runtime(rig,data); }
    }
    private sealed class Runtime(SourceExtractReadDependencies rig, SourceExtractReadFixture data) : ISourcesCacheRuntime
    {
        private bool disposed;
        public IReadOnlySet<string> CacheProducerKinds { get; } = new HashSet<string>(data.Case.Noncache || data.Case.Id=="cache-capability" ? [] : [data.Row.Kind!],StringComparer.Ordinal);
        public SourcesRuntimeConcerns Concerns => throw new InvalidOperationException("Reader does not need unrelated concern service composition");
        public ISourcesExtractionDispatcher Dispatcher => new ScriptedSourcesExtractionDispatcher([data.Row.Kind!],(source,kind,scope)=>
        {
            ObjectDisposedException.ThrowIf(disposed,this);Assert.Equal(data.Row.Kind,kind);Assert.Equal(data.Row.Context.Scope,scope);
            Assert.Equal(data.Row.LogicalPath,source.RelativePath);Assert.Equal(data.ExpectedContext,source.Context);Assert.Equal(new byte[]{(byte)'x'},source.Bytes.ToArray());
            rig.Dispatches++;rig.Events.Add("dispatch");
            return new(JsonNode.Parse(data.RecordBytes)!.AsArray().OfType<JsonObject>().Select(row=>(JsonObject)row.DeepClone()).ToArray(),data.ExpectedRow().Issues,data.Case.Status,(JsonObject)data.Metadata.DeepClone());
        });
        public void Dispose(){if(disposed)return;disposed=true;rig.Disposals++;rig.Events.Add("runtime-dispose");}
    }
    private sealed class CacheProvider(SourceExtractReadDependencies rig,SourceExtractReadFixture data) : ISealedSourceCache
    {
        public Task<SourceCacheLookup> LookupAsync(SourceCacheRequest identity,SourceCacheSeal trustedSeal,CancellationToken cancellationToken=default)
        {
            rig.Check(cancellationToken);rig.Lookups++;rig.Events.Add("sealed-lookup");
            Assert.Equal(data.Lookup.Seal,trustedSeal);Assert.Equal(data.Lookup.Receipt.Path,identity.Path);Assert.Equal(data.Row.Kind,identity.Kind);
            Assert.Equal(data.Request.Producer.ProducerId,identity.ProducerId);Assert.Equal(data.Request.Profile.Id,identity.ProfileId);Assert.Equal(data.Request.Projection.RecipeSha256,identity.RecipeSha256);
            Assert.Equal(SourceExtractReadFixture.RawSha,identity.SourceSha256);Assert.Empty(identity.RecordsJson.ToArray());Assert.Equal(data.Request.Producer.RuntimeIdentity,identity.RuntimeIdentity);
            Assert.Equal(data.Request.Producer.ProducerFiles.OrderBy(pair=>pair.Key),identity.ProducerFiles!.OrderBy(pair=>pair.Key));
            if(data.Case.Id=="lookup-infrastructure")throw data.InfrastructureFault;
            return Task.FromResult(data.Lookup with{RecordsJson=data.Lookup.RecordsJson.ToArray()});
        }
        public Task<SourceCachePublication> PublishAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
        {rig.Writes++;throw new IOException("Reader cannot publish cache");}
        public Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
        {rig.Writes++;throw new IOException("Reader cannot use historical Write API");}
        public Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
            =>throw new IOException("Reader must use sealed Lookup, not parsed-record-dependent legacy Read");
    }
}

public sealed class SourceExtractReadProbe(Func<SourceExtractReadFixture,SourceExtractReadDependencies,ISourceExtractGenerationReader> factory,
    Func<SourceExtractReadObservation,SourceExtractReadObservation>? mutation=null) : ISourceExtractReadProbe
{
    public async Task<SourceExtractReadObservation> ReadAsync(SourceExtractReadFixture fixture,CancellationToken cancellationToken)
    {
        var rig=new SourceExtractReadDependencies(fixture,cancellationToken);var original=fixture.Files.ToDictionary(pair=>pair.Key,pair=>pair.Value.Bytes.ToArray(),StringComparer.Ordinal);
        var rows=new List<SourceExtractRow>();Exception? error=null;ISourceExtractGeneration? generation=null;
        try
        {
            var service=factory(fixture,rig);
            if(fixture.Case.DirectResolve)
                rows.Add(await Assert.IsAssignableFrom<ISourceExtractResolver>(service).ResolveAsync(new(fixture.Request.Projection,fixture.Row,fixture.Request.Profile,fixture.Request.Producer,fixture.Request.Java),cancellationToken));
            else
            {
                generation=await service.OpenAsync(fixture.Request,cancellationToken);
                rows.Add(await generation.ReadAsync(fixture.Row,cancellationToken));
                if(fixture.Case.Repeat)rows.Add(await generation.ReadAsync(fixture.Row,cancellationToken));
            }
        }
        catch(Exception failure){error=failure;rows.Clear();}
        finally{generation?.Dispose();}
        var retained=original.Count==fixture.Files.Count&&original.All(pair=>fixture.Files.TryGetValue(pair.Key,out var file)&&pair.Value.AsSpan().SequenceEqual(file.Bytes.Span));
        var observation=new SourceExtractReadObservation(rows,error,rig.Events.ToArray(),rig.Creates,rig.Disposals,rig.RawReads,rig.Dispatches,rig.Lookups,rig.Writes,retained,rig.TokenForwarded);
        return mutation?.Invoke(observation)??observation;
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceExtractReadMockTests
{
    // Branches are fixture-scripted stage responses. This provider does not
    // inspect JSON, validate pins or implement the future admission algorithm.
    private sealed class ScriptedReader(SourceExtractReadFixture data,SourceExtractReadDependencies rig) : ISourceExtractGenerationReader, ISourceExtractResolver
    {
        public async Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request,CancellationToken cancellationToken=default)
        {
            Assert.Equal(data.Request,request);cancellationToken.ThrowIfCancellationRequested();rig.Check(cancellationToken);
            if(request.TrustedIndex is not null)
            {
                _=await rig.Generations.ReadAsync(request.TrustedIndex.RelativePath,cancellationToken);
                if(data.Case.Id.StartsWith("manifest-",StringComparison.Ordinal))throw new SourceExtractException(data.Case.Error!,"Scripted manifest rejection");
                _=await rig.Generations.ReadAsync(data.Manifest.IndexData.RelativePath,cancellationToken);
                if(data.Case.Error is not null&&data.Case.Id is not ("lookup-infrastructure" or "cache-capability"))throw new SourceExtractException(data.Case.Error,"Scripted index/row rejection");
            }
            var runtime=rig.Runtimes.Create(request.Profile,request.Java);
            if(data.Case.Id=="cache-capability"){runtime.Dispose();throw new SourceExtractException(data.Case.Error!,"Scripted descriptor capability rejection");}
            return new Generation(data,rig,runtime);
        }
        public async Task<SourceExtractRow> ResolveAsync(SourceExtractResolveRequest request,CancellationToken cancellationToken=default)
        {
            Assert.Equal(data.Request.Projection,request.Projection);Assert.Equal(data.Row,request.Row);Assert.Equal(data.Request.Profile,request.Profile);Assert.Equal(data.Request.Producer,request.Producer);
            cancellationToken.ThrowIfCancellationRequested();using var generation=new Generation(data,rig,rig.Runtimes.Create(request.Profile,request.Java));
            return await generation.ReadAsync(request.Row,cancellationToken);
        }
        private sealed class Generation(SourceExtractReadFixture data,SourceExtractReadDependencies rig,ISourcesRuntime runtime) : ISourceExtractGeneration
        {
            private SourceCacheLookup? cached;private bool disposed;
            public async Task<SourceExtractRow> ReadAsync(RehydrationSourceRow row,CancellationToken cancellationToken=default)
            {
                ObjectDisposedException.ThrowIf(disposed,this);Assert.Equal(data.Row,row);rig.Check(cancellationToken);
                if(data.Case.Raw)
                {
                    var bytes=await rig.Raw.ReadAsync("raw/API.xml",cancellationToken);
                    if(data.Case.Error is not null)throw new SourceExtractException(data.Case.Error,"Scripted raw-source rejection");
                    var result=runtime.Dispatcher.Extract(new(row.LogicalPath,bytes,data.ExpectedContext),row.Kind,row.Context.Scope);
                    return data.ExpectedRow() with{RecordsJson=JsonSerializer.SerializeToUtf8Bytes(result.Records),StructuralStatus=result.StructuralStatus,Issues=result.Issues,Metadata=result.Metadata};
                }
                cached??=await rig.Cache.LookupAsync(new(data.Lookup.Receipt.Path,row.Kind!,data.Request.Producer.ProducerId,data.Request.Profile.Id,data.Request.Projection.RecipeSha256,
                    SourceExtractReadFixture.RawSha,ReadOnlyMemory<byte>.Empty,ProducerFiles:data.Request.Producer.ProducerFiles,RuntimeIdentity:data.Request.Producer.RuntimeIdentity),data.Lookup.Seal,cancellationToken);
                return data.ExpectedRow() with{RecordsJson=cached.RecordsJson,CacheReceipt=cached.Receipt,CacheSeal=cached.Seal};
            }
            public void Dispose(){if(disposed)return;disposed=true;runtime.Dispose();}
        }
    }
    private static ISourceExtractReadProbe Probe(Func<SourceExtractReadObservation,SourceExtractReadObservation>? wrong=null) => new SourceExtractReadProbe((data,rig)=>new ScriptedReader(data,rig),wrong);
    [Theory,MemberData(nameof(SourceExtractReadContractCases.Cases),MemberType=typeof(SourceExtractReadContractCases))]
    public Task CorrectMocksExerciseAdmissionLookupRawFallbackContextAndOwnershipAssertions(string id)=>SourceExtractReadContractCases.AssertContract(SourceExtractReadContractCases.Get(id),Probe());

    public static IEnumerable<object[]> Controls=>SourceExtractReadContractCases.All.SelectMany(test=>new[]{"writes","retention","token","creates","disposals","raw-reads","dispatches","lookups","error","rows"}.Select(control=>new object[]{test.Id,control}));
    [Theory,MemberData(nameof(Controls))]
    public async Task EachWrongInteractionFailureOrOwnershipResponseIsRejected(string id,string control)
    {
        var test=SourceExtractReadContractCases.Get(id);
        SourceExtractReadObservation Wrong(SourceExtractReadObservation good)=>control switch
        {
            "writes"=>good with{Writes=1},"retention"=>good with{OriginalsRetained=false},"token"=>good with{TokenForwarded=false},
            "creates"=>good with{RuntimeCreates=good.RuntimeCreates+1},"disposals"=>good with{RuntimeDisposals=good.RuntimeDisposals+1},
            "raw-reads"=>good with{RawReads=good.RawReads+1},"dispatches"=>good with{Dispatches=good.Dispatches+1},"lookups"=>good with{CacheLookups=good.CacheLookups+1},
            "error"=>good with{Error=test.Error is null?new IOException("Invented"):test.Error=="infrastructure"?new IOException("Replaced"):test.Error=="cancelled"?new SourceExtractException("wrong","Wrong cancellation"):new SourceExtractException("wrong","Wrong diagnostic")},
            _=>good with{Rows=test.Error is null?[]:[SourceExtractReadFixture.Create(test).ExpectedRow()]}
        };
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractReadContractCases.AssertContract(test,Probe(Wrong))));
    }
    public static IEnumerable<object[]> RowControls=>SourceExtractReadContractCases.All.Where(test=>test.Error is null).SelectMany(test=>new[]{"row","length","context","platform","records","status","issues","metadata","raw","hit","receipt","seal"}.Select(control=>new object[]{test.Id,control}));
    [Theory,MemberData(nameof(RowControls))]
    public async Task EachWrongNativeRowFieldIsRejected(string id,string control)
    {
        var test=SourceExtractReadContractCases.Get(id);
        SourceExtractReadObservation Wrong(SourceExtractReadObservation good)
        {
            var row=good.Rows[0];var changed=control switch
            {
                "row"=>row with{RowIndex=99},"length"=>row with{SourceLength=99},"context"=>row with{Context=row.Context with{SnapshotId="catalog-derived-sid"}},
                "platform"=>row with{Context=row.Context with{PlatformVersion="37.2"}},"records"=>row with{RecordsJson=new byte[]{0xff}},
                "status"=>row with{StructuralStatus=row.StructuralStatus==StructuralParseStatus.Clean?StructuralParseStatus.Unsupported:StructuralParseStatus.Clean},
                "issues"=>row with{Issues=[new("invented","Invented",null,null)]},"metadata"=>row with{Metadata=new()},"raw"=>row with{RawSourceBase64="wrong"},
                "hit"=>row with{CacheHit=!row.CacheHit},"receipt"=>row with{CacheReceipt=row.CacheReceipt is null?SourceExtractReadFixture.Create(test).Lookup.Receipt:null},
                _=>row with{CacheSeal=row.CacheSeal is null?SourceExtractReadFixture.Create(test).Lookup.Seal:null}
            };
            return good with{Rows=good.Rows.Select((value,index)=>index==0?changed:value).ToArray()};
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>SourceExtractReadContractCases.AssertContract(test,Probe(Wrong))));
    }
}
