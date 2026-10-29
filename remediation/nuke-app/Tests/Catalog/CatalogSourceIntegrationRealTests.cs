using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogSourceIntegrationProvider:ICatalogSourceIntegrationCaseProvider
{
    internal static NativeCatalogBuildProvider CreateProvider(CatalogSourceIntegrationCall call,ISourcesExtractionDispatcher dispatcher,
        Func<CatalogBuildInvocation,Task<CatalogBuildException?>>? execution=null)
    {
        var cached=call.Variant=="java-jvm-link" && call.TrustedCacheInput;
        if(cached && execution is not null)throw new InvalidOperationException("The raw Catalog CLI fixture must explicitly select its raw route.");
        var forbidden=new ForbiddenIntakeDispatcher(dispatcher.SupportedKinds);
        return new NativeCatalogBuildProvider
        {
            SourceDispatcher=cached ? forbidden : dispatcher,
            Execution=execution,
            SourceValues=values=>
            {
                if(call.Variant=="structured-aidl") values[2]=("aidl","IFoo.aidl",CatalogSourceIntegrationCases.Structured);
                if(call.Variant=="malformed-aidl") values[2]=("aidl","IFoo.aidl",CatalogSourceIntegrationCases.Malformed);
                if(call.Variant=="java-jvm-link"){values.Add(("java-source","C.java",CatalogSourceIntegrationCases.Java));values.Add(("sdk-api-index","api.xml",CatalogSourceIntegrationCases.Index));}
            },
            InventorySetup=(inventory,root,originals)=>
            {
                if(call.Variant=="java-cache-sha-mismatch")
                {
                    var source=inventory["sources"]![0]!;source["kind"]="java-source";var bytes=Encoding.UTF8.GetBytes("broken");File.WriteAllBytes(Path.Combine(root,"bad.gz"),bytes);originals.Add("bad.gz",bytes);
                    source["metadata_cache"]=new JsonObject{["path"]="bad.gz",["sha256"]=new string('0',64),["records"]=0};
                }
                if(call.Variant=="java-jvm-link") foreach(var source in inventory["sources"]!.AsArray().OfType<JsonObject>().Where(source=>source["path"]!.GetValue<string>() is "C.java" or "api.xml"))
                {
                    var path=source["path"]!.GetValue<string>();source["platform_api_version"]="37.2";source["url"]="https://dl.google.com/mock/"+path;
                }
            },
            IntakeFactory=cached ? async fixture=>
            {
                var java=JavaAdmission();var factory=new SourcesRuntimeFactory(new JavaSourceRuntimeLoader());
                var prepared=await NativeCatalogSourceCacheInput.CreateAsync(fixture,java,factory,[3,4]);
                var canonical=new CanonicalJson();var raw=new CacheAvoidingRawStore(new FileArtifactStore(fixture.Root,bytes=>ContentHash.Sha256(bytes.Span)),new HashSet<string>(["C.java","api.xml"],StringComparer.Ordinal));
                var cache=new SourceCache(prepared.Generations,prepared.Leases,canonical);
                return new CatalogSourceIntake(new SourcesExtractService(raw,prepared.Generations,factory,cache,canonical),prepared.Request);
            } : null
        };
    }
    private static SourceJavaAdmission JavaAdmission()=>new(Environment.GetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST") ?? throw new InvalidOperationException("Selected pinned Java native manifest required for the original Java/JVM integration case"));
    public CatalogSourceIntegrationReply Import(CatalogSourceIntegrationCall call)
    {
        var java=call.Variant=="java-jvm-link" ? JavaAdmission() : null;
        using var runtime=new SourcesRuntimeFactory(new JavaSourceRuntimeLoader()).Create(SourceExtractionProfile.CurrentCandidate,java);
        using var provider=CreateProvider(call,runtime.Dispatcher);
        var result=provider.Build(new("source-integration"));return Observe(call,provider,result);
    }
    internal static CatalogSourceIntegrationReply Observe(CatalogSourceIntegrationCall call,NativeCatalogBuildProvider provider,CatalogBuildReply result)
    {
        var output=Path.Combine(provider.EvidenceRoot,"out");
        if(result.Outcome!="ok")
        {var failure=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation-failure.json")))!;return new(result.Outcome,result.FinalDatabase,FailureReason:failure["error"]!.GetValue<string>(),OriginalsRetained:result.OriginalInputsRetained);}
        var docs=File.ReadLines(Path.Combine(output,"entries.jsonl")).Where(line=>line.Length!=0).Select(line=>JsonNode.Parse(line)!.AsObject()).ToArray();
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(output,"catalog.sqlite"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();
        int Count(string query){using var command=connection.CreateCommand();command.CommandText=query;return (int)(long)command.ExecuteScalar()!;}
        if(call.Variant=="structured-aidl")
        {
            var fields=docs.Where(doc=>doc["canonical_signature"]!.GetValue<string>() is "aidl:a.P#name" or "aidl:a.P#values").ToArray();var type=docs.Single(doc=>doc["canonical_signature"]!.GetValue<string>()=="aidl:a.P");
            return new("ok",result.FinalDatabase,Sources:result.Sources,StructuredFields:fields.Length,FieldWireTagsUnknown:fields.All(field=>field["extensions"]!.AsObject().ContainsKey("wire_tag") && field["extensions"]!["wire_tag"] is null),
                RootKind:type["kind"]!.GetValue<string>(),RootSchemaVersion:type["schema_version"]!.GetValue<string>(),NoStructuredUnresolved:!type["unresolved"]!.AsArray().Any(value=>value!.GetValue<string>().Contains("Structured AIDL",StringComparison.Ordinal)),
                AidlSemanticGaps:Count("SELECT count(*) FROM issues WHERE category='aidl-semantic-gap'"),OriginalsRetained:result.OriginalInputsRetained);
        }
        if(call.Variant=="java-jvm-link")
        {
            var source=docs.Single(doc=>doc["canonical_signature"]!.GetValue<string>()=="java-source:a.C#f(int)");var jvm=docs.Single(doc=>doc["canonical_signature"]!.GetValue<string>()=="jvm:a/C#f(I)V");
            int? hits=null;bool? pinned=null;bool? noRaw=null;
            if(call.TrustedCacheInput)
            {
                using var query=connection.CreateCommand();query.CommandText="SELECT document_json FROM source_details WHERE source_id IN (SELECT source_id FROM sources WHERE kind IN ('java-source','sdk-api-index'))";
                using var rows=query.ExecuteReader();var details=new List<JsonObject>();while(rows.Read())details.Add(JsonNode.Parse(rows.GetString(0))!.AsObject());
                hits=details.Count(value=>value["x-native-source-intake"]?["cache_hit"]?.GetValue<bool>()==true);
                pinned=details.Count==2 && details.All(value=>
                {
                    var intake=value["x-native-source-intake"]!;var producer=intake["producer"]!.Deserialize<SourceExtractProducerIdentity>()!;
                    var receipt=intake["cache_receipt"]!.Deserialize<SourceCacheReceipt>()!;var seal=intake["cache_seal"]!.Deserialize<SourceCacheSeal>()!;
                    return producer.ProducerId=="csharp-native-fixture-generation-v1" && receipt.ProducerId==producer.ProducerId && receipt.RecordCount>0 &&
                        receipt.RecordCount==seal.RecordCount && receipt.SourceSha256==value["sha256"]!.GetValue<string>() &&
                        producer.ProducerFiles["native/assembly"]==ContentHash.Sha256(File.ReadAllBytes(typeof(SourcesExtractService).Assembly.Location)) &&
                        producer.ParserIdentity==ContentHash.Sha256(File.ReadAllBytes(JavaAdmission().ManifestPath)) &&
                        !intake["historical_identity_claimed"]!.GetValue<bool>();
                });
                // CacheAvoidingRawStore throws on either cached source path.
                // An admitted successful hit proves the consumer avoided it.
                noRaw=hits==2 && pinned==true;
            }
            return new("ok",result.FinalDatabase,Sources:result.Sources,JavaPermission:source["permission"]!["name"]!.GetValue<string>(),JvmPermission:jvm["permission"]!["name"]!.GetValue<string>(),
                ProvenJvmIdentity:source["extensions"]!["proven_current_sdk_jvm_identity"]!.GetValue<string>(),DistinctSourceJvm:source["entry_id"]!.GetValue<string>()!=jvm["entry_id"]!.GetValue<string>(),OriginalsRetained:result.OriginalInputsRetained,
                TrustedCacheHits:hits,NativeProducerIdentityPinned:pinned,CacheConsumerAvoidedRawExtraction:noRaw);
        }
        using var schema=connection.CreateCommand();schema.CommandText="SELECT raw_schema,parse_status FROM aidl_schemas WHERE source_id=(SELECT source_id FROM sources WHERE path='IFoo.aidl')";using var row=schema.ExecuteReader();Assert.True(row.Read());
        var raw=row.GetString(0);var status=row.GetString(1);Assert.False(row.Read());row.Close();
        return new("ok",result.FinalDatabase,Sources:result.Sources,RawSchema:raw,ParseStatus:status,AnyIssues:Count("SELECT count(*) FROM issues")>0,OriginalsRetained:result.OriginalInputsRetained);
    }
    private sealed class CacheAvoidingRawStore(IArtifactStore inner,IReadOnlySet<string> cached):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)=>cached.Contains(path)
            ? throw new InvalidOperationException("Catalog reread parser-produced cached raw source: "+path) : inner.ReadAsync(path,token);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>throw new InvalidOperationException("Consumer fixture source store is read-only");
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogSourceIntegrationRealTests
{
    [Theory,MemberData(nameof(CatalogSourceIntegrationCases.All),MemberType=typeof(CatalogSourceIntegrationCases))]
    public void NativeImportUsesSameFourOriginalSourceIntegrationAssertions(CatalogSourceIntegrationCase test)=>CatalogSourceIntegrationCases.AssertCase(test,new NativeCatalogSourceIntegrationProvider());
}
