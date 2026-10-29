using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogSourcesCliObservation(int Exit,int BadPinExit,JsonElement Summary,
    CatalogPipelineWorkflowFile Workflow,SourcesExtractWorkflowFile SourceWorkflow,StoredArtifact ExtractIndex,StoredArtifact WorkflowPin,
    string ExpectedOriginalRowHash,string ExpectedProducerFingerprint,IReadOnlyList<string> Arguments,string ProjectRoot,string RuntimeRoot,
    bool SourcePreludeVerified,bool ArtifactsVerified,bool DatabaseExportAgreement,bool NormalizationAuditVerified,
    bool OriginalRowUnchanged,bool SourceContextPreserved,bool InputsRetained,bool NativeProducerObserved,
    bool DefaultDenyAndUnknownCodec,bool RetainedStageMatchesFinal,bool BadPinRejected,bool BadPinNoEffects,bool HostAndDllPinned);
public interface ICatalogSourcesCliProbe { Task<CatalogSourcesCliObservation> RunAsync(); }
public static class CatalogSourcesCliCases
{
    public static string[] Arguments(string runtime,string project,StoredArtifact pin)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Catalog","--operation","BuildFromSources",
        "--project-root",project,"--pipeline-request",pin.RelativePath,"--pipeline-request-sha256",pin.Sha256,
        "--pipeline-request-length",pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    public static async Task AssertCase(ICatalogSourcesCliProbe provider)
    {
        var actual=await provider.RunAsync();Assert.Equal(0,actual.Exit);Assert.NotEqual(0,actual.BadPinExit);
        Assert.True(actual.SourcePreludeVerified);Assert.True(actual.ArtifactsVerified);Assert.True(actual.DatabaseExportAgreement);
        Assert.True(actual.NormalizationAuditVerified);Assert.True(actual.OriginalRowUnchanged);Assert.True(actual.SourceContextPreserved);
        Assert.True(actual.InputsRetained);Assert.True(actual.NativeProducerObserved);Assert.True(actual.DefaultDenyAndUnknownCodec);
        Assert.True(actual.RetainedStageMatchesFinal);Assert.True(actual.BadPinRejected);Assert.True(actual.BadPinNoEffects);Assert.True(actual.HostAndDllPinned);
        var summary=actual.Summary;Assert.Matches("^[a-f0-9]{64}$",summary.GetProperty("RegistryId").GetString()!);
        Assert.Equal(actual.ExpectedProducerFingerprint,summary.GetProperty("ProducerFingerprint").GetString());Assert.False(summary.GetProperty("Complete").GetBoolean());
        var counts=summary.GetProperty("Counts");Assert.Equal(1,counts.GetProperty("sources").GetInt32());
        foreach(var name in new[]{"bindings","occurrences","binding_declarations","effective_entries"})Assert.Equal(2,counts.GetProperty(name).GetInt32());
        var artifacts=summary.GetProperty("Artifacts").Deserialize<StoredArtifact[]>()!;
        foreach(var name in new[]{"catalog.sqlite","entries.jsonl","device_inventory.jsonl","device_inventory_manifest.json","dispatch_manifest.json"})Assert.Contains(artifacts,artifact=>artifact.RelativePath==name);
        var file=actual.Workflow;
        Assert.Equal(actual.SourceWorkflow.Sources,file.Sources);Assert.Equal(actual.SourceWorkflow.Stores,file.Stores);
        Assert.Equal(actual.SourceWorkflow.InventoryManifest,file.InventoryManifest);Assert.Equal(actual.ExtractIndex,file.SourceGenerationCommit);
        Assert.NotEqual(actual.SourceWorkflow.TrustedPriorIndex,file.SourceGenerationCommit);
        Assert.Equal(SourcesExtractCliCases.SecondGeneration+"/index.json",file.SourceGenerationCommit.RelativePath);
        Assert.Null(file.Java);Assert.Equal("development",Assert.Single(file.Metadata.SourceNormalization!).Channel);
        var metadata=Assert.Single(file.Metadata.SourceNormalization!);Assert.Equal(0,metadata.RowIndex);Assert.Equal(actual.ExpectedOriginalRowHash,metadata.ExpectedOriginalRowCanonicalSha256);
        Assert.Equal(37,metadata.ApiLevel);Assert.Equal("2026-10-28T00:00:00Z",metadata.RetrievedAt);
        var wire=JsonSerializer.SerializeToElement(file);Assert.False(wire.TryGetProperty("Projection",out _));Assert.False(wire.TryGetProperty("Producer",out _));
        Assert.Equal(file.SourceGenerationCommit,summary.GetProperty("SourceGenerationCommit").Deserialize<StoredArtifact>());
        Assert.Equal(file.OutputRoot,summary.GetProperty("OutputRoot").GetString());Assert.NotEqual(actual.ProjectRoot,actual.RuntimeRoot);
        var args=actual.Arguments;Assert.Equal("exec",args[0]);Assert.DoesNotContain(args,value=>value is "build" or "restore" or "run" or "--internal-process-probe");
        string Value(string name){var index=args.ToList().IndexOf(name);Assert.InRange(index,0,args.Count-2);return args[index+1];}
        Assert.Equal("Catalog",Value("--target"));Assert.Equal("BuildFromSources",Value("--operation"));
        Assert.Equal(actual.RuntimeRoot,Value("--root"));Assert.Equal(actual.ProjectRoot,Value("--project-root"));
        Assert.Equal(actual.WorkflowPin.RelativePath,Value("--pipeline-request"));Assert.Equal(actual.WorkflowPin.Sha256,Value("--pipeline-request-sha256"));
        Assert.Equal(actual.WorkflowPin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),Value("--pipeline-request-length"));
        Assert.DoesNotContain("--source-manifest",args);
    }
}
public sealed class ScriptedCatalogSourcesCli(string mutation=""):ICatalogSourcesCliProbe
{
    public Task<CatalogSourcesCliObservation> RunAsync()
    {
        var first=new StoredArtifact(SourcesExtractCliCases.FirstGeneration+"/index.json",new('a',64),81,"application/json");
        var second=new StoredArtifact(SourcesExtractCliCases.SecondGeneration+"/index.json",new('b',64),123,"application/json");
        var source=SourcesExtractCliCases.Workflow("/inventory","/raw","/generations",new("sources/inventory.json",new('c',64),71,"application/json"),SourcesExtractCliCases.SecondGeneration,first);
        var metadata=new CatalogSourceNormalization(0,new('d',64),37,"development",RetrievedAt:"2026-10-28T00:00:00Z");
        var file=new CatalogPipelineWorkflowFile(source.Sources,source.Stores,source.InventoryManifest,second,new(1,"authored fixture",37,"fixture",[metadata]),"/schemas","/catalog-output","/catalog-stage");
        if(mutation=="first-index")file=file with{SourceGenerationCommit=first};
        if(mutation=="recipe")file=file with{Sources=file.Sources with{Recipe=file.Sources.Recipe with{Sha256=new('0',64)}}};
        if(mutation=="roots")file=file with{Stores=file.Stores with{AcquisitionRawRoot="wrong"}};
        if(mutation=="inventory")file=file with{InventoryManifest=file.InventoryManifest with{Sha256=new('0',64)}};
        var altered=metadata;
        if(mutation=="row-hash")altered=altered with{ExpectedOriginalRowCanonicalSha256=new('0',64)};
        if(mutation=="row-index")altered=altered with{RowIndex=1};
        if(mutation=="api")altered=altered with{ApiLevel=null};
        if(mutation=="channel")altered=altered with{Channel="stable"};
        if(mutation=="retrieved")altered=altered with{RetrievedAt=null};
        file=file with{Metadata=file.Metadata with{SourceNormalization=[altered]}};
        var names=new[]{"catalog.sqlite","entries.jsonl","device_inventory.jsonl","device_inventory_manifest.json","dispatch_manifest.json"};
        var artifacts=names.Select(name=>new StoredArtifact(name,new('f',64),17,"application/json")).ToArray();
        var summary=JsonSerializer.SerializeToNode(new{RegistryId=new string('1',64),ProducerFingerprint=new string('e',64),Artifacts=artifacts,
            Counts=new{sources=1,bindings=2,occurrences=2,binding_declarations=2,effective_entries=2},Complete=false,OutputRoot="/catalog-output",SourceGenerationCommit=second})!.AsObject();
        if(mutation.StartsWith("summary:",StringComparison.Ordinal))summary.Remove(mutation[8..]);
        if(mutation.StartsWith("count:",StringComparison.Ordinal))summary["Counts"]![mutation[6..]]=99;
        if(mutation=="complete")summary["Complete"]=true;
        var pin=new StoredArtifact("workflows/catalog.json",new('2',64),319,"application/json");var args=CatalogSourcesCliCases.Arguments("/runtime","/project",pin);
        if(mutation=="child-build")args[0]="build";
        if(mutation=="operation")args[Array.IndexOf(args,"--operation")+1]="Build";
        if(mutation=="request-pin")args[Array.IndexOf(args,"--pipeline-request-sha256")+1]="wrong";
        return Task.FromResult(new CatalogSourcesCliObservation(mutation=="exit"?1:0,mutation=="bad-exit"?0:1,JsonSerializer.SerializeToElement(summary),file,source,second,pin,new('d',64),new('e',64),args,"/project","/runtime",
            mutation!="prelude",mutation!="artifacts",mutation!="db-export",mutation!="audit",mutation!="original",mutation!="context",mutation!="inputs",mutation!="producer",
            mutation!="authority",mutation!="stage",mutation!="bad-pin",mutation!="bad-effects",mutation!="host-dll"));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourcesCliMockTests
{
    [Fact]public Task CorrectSameSdkSourcesToCatalogContinuationPasses()=>CatalogSourcesCliCases.AssertCase(new ScriptedCatalogSourcesCli());
    [Fact]public async Task EveryInvocationPinProvidedMetadataOutputAndAuthorityAssertionDiscriminates()
    {
        var mutations=new[]{"exit","bad-exit","first-index","recipe","roots","inventory","row-hash","row-index","api","channel","retrieved","complete","child-build","operation","request-pin",
            "prelude","artifacts","db-export","audit","original","context","inputs","producer","authority","stage","bad-pin","bad-effects","host-dll"}
            .Concat(new[]{"RegistryId","ProducerFingerprint","Artifacts","Counts","Complete","OutputRoot","SourceGenerationCommit"}.Select(name=>"summary:"+name))
            .Concat(new[]{"sources","bindings","occurrences","binding_declarations","effective_entries"}.Select(name=>"count:"+name));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogSourcesCliCases.AssertCase(new ScriptedCatalogSourcesCli(mutation))));
    }
}
