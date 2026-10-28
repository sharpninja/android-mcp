using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Tests.Sources;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogSourceIntakeProvider:ICatalogSourceIntakeCaseProvider
{
    internal CatalogIntakeHistoricalAuditReply? HistoricalAudit {get;private set;}
    public async Task<CatalogSourceIntakeReply> ImportAsync(CatalogSourceIntakeCall call)
    {
        var java=new SourceJavaAdmission(Environment.GetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST") ?? throw new InvalidOperationException("Selected pinned Java native manifest required"));
        var factory=new SourcesRuntimeFactory(new JavaSourceRuntimeLoader());using var concerns=factory.Create(SourceExtractionProfile.CurrentCandidate,java);
        var forbidden=new ForbiddenIntakeDispatcher(concerns.Dispatcher.SupportedKinds);var observations=new List<CatalogImportObservation>();
        ObservedIntakeReader? reader=null;ObservedIntakeRawStore? rawStore=null;SourceExtractGenerationRequest? request=null;var hitPaths=new List<string>();
        using var provider=new NativeCatalogBuildProvider
        {
            SourceDispatcher=forbidden,ImportObserver=observations.Add,
            SourceValues=values=>
            {
                values.Add(("java-source","C.java",CatalogSourceIntegrationCases.Java));
                var index=call.Variant is "diagnostic-hit" or "missing-raw-evidence" ? CatalogSourceIntegrationCases.Index.Replace("f(I)V","bad(I",StringComparison.Ordinal) : CatalogSourceIntegrationCases.Index;
                values.Add(("sdk-api-index","api.xml",index));
                if(call.Variant=="logical-alias") values.Add(("sdk-api-index","api-alias.xml",index));
            },
            InventorySetup=(inventory,_,_)=>
            {
                foreach(var source in inventory["sources"]!.AsArray().OfType<JsonObject>().Where(source=>source["kind"]!.GetValue<string>() is "java-source" or "sdk-api-index")) source["platform_api_version"]="37.2";
                var cached=CachedRows(call.Variant);
                if(call.Variant=="declared-historical")
                {
                    var source=inventory["sources"]![3]!;source["metadata_cache"]=new JsonObject{["path"]="historical/missing-java-cache.gz",["sha256"]=new string('9',64),["parser_sha256"]=new string('8',64),
                        ["source_sha256"]=source["sha256"]!.DeepClone(),["kind"]="java-source",["records"]=999};
                }
                foreach(var index in cached)
                {
                    var source=inventory["sources"]![index]!;source["local_path"]=source["kind"]!.GetValue<string>()=="java-source" ? "missing raw/C.java" : "missing raw/api.xml";
                }
            },
            IntakeFactory=async fixture=>
            {
                var canonical=new CanonicalJson();var producer=NativeSourceCacheGenerationFixture.Producer(java);
                var files=new JsonObject();foreach(var file in producer.ProducerFiles) files[file.Key]=file.Value;
                var recipe=new JsonObject{["recipe_version"]=1,["release"]="native-catalog-intake-fixture",["inventory"]=fixture.Inventory.DeepClone(),["producer"]=new JsonObject{["path"]="native-fixture-producer",["files"]=files}};
                var recipeBytes=canonical.Serialize(JsonSerializer.SerializeToElement(recipe));
                var locked=new JsonObject{["recipe_file"]="recipe.json",["sha256"]=ContentHash.Sha256(recipeBytes),["source_rows"]=fixture.Inventory["sources"]!.AsArray().Count,["producer_files"]=producer.ProducerFiles.Count};
                var planned=new RehydrationPlanner(canonical).Plan(new(new("recipe.json",recipeBytes,"application/json"),new("recipe.lock.json",canonical.Serialize(JsonSerializer.SerializeToElement(locked)),"application/json")));
                Assert.True(planned.IsValid,JsonSerializer.Serialize(planned.Issues));var projection=planned.Plan!.SourceProjection!;
                request=new(projection,SourceExtractionProfile.CurrentCandidate,producer,Java:java);
                var generationStore=new FileArtifactStore(Path.Combine(fixture.Root,"source generation"),Hash);var leases=new FileArtifactLeaseProvider(Path.Combine(fixture.Root,"source generation locks"));
                var producerRaw=new FileArtifactStore(Path.Combine(fixture.Root,"producer raw originals"),Hash);
                foreach(var physical in projection.PhysicalSources)
                {
                    var source=projection.Rows.First(row=>row.PhysicalSourceIndex==physical.PhysicalSourceIndex);
                    var originalPath=source.OriginalRow.GetProperty("path").GetString()!;
                    await producerRaw.WriteAsync(new(physical.Pin.LocalPath,fixture.Originals[originalPath],"application/octet-stream"));
                }
                var cached=CachedRows(call.Variant);
                if(cached.Count!=0)
                {
                    var prepared=await NativeSourceCacheGenerationFixture.CreateAsync(request,producerRaw,generationStore,leases,factory,cached,"prepared-native-input",
                        indexMutation:call.Variant=="missing-raw-evidence" ? index=>index with{Rows=index.Rows.Select(row=>row.Kind=="sdk-api-index" ? row with{RawSourceBase64=null} : row).ToArray()} : null);
                    request=prepared.Request;
                    foreach(var row in cached) hitPaths.Add(projection.PhysicalSources.Single(physical=>physical.PhysicalSourceIndex==projection.Rows.Single(source=>source.RowIndex==row).PhysicalSourceIndex).Pin.LocalPath);
                }
                if(call.Variant=="index-pin") request=request with{TrustedIndex=request.TrustedIndex! with{Sha256=new string('0',64)}};
                if(call.Variant=="edited-row")
                {
                    var before=await File.ReadAllBytesAsync(fixture.InventoryPath);var edited=JsonNode.Parse(before)!;edited["sources"]![0]!["revision"]="explicitly edited inventory row";
                    var after=canonical.Serialize(JsonSerializer.SerializeToElement(edited));var retained=Path.Combine(fixture.Root,"inventory.before-edit.json");await File.WriteAllBytesAsync(retained,before);fixture.Originals.Add("inventory.before-edit.json",before);
                    await File.WriteAllBytesAsync(fixture.InventoryPath,after);fixture.Originals["inventory.json"]=after;
                }
                rawStore=new(new FileArtifactStore(fixture.Root,Hash));var cache=new SourceCache(generationStore,leases,canonical);
                reader=new(new SourcesExtractService(rawStore,generationStore,factory,cache,canonical),fixture.OutputDirectory,fixture.StageDirectory,call.Variant=="invalid-record-shape");
                return new(reader,request);
            }
        };
        // All fixture preparation and the actual service call use the shared
        // provider. This async yield does not run an additional producer.
        await Task.CompletedTask;var result=provider.Build(new("source-intake"));Assert.NotNull(reader);Assert.NotNull(rawStore);Assert.NotNull(request);
        var rawPath=observations.FirstOrDefault(value=>value.Event=="raw-stage")?.StagePath;
        int Count(string path,string sql){using var database=CatalogDatabase.OpenExisting(path);using var query=database.Command(sql);return (int)(long)query.ExecuteScalar()!;}
        var captured=rawPath is not null && File.Exists(rawPath) ? Count(rawPath,"SELECT count(*) FROM source_records") : 0;
        var output=Path.Combine(provider.EvidenceRoot,"out");var stage=Path.Combine(provider.EvidenceRoot,"private-stage");var final=Path.Combine(output,"catalog.sqlite");
        var success=result.Outcome=="ok";bool? context=null,separate=null,audit=null,lossless=null;
        string? historicalDisposition=null;var historicalRetained=false;var historicalBodyAbsent=false;var nativeReceiptSeparate=false;var historicalIdentityNotClaimed=false;
        if(success)
        {
            context=true;separate=true;audit=true;lossless=true;
            using var database=CatalogDatabase.OpenExisting(final);using var query=database.Command("SELECT source_id,document_json FROM source_details");using var rows=query.ExecuteReader();
            while(rows.Read())
            {
                var source=JsonNode.Parse(rows.GetString(1))!;var details=source["x-native-source-intake"]!;var saved=details["context"]!.Deserialize<SourceContext>()!;
                var projected=request.Projection.Rows.Single(row=>row.LogicalPath==source["path"]!.GetValue<string>());
                context &= saved.Repository==projected.Context.Repository && saved.Revision==projected.Context.Revision && saved.SnapshotId==request.Projection.RecipeSha256 && saved.PlatformVersion==source["platform_api_version"]?.GetValue<string>();
                separate &= rows.GetString(0)!=saved.SnapshotId;
                audit &= details["profile_id"]!.GetValue<string>()==request.Profile.Id && JsonNode.DeepEquals(details["producer"],JsonSerializer.SerializeToNode(request.Producer));
                if(details["cache_hit"]!.GetValue<bool>()) audit &= details["cache_receipt"] is not null && details["cache_seal"] is not null;
                if(call.Variant=="declared-historical" && source["metadata_cache"] is JsonObject historical)
                {
                    historicalDisposition=details["historical_cache_disposition"]!.GetValue<string>();historicalRetained=JsonNode.DeepEquals(historical,details["historical_cache"]);
                    historicalBodyAbsent=!File.Exists(ArtifactPaths.Resolve(provider.EvidenceRoot,historical["path"]!.GetValue<string>()));
                    var receipt=details["cache_receipt"]!.Deserialize<SourceCacheReceipt>()!;
                    nativeReceiptSeparate=receipt.Path!=historical["path"]!.GetValue<string>() && receipt.ProducerId==request.Producer.ProducerId && receipt.RecordCount!=historical["records"]!.GetValue<int>();
                    historicalIdentityNotClaimed=!details["historical_records_consumed"]!.GetValue<bool>() && !details["historical_identity_claimed"]!.GetValue<bool>() && source["x-native-cache-audit"] is null;
                }
            }
            rows.Close();
            if(call.Variant=="diagnostic-hit")
            {
                using var issue=database.Command("SELECT raw FROM issues WHERE category='sdk-descriptor'");var saved=(string)issue.ExecuteScalar()!;
                var wanted=Encoding.UTF8.GetBytes(CatalogSourceIntegrationCases.Index.Replace("f(I)V","bad(I",StringComparison.Ordinal));lossless=Convert.FromBase64String(saved).AsSpan().SequenceEqual(wanted);
            }
        }
        var observed=new CatalogSourceIntakeReply(success ? result : null,success ? null : result.Outcome,reader.Opens,reader.Resolutions,reader.Hits,rawStore.Reads,captured,reader.BeforeEffects,
            hitPaths.All(path=>!File.Exists(ArtifactPaths.Resolve(provider.EvidenceRoot,path))),forbidden.Calls==0,context,separate,audit,lossless,reader.Disposed,result.OriginalInputsRetained,
            !Directory.Exists(output) && !Directory.Exists(stage),File.Exists(final));
        if(call.Variant=="declared-historical") HistoricalAudit=new(observed,historicalDisposition ?? "missing-audit",historicalRetained,historicalBodyAbsent,nativeReceiptSeparate,historicalIdentityNotClaimed);
        return observed;
    }
    private static IReadOnlyList<int> CachedRows(string variant)=>variant switch{"no-index"=>[],"one-hit"=>[3],"logical-alias"=>[3,4,5],_=>[3,4]};
    private static string Hash(ReadOnlyMemory<byte> bytes)=>ContentHash.Sha256(bytes.Span);
}
public sealed class ForbiddenIntakeDispatcher(IReadOnlyList<string> supported):ISourcesExtractionDispatcher
{
    public int Calls {get;private set;}
    public IReadOnlyList<string> SupportedKinds=>supported;
    public ExtractionResult Extract(SourceDocument source,string? kind,string? scope=null){Calls++;throw new InvalidOperationException("Catalog must use the admitted facade rather than its direct dispatcher");}
}
public sealed class ObservedIntakeRawStore(IArtifactStore inner):IArtifactStore
{
    public int Reads {get;private set;}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default){Reads++;return inner.ReadAsync(path,token);}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>throw new InvalidOperationException("Catalog intake raw source store is read-only");
}
public sealed class ObservedIntakeReader(ISourceExtractGenerationReader inner,string output,string stage,bool corruptRecords):ISourceExtractGenerationReader
{
    private readonly bool corrupt=corruptRecords;
    public int Opens {get;private set;}public int Resolutions {get;private set;}public int Hits {get;private set;}public bool BeforeEffects {get;private set;}public bool Disposed {get;private set;}
    public async Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request,CancellationToken token=default)
    {Opens++;BeforeEffects=!Directory.Exists(output) && !Directory.Exists(stage);return new ObservedGeneration(this,await inner.OpenAsync(request,token));}
    private sealed class ObservedGeneration(ObservedIntakeReader owner,ISourceExtractGeneration inner):ISourceExtractGeneration
    {
        public async Task<SourceExtractRow> ReadAsync(RehydrationSourceRow row,CancellationToken token=default)
        {owner.Resolutions++;var result=await inner.ReadAsync(row,token);if(result.CacheHit)owner.Hits++;return owner.corrupt ? result with{RecordsJson=Encoding.UTF8.GetBytes("[null]")} : result;}
        public void Dispose(){owner.Disposed=true;inner.Dispose();}
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourceIntakeRealTests
{
    [Theory,MemberData(nameof(CatalogSourceIntakeCases.All),MemberType=typeof(CatalogSourceIntakeCases))]
    public Task NativeSharedSourceReaderFeedsSameNineCatalogAssertions(CatalogSourceIntakeCase test)=>CatalogSourceIntakeCases.AssertCase(test,new NativeCatalogSourceIntakeProvider());
}
