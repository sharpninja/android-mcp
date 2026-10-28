using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Core;
using AndroidMcp.BuildTool.Tests.Publication;
using AndroidMcp.BuildTool.Tests.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogPipelineWorkflowProvider:ICatalogPipelineWorkflowCaseProvider
{
    private sealed class Counts { public int Outer,Recipe,Planner,Inventory,Generation,Producer; }
    private static string Sha(ReadOnlyMemory<byte> bytes)=>ContentHash.Sha256(bytes.Span);
    public async Task<CatalogPipelineWorkflowReply> AdmitAsync(CatalogPipelineWorkflowCall call)
    {
        var count=new Counts();
        if(call.Variant=="legacy-and-pipeline")
        {
            var planned=new OperationPlanner(new OperationRegistry()).Plan(PlannerContractCases.Request("Catalog","Build",
                ("project-root","fixture"),("source-manifest","legacy.json"),("output-root","out"),("stage-root","stage"),
                ("pipeline-request","workflow.json"),("pipeline-request-sha256",new string('0',64)),("pipeline-request-length","1")));
            Assert.False(planned.IsValid);Assert.Contains(planned.Errors,error=>error.Code=="UNSUPPORTED_PARAMETER" && error.Parameter=="pipeline-request");
            return Reply("catalog-input-mode",count,"",true,true,true,true,true,true);
        }
        var root=Path.Combine(Path.GetTempPath(),"native-catalog-workflow-"+Guid.NewGuid().ToString("N"));
        var project=Path.Combine(root,"project with spaces");var rawRoot=Path.Combine(root,"raw role");var inventoryRoot=Path.Combine(root,"inventory role");
        var generationRoot=Path.Combine(root,"generation role");var output=Path.Combine(root,"catalog output");var stage=Path.Combine(root,"private stage");
        var canonical=new CanonicalJson();
        FileArtifactStore Store(string path)=>new(path,Sha);
        var projectStore=Store(project);var rawStore=Store(rawRoot);var bundle=Store(Path.Combine(root,"fixture bundle"));
        var inventories=Store(inventoryRoot);var generations=Store(generationRoot);
        var bytes=Encoding.UTF8.GetBytes(SdkXmlContractCases.Get("SDK-02").Text);var other=bytes.Concat(new byte[]{10}).ToArray();
        var hashes=new[]{Sha(bytes),Sha(other)};var none=call.Variant=="none-selected";
        var recipe=RehydrationContractCases.Request(spec=>
        {
            var rows=new JsonArray();
            for(var index=0;index<3;index++)
            {
                var physical=index<2?0:1;var data=physical==0?bytes:other;
                var row=new JsonObject{["path"]="logical/"+index+".xml",["local_path"]=physical==0?"raw/shared.xml":"raw/other.xml",
                    ["kind"]="sdk-api-index",["sha256"]=hashes[physical],["size_bytes"]=data.Length,["status"]=index<2?"failed":"downloaded",
                    ["url"]="https://example.invalid/fixture/"+physical,["revision"]=new string('1',40),["repository"]="fixture-repository",
                    ["platform_api_version"]="37.2",["transport"]=new JsonObject{["immutable_url"]=false,["required_local_snapshot"]=physical==0?"snapshots/shared.xml":"snapshots/other.xml"}};
                if(index<2)row["metadata_cache"]=new JsonObject{["path"]="historical/shared.gz",["sha256"]=new string('1',64),
                    ["parser_sha256"]=new string('2',64),["source_sha256"]=hashes[0],["records"]=999};
                rows.Add(row);
            }
            spec["inventory"]!["sources"]=rows;
        },limit:none?0:1);
        var actualPlanner=new RehydrationPlanner(canonical);var preparation=actualPlanner.Plan(recipe);Assert.True(preparation.IsValid);
        var projection=preparation.Plan!.SourceProjection!;
        var recipePin=await projectStore.WriteAsync(recipe.Recipe);var lockPin=await projectStore.WriteAsync(recipe.Lock);
        await rawStore.WriteAsync(new("raw/shared.xml",bytes,"application/octet-stream"));await rawStore.WriteAsync(new("raw/other.xml",other,"application/octet-stream"));
        await bundle.WriteAsync(new("snapshots/shared.xml",bytes,"application/octet-stream"));await bundle.WriteAsync(new("snapshots/other.xml",other,"application/octet-stream"));
        var batch=new SourceBatch(new NoNetwork(),rawStore,bundle,new FileArtifactLeaseProvider(rawRoot),canonical);
        var acquired=await batch.RunAsync(new("native-workflow-acquisition","sources/acquisition.json",projection));
        var inventory=await new SourcesInventoryOperation(actualPlanner,batch,rawStore,inventories,canonical).RunAsync(new(recipe,acquired.Manifest));
        var factory=new SourcesRuntimeFactory();var identities=new SourceProducerIdentityBuilder();var producer=identities.Create();
        var leases=new FileArtifactLeaseProvider(generationRoot);var cache=new SourceCache(generations,leases,canonical);
        var source=new SourcesExtractService(rawStore,generations,factory,cache,canonical,leases);
        // This is a real writer commit and native SDK extraction, not a fixture
        // envelope or a fabricated producer. Existing raw inputs are retained.
        var extracted=await source.ExtractAsync(new(projection,"native-generation-001",SourceExtractionProfile.CurrentCandidate,producer.Identity));
        var inventoryPin=inventory.Manifest;
        if(call.Variant=="foreign-inventory")
        {
            var foreign=inventory.Receipt with{Acquisition=inventory.Receipt.Acquisition with{Receipt=inventory.Receipt.Acquisition.Receipt with{
                Projection=projection with{RecipeSha256=new string('f',64)}}}};
            inventoryPin=await inventories.WriteAsync(new("sources/foreign-inventory.json",canonical.Serialize(JsonSerializer.SerializeToElement(foreign)),"application/json"));
        }
        var metadataRows=projection.Rows.Where(row=>projection.SelectedPhysicalSourceIndices.Contains(row.PhysicalSourceIndex))
            .Select(row=>new CatalogSourceNormalization(row.RowIndex,canonical.Hash(row.OriginalRow),37,"development",RetrievedAt:"2026-10-28T00:00:00Z")).ToArray();
        var metadata=new CatalogPipelineMetadata(1,"authored fixture",37,"selected fixture",metadataRows);
        var file=new CatalogPipelineWorkflowFile(new(recipePin,lockPin,"fixture-release",none?0:1),new(inventoryRoot,rawRoot,generationRoot),
            inventoryPin,extracted.Index,metadata,NativeSchemaProvider.Root(),output,stage);
        if(call.Variant=="wrong-recipe-pin")file=file with{Sources=file.Sources with{Recipe=file.Sources.Recipe with{Sha256=new string('0',64)}}};
        if(call.Variant=="wrong-lock-pin")file=file with{Sources=file.Sources with{RecipeLock=file.Sources.RecipeLock with{Sha256=new string('0',64)}}};
        if(call.Variant=="wrong-generation-pin")file=file with{SourceGenerationCommit=file.SourceGenerationCommit with{Sha256=new string('0',64)}};
        var document=JsonSerializer.SerializeToNode(file)!.AsObject();
        if(call.Variant=="forged-projection")document["Projection"]=JsonSerializer.SerializeToNode(projection);
        if(call.Variant=="forged-producer")document["Producer"]=JsonSerializer.SerializeToNode(producer.Identity);
        var workflowBytes=canonical.Serialize(JsonSerializer.SerializeToElement(document));
        var outer=await projectStore.WriteAsync(new("workflow/catalog.json",workflowBytes,"application/json"));
        var originals=new Dictionary<string,byte[]>(StringComparer.Ordinal)
        {
            [ArtifactPaths.Resolve(project,recipePin.RelativePath)]=recipe.Recipe.Bytes.ToArray(),[ArtifactPaths.Resolve(project,lockPin.RelativePath)]=recipe.Lock.Bytes.ToArray(),
            [ArtifactPaths.Resolve(project,outer.RelativePath)]=workflowBytes,[ArtifactPaths.Resolve(rawRoot,"raw/shared.xml")]=bytes,
            [ArtifactPaths.Resolve(rawRoot,"raw/other.xml")]=other,[ArtifactPaths.Resolve(inventoryRoot,inventoryPin.RelativePath)]=(await inventories.ReadAsync(inventoryPin.RelativePath)).ToArray(),
            [ArtifactPaths.Resolve(generationRoot,extracted.Index.RelativePath)]=(await generations.ReadAsync(extracted.Index.RelativePath)).ToArray()
        };
        if(call.Variant=="wrong-outer-sha")outer=outer with{Sha256=new string('0',64)};
        if(call.Variant=="wrong-outer-length")outer=outer with{Length=outer.Length+1};
        var beforeProjection=canonical.Serialize(JsonSerializer.SerializeToElement(projection));
        var observedPins=new ObservedPins(new PinnedArtifactReader(projectStore),count);
        var reader=new CatalogPipelineWorkflowReader(observedPins,new SourceWorkflowInputsReader(observedPins),canonical);
        var observedInventory=new ObservedInventory(new SourcesInventoryReader(new PinnedArtifactReader(inventories),batch,canonical),count);
        var observedSource=new ObservedGeneration(source,count);
        var outcome="ok";var selected="";var originalRows=true;var contexts=true;var audit=true;var roots=true;
        try
        {
            var admitted=await reader.ReadAsync(outer);
            roots=admitted.File.CatalogRoot==NativeSchemaProvider.Root() && admitted.File.OutputRoot==output && admitted.File.StageRoot==stage &&
                admitted.File.Stores.InventoryRoot==inventoryRoot && admitted.File.Stores.AcquisitionRawRoot==rawRoot && admitted.File.Stores.GenerationStoreRoot==generationRoot;
            var operation=new CatalogPipelineWorkflowOperation(admitted.File.CatalogRoot,admitted.File.Stores.AcquisitionRawRoot,
                new ObservedPlanner(actualPlanner,count),observedInventory,new ObservedIdentity(identities,count),observedSource,factory,canonical);
            var result=await operation.RunAsync(admitted.Request);Assert.Equal(output,Path.GetDirectoryName(ArtifactPaths.Resolve(output,"catalog.sqlite")));
            Assert.Contains(result.Artifacts,artifact=>artifact.RelativePath=="catalog.sqlite");
            using var database=CatalogDatabase.OpenExisting(Path.Combine(output,"catalog.sqlite"));
            using var query=database.Command("SELECT document_json FROM source_details ORDER BY source_id");using var rows=query.ExecuteReader();var indices=new List<int>();
            while(rows.Read())
            {
                var detail=JsonNode.Parse(rows.GetString(0))!.AsObject();var path=detail["path"]!.GetValue<string>();var row=projection.Rows.Single(item=>item.LogicalPath==path);indices.Add(row.RowIndex);
                var native=detail["x-native-source-intake"]!;var context=native["context"]!;
                contexts &= context["SnapshotId"]!.GetValue<string>()==projection.RecipeSha256 && context["PlatformVersion"]!.GetValue<string>()=="37.2" &&
                    context["Repository"]!.GetValue<string>()=="fixture-repository" && context["Revision"]!.GetValue<string>()==new string('1',40);
                audit &= native["historical_cache_disposition"]!.GetValue<string>()=="declared-unconsumed-unverified" && !native["historical_records_consumed"]!.GetValue<bool>() && !native["historical_identity_claimed"]!.GetValue<bool>();
                var normalized=detail["x-native-catalog-normalization"]!;
                audit &= !normalized["source_evidence"]!.GetValue<bool>() && normalized["original_row_canonical_sha256"]!.GetValue<string>()==canonical.Hash(row.OriginalRow);
                originalRows &= row.OriginalRow.GetProperty("status").GetString()=="failed" && detail["status"]!.GetValue<string>()=="failed";
            }
            selected=string.Join(',',indices.Order());
        }
        catch(Exception error)when(error is CatalogBuildException or PinnedArtifactException or SourcesInventoryException or SourceExtractException)
        {
            outcome=error switch{CatalogBuildException item=>item.Code,PinnedArtifactException item=>item.Code,SourcesInventoryException item=>item.Code,SourceExtractException item=>item.Code,_=>throw new InvalidOperationException()};
        }
        var retained=originals.All(item=>File.ReadAllBytes(item.Key).AsSpan().SequenceEqual(item.Value));
        originalRows &= beforeProjection.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(projection)));
        var noEffects=!Directory.Exists(output) && !Directory.Exists(stage);
        return Reply(outcome,count,selected,originalRows,roots,audit,contexts,noEffects,retained);
    }
    private static CatalogPipelineWorkflowReply Reply(string outcome,Counts count,string selected,bool originals,bool roots,bool audit,bool context,bool noEffects,bool retained)=>
        new(outcome,count.Outer,count.Recipe,count.Planner,count.Inventory,count.Generation,count.Producer,selected,originals,roots,audit,context,noEffects,retained);
    private sealed class NoNetwork:IByteTransport
    {
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Seeded native workflow fixture must not use network");
    }
    private sealed class ObservedPins(IPinnedArtifactReader inner,Counts count):IPinnedArtifactReader
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default)
        {if(pin.RelativePath=="workflow/catalog.json")count.Outer++;else count.Recipe++;return inner.ReadAsync(pin,cancellationToken);}
    }
    private sealed class ObservedPlanner(IRehydrationPlanner inner,Counts count):IRehydrationPlanner
    {
        public RehydrationPlanResult Plan(RehydrationPlanRequest request){count.Planner++;return inner.Plan(request);}
    }
    private sealed class ObservedInventory(ISourcesInventoryReader inner,Counts count):ISourcesInventoryReader
    {
        public Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken cancellationToken=default)
        {count.Inventory++;return inner.ReadAsync(request,cancellationToken);}
    }
    private sealed class ObservedIdentity(ISourceProducerIdentityBuilder inner,Counts count):ISourceProducerIdentityBuilder
    {
        public SourceProducerIdentityAdmission Create(SourceJavaAdmission? java=null){count.Producer++;return inner.Create(java);}
    }
    private sealed class ObservedGeneration(ISourceExtractGenerationReader inner,Counts count):ISourceExtractGenerationReader
    {
        public Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request,CancellationToken cancellationToken=default)
        {count.Generation++;return inner.OpenAsync(request,cancellationToken);}
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogPipelineWorkflowRealTests
{
    [Theory,MemberData(nameof(CatalogPipelineWorkflowCases.All),MemberType=typeof(CatalogPipelineWorkflowCases))]
    public Task NativeWorkflowUsesSameElevenPinsProjectionIdentityAndEffectAssertions(CatalogPipelineWorkflowCase test)=>
        CatalogPipelineWorkflowCases.AssertCase(test,new NativeCatalogPipelineWorkflowProvider());
}
