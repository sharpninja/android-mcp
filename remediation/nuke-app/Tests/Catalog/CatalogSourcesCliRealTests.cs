using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogSourcesCliProbe:ICatalogSourcesCliProbe
{
    private static string FileHash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
    private static Dictionary<string,string> Hashes(string root)=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path),FileHash,StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> before,IReadOnlyDictionary<string,string> after)=>before.OrderBy(pair=>pair.Key).SequenceEqual(after.OrderBy(pair=>pair.Key));
    public async Task<CatalogSourcesCliObservation> RunAsync()
    {
        // ONE already-asserted Source CLI fixture under this same compiled DLL.
        // This helper owns Plan/Fetch/Inventory/Extract/resume preparation.
        var source=await SourcesExtractCliFixture.CreateAsync();
        var canonical=new CanonicalJson();var inputs=new FileArtifactStore(source.ProjectRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var recipe=await new SourceWorkflowInputsReader(new PinnedArtifactReader(inputs)).ReadAsync(source.Workflow.Sources);
        // Author the Catalog-only sidecar through the sole planner, never a
        // second recipe parser or by editing Source's original recipe/rows.
        var planned=new RehydrationPlanner(canonical).Plan(recipe);Assert.True(planned.IsValid);
        var projection=planned.Plan!.SourceProjection!;var row=Assert.Single(projection.Rows);var originalHash=canonical.Hash(row.OriginalRow);
        var root=Path.Combine(Path.GetTempPath(),"native-sources-catalog-continuation",Guid.NewGuid().ToString("N"));
        var output=Path.Combine(root,"catalog output");var stage=Path.Combine(root,"private stage");
        var metadata=new CatalogSourceNormalization(row.RowIndex,originalHash,37,"development",RetrievedAt:"2026-10-28T00:00:00Z");
        var file=new CatalogPipelineWorkflowFile(source.Workflow.Sources,source.Workflow.Stores,source.Workflow.InventoryManifest,source.ExtractIndex,
            new(1,"authored fixture Android",37,"one SDK fixture only",[metadata]),NativeSchemaProvider.Root(),output,stage);
        var pin=await inputs.WriteAsync(new("workflows/catalog-from-sources.json",canonical.Serialize(JsonSerializer.SerializeToElement(file)),"application/json"));
        var refused=file with{OutputRoot=Path.Combine(root,"refused output"),StageRoot=Path.Combine(root,"refused stage")};
        var refusedPin=await inputs.WriteAsync(new("workflows/catalog-bad-pin.json",canonical.Serialize(JsonSerializer.SerializeToElement(refused)),"application/json"));
        var protectedInputs=new[]{file.Sources.Recipe,file.Sources.RecipeLock,pin,refusedPin};
        var rawBefore=Hashes(file.Stores.AcquisitionRawRoot);var inventoryBefore=Hashes(file.Stores.InventoryRoot);var generationsBefore=Hashes(file.Stores.GenerationStoreRoot);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host required");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;var dllHash=FileHash(dll);
        var tool=new ToolIdentity(host,FileHash(host),Environment.Version.ToString());var runner=new NativeProcessRunner(new SystemClock());
        async Task<(ProcessResult Result,string Stdout,string Stderr,string[] Arguments)> Invoke(StoredArtifact callerPin,string receipt)
        {
            var args=CatalogSourcesCliCases.Arguments(source.RuntimeRoot,source.ProjectRoot,callerPin).ToList();args[1]=dll;args.AddRange(["--dotnet-host",host]);
            args.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
            var result=await runner.RunAsync(new(tool,args,source.RuntimeRoot,new Dictionary<string,string>(),Path.Combine(root,receipt)));
            return(result,await File.ReadAllTextAsync(result.StandardOutputPath),await File.ReadAllTextAsync(result.StandardErrorPath),args.ToArray());
        }
        var success=await Invoke(pin,"catalog process receipts");
        Assert.True(success.Result.ExitCode==0,"Catalog continuation failed; retained fixture "+root+Environment.NewLine+success.Stdout+Environment.NewLine+success.Stderr);
        var summaryLine=Assert.Single(success.Stdout.Split('\n').Select(line=>line.Trim()),line=>line.StartsWith("{\"RegistryId\":",StringComparison.Ordinal));
        using var summaryDocument=JsonDocument.Parse(summaryLine);var summary=summaryDocument.RootElement.Clone();
        var rejected=await Invoke(refusedPin with{Sha256=new string('0',64)},"bad pin process receipts");
        var artifacts=summary.GetProperty("Artifacts").Deserialize<StoredArtifact[]>()!;
        var artifactsVerified=artifacts.All(artifact=>
        {
            var path=ArtifactPaths.Resolve(output,artifact.RelativePath);return new FileInfo(path).Length==artifact.Length && FileHash(path)==artifact.Sha256;
        });
        using var database=CatalogDatabase.OpenExisting(ArtifactPaths.Resolve(output,"catalog.sqlite"));
        string[] Documents(string sql)
        {using var query=database.Command(sql);using var rows=query.ExecuteReader();var result=new List<string>();while(rows.Read())result.Add(rows.GetString(0));return result.ToArray();}
        var exported=File.ReadAllLines(ArtifactPaths.Resolve(output,"entries.jsonl"));var stored=Documents("SELECT document_json FROM effective_entries ORDER BY entry_id");
        var inventoryLines=File.ReadAllLines(ArtifactPaths.Resolve(output,"device_inventory.jsonl"));
        var agreement=exported.SequenceEqual(stored,StringComparer.Ordinal) && inventoryLines.SequenceEqual(Documents("SELECT document_json FROM registry_inventory ORDER BY entry_id"),StringComparer.Ordinal);
        var detail=JsonNode.Parse(Assert.Single(Documents("SELECT document_json FROM source_details")))!;var normalization=detail["x-native-catalog-normalization"]!;
        var native=detail["x-native-source-intake"]!;var context=native["context"]!;
        var audit=!normalization["source_evidence"]!.GetValue<bool>() && normalization["disposition"]!.GetValue<string>()=="provided-catalog-normalization" &&
            normalization["original_row_canonical_sha256"]!.GetValue<string>()==originalHash && detail["api_level"]!.GetValue<long>()==37 &&
            detail["channel"]!.GetValue<string>()=="development" && detail["retrieved_at"]!.GetValue<string>()=="2026-10-28T00:00:00Z" &&
            native["cache_hit"]!.GetValue<bool>() && native["cache_receipt"] is JsonObject && native["cache_seal"] is JsonObject;
        var originalUnchanged=row.OriginalRow.EnumerateObject().All(property=>canonical.Hash(property.Value)==canonical.Hash(JsonSerializer.SerializeToElement(detail[property.Name])));
        var sourceContext=context["SnapshotId"]!.GetValue<string>()==projection.RecipeSha256 && context["PlatformVersion"]!.GetValue<string>()=="37.2" &&
            context["Repository"]!.GetValue<string>()==row.Context.Repository && context["Revision"]!.GetValue<string>()==row.Context.Revision;
        var retained=true;
        foreach(var input in protectedInputs)retained &= (await new PinnedArtifactReader(inputs).ReadAsync(input)).Length==input.Length;
        retained &= Same(rawBefore,Hashes(file.Stores.AcquisitionRawRoot)) && Same(inventoryBefore,Hashes(file.Stores.InventoryRoot)) && Same(generationsBefore,Hashes(file.Stores.GenerationStoreRoot));
        var aliases=JsonNode.Parse(File.ReadAllText(ArtifactPaths.Resolve(output,"dispatch_manifest.json")))!["aliases"]!.AsArray();
        var deny=aliases.Count>0 && aliases.All(alias=>alias!["default_grant"]!.GetValue<string>()=="deny" && alias["dispatch_state"]!.GetValue<string>()=="unavailable") &&
            exported.All(line=>JsonNode.Parse(line)!["codec_status"]!.GetValue<string>()=="unknown");
        var stageDatabase=Assert.Single(Directory.EnumerateFiles(stage,"catalog.staging.sqlite",SearchOption.AllDirectories));
        var stageMatches=FileHash(stageDatabase)==FileHash(ArtifactPaths.Resolve(output,"catalog.sqlite"));
        var validation=JsonNode.Parse(File.ReadAllText(ArtifactPaths.Resolve(output,"validation.json")))!;
        var producerObserved=validation["native_producer"]!["id"]!.GetValue<string>()==CatalogBuild.ProducerId && !validation["native_producer"]!["historical_python_identity"]!.GetValue<bool>();
        return new(success.Result.ExitCode,rejected.Result.ExitCode,summary,file,source.Workflow,source.ExtractIndex,pin,originalHash,dllHash,success.Arguments,
            source.ProjectRoot,source.RuntimeRoot,true,artifactsVerified,agreement,audit,originalUnchanged,sourceContext,retained,producerObserved,deny,stageMatches,
            (rejected.Stdout+rejected.Stderr).Contains("ARTIFACT_PIN_HASH",StringComparison.Ordinal),!Directory.Exists(refused.OutputRoot)&&!Directory.Exists(refused.StageRoot),
            FileHash(host)==tool.Sha256 && FileHash(dll)==dllHash && success.Arguments[5]==dll);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourcesCliRealTests
{
    [Fact]public Task SameActualSdkSourceChainContinuesIntoRegisteredCatalogBuild()=>CatalogSourcesCliCases.AssertCase(new NativeCatalogSourcesCliProbe());
}
