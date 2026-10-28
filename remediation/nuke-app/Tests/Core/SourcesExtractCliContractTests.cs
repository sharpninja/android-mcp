using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using AndroidMcp.BuildTool.Tests.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesExtractCliObservation(IReadOnlyList<int> Exits,JsonElement FirstSummary,JsonElement SecondSummary,
    byte[] FirstRecords,byte[] SecondRecords,StoredArtifact FirstCacheData,StoredArtifact SecondCacheData,
    StoredArtifact FirstSidecar,StoredArtifact SecondSidecar,byte[] FirstCommitBefore,byte[] FirstCommitAfter,
    bool InputsRetained,bool BundleRetained,bool RawRetained,bool InventoryRetained,bool PriorGenerationRetained,
    bool ObservedProducerMatches,bool WrongOuterPinError,bool WrongPinNoGeneration,
    SourcesExtractWorkflowFile FirstWorkflow,SourcesExtractWorkflowFile SecondWorkflow,
    StoredArtifact FirstWorkflowPin,StoredArtifact SecondWorkflowPin,IReadOnlyList<IReadOnlyList<string>> Arguments,
    string RuntimeRoot,string ProjectRoot);
public interface ISourcesExtractCliProbe{Task<SourcesExtractCliObservation> RunAsync();}
public static class SourcesExtractCliCases
{
    public const string FirstGeneration="sdk-generation-001";
    public const string SecondGeneration="sdk-generation-002";
    public static byte[] SourceBytes=>Encoding.UTF8.GetBytes(SdkXmlContractCases.Get("SDK-02").Text);
    public static RehydrationPlanRequest Recipe()=>RehydrationContractCases.Request(root=>{
        var rows=root["inventory"]!["sources"]!.AsArray();rows.RemoveAt(0);var row=rows[0]!.AsObject();
        row["path"]="sdk/api.xml";row["local_path"]="raw/api.xml";row["kind"]="sdk-api-index";
        row["sha256"]=ContentHash.Sha256(SourceBytes);row["size_bytes"]=SourceBytes.Length;
        row["repository"]="fixture-repository";row["revision"]="pinned-fixture-revision";row["snapshot_id"]="fixture-snapshot";row["platform_api_version"]="37.2";
        row["transport"]=new JsonObject{["immutable_url"]=false,["required_local_snapshot"]="snapshots/api.xml"};
    });
    public static StoredArtifact Pin(Artifact artifact)=>new(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType);
    public static SourcesExtractWorkflowFile Workflow(string inventoryRoot,string rawRoot,string generationsRoot,StoredArtifact inventory,
        string generation=FirstGeneration,StoredArtifact? prior=null)
    {var recipe=Recipe();return new(new(Pin(recipe.Recipe),Pin(recipe.Lock),"fixture-release"),new(inventoryRoot,rawRoot,generationsRoot),inventory,generation,TrustedPriorIndex:prior);}
    public static string[] Arguments(string runtime,string project,StoredArtifact pin)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Extract","--dotnet-host","/native/dotnet",
        "--project-root",project,"--pipeline-request",pin.RelativePath,"--pipeline-request-sha256",pin.Sha256,
        "--pipeline-request-length",pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    public static JsonObject Summary(StoredArtifact index,bool hit)=>JsonSerializer.SerializeToNode(new{Index=index,SelectedLogicalCacheRows=1,UniqueCacheAliases=1,CacheHits=hit?1:0,RawExtractions=hit?0:1})!.AsObject();
    public static async Task AssertCase(ISourcesExtractCliProbe provider)
    {
        var actual=await provider.RunAsync();Assert.Equal(6,actual.Exits.Count);Assert.All(actual.Exits.Take(5),exit=>Assert.Equal(0,exit));Assert.NotEqual(0,actual.Exits[5]);
        Assert.True(actual.InputsRetained);Assert.True(actual.BundleRetained);Assert.True(actual.RawRetained);Assert.True(actual.InventoryRetained);Assert.True(actual.PriorGenerationRetained);
        Assert.True(actual.ObservedProducerMatches);Assert.True(actual.WrongOuterPinError);Assert.True(actual.WrongPinNoGeneration);
        Assert.Equal(actual.FirstCommitBefore,actual.FirstCommitAfter);
        Assert.Equal(actual.FirstRecords,actual.SecondRecords);
        using var records=JsonDocument.Parse(actual.FirstRecords);var rows=records.RootElement.EnumerateArray().ToArray();Assert.Equal(2,rows.Length);
        var golden=SdkXmlContractCases.Get("SDK-02").ExpectedRecords;
        for(var index=0;index<2;index++)SourceExtractionContractCases.AssertSubset(golden[index],JsonNode.Parse(rows[index].GetRawText()));
        var summaries=new[]{actual.FirstSummary,actual.SecondSummary};
        for(var index=0;index<2;index++){
            var summary=summaries[index];Assert.Equal(1,summary.GetProperty("SelectedLogicalCacheRows").GetInt32());Assert.Equal(1,summary.GetProperty("UniqueCacheAliases").GetInt32());
            Assert.Equal(index,summary.GetProperty("CacheHits").GetInt32());Assert.Equal(1-index,summary.GetProperty("RawExtractions").GetInt32());
            var pin=summary.GetProperty("Index").Deserialize<StoredArtifact>()!;Assert.Equal((index==0?FirstGeneration:SecondGeneration)+"/index.json",pin.RelativePath);
            if(index==0){Assert.Equal(ContentHash.Sha256(actual.FirstCommitBefore),pin.Sha256);Assert.Equal(actual.FirstCommitBefore.Length,pin.Length);}
        }
        Assert.Equal(actual.FirstCacheData,actual.SecondCacheData);Assert.Equal(actual.FirstSidecar,actual.SecondSidecar);
        Assert.StartsWith(FirstGeneration+"/cache/",actual.FirstCacheData.RelativePath,StringComparison.Ordinal);Assert.EndsWith(".csharp-cache.json",actual.FirstSidecar.RelativePath,StringComparison.Ordinal);
        var first=actual.FirstWorkflow;var second=actual.SecondWorkflow;
        Assert.Equal(FirstGeneration,first.GenerationRoot);Assert.Equal(SecondGeneration,second.GenerationRoot);Assert.Null(first.TrustedPriorIndex);
        Assert.Equal(actual.FirstSummary.GetProperty("Index").Deserialize<StoredArtifact>(),second.TrustedPriorIndex);Assert.Equal(first.Sources,second.Sources);Assert.Equal(first.Stores,second.Stores);
        Assert.Equal(first.InventoryManifest,second.InventoryManifest);Assert.Null(first.Java);Assert.Null(second.Java);
        Assert.Equal(Pin(Recipe().Recipe),first.Sources.Recipe);Assert.Equal(Pin(Recipe().Lock),first.Sources.RecipeLock);Assert.Equal("fixture-release",first.Sources.Release);
        Assert.Equal(6,actual.Arguments.Count);
        for(var index=0;index<actual.Arguments.Count;index++){
            var args=actual.Arguments[index];Assert.Equal("exec",args[0]);Assert.DoesNotContain(args,value=>value is "build" or "run" or "--internal-process-probe");
            string Value(string name){var offset=args.ToList().IndexOf(name);Assert.InRange(offset,0,args.Count-2);return args[offset+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal(new[]{"Plan","Fetch","Inventory","Extract","Extract","Extract"}[index],Value("--operation"));
            Assert.Equal(actual.RuntimeRoot,Value("--root"));Assert.Equal(actual.ProjectRoot,Value("--project-root"));Assert.NotEqual(actual.ProjectRoot,actual.RuntimeRoot);
            if(index is 3 or 4){var pin=index==3?actual.FirstWorkflowPin:actual.SecondWorkflowPin;Assert.Equal(pin.RelativePath,Value("--pipeline-request"));Assert.Equal(pin.Sha256,Value("--pipeline-request-sha256"));Assert.Equal(pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),Value("--pipeline-request-length"));}
        }
    }
}
public sealed class ScriptedSourcesExtractCliProbe(string mutation=""):ISourcesExtractCliProbe
{
    public Task<SourcesExtractCliObservation> RunAsync()
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(new{Schema="fixture-observation-only",GenerationId=SourcesExtractCliCases.FirstGeneration});
        var firstPin=new StoredArtifact(SourcesExtractCliCases.FirstGeneration+"/index.json",ContentHash.Sha256(before),before.Length,"application/json");
        var secondPin=new StoredArtifact(SourcesExtractCliCases.SecondGeneration+"/index.json",new('b',64),17,"application/json");
        var first=SourcesExtractCliCases.Summary(firstPin,false);var second=SourcesExtractCliCases.Summary(secondPin,true);
        if(mutation.StartsWith("first:",StringComparison.Ordinal))first.Remove(mutation[6..]);if(mutation.StartsWith("second:",StringComparison.Ordinal))second.Remove(mutation[7..]);
        var records=JsonSerializer.SerializeToUtf8Bytes(SdkXmlContractCases.Get("SDK-02").ExpectedRecords);
        var data=new StoredArtifact(SourcesExtractCliCases.FirstGeneration+"/cache/0.json.gz",new('c',64),73,"application/gzip");
        var sidecar=new StoredArtifact(data.RelativePath+".csharp-cache.json",new('d',64),109,"application/json");
        var workflow=SourcesExtractCliCases.Workflow("inventory","raw","generations",new("sources/inventory.json",new('e',64),719,"application/json"));
        var resume=workflow with{GenerationRoot=SourcesExtractCliCases.SecondGeneration,TrustedPriorIndex=firstPin};
        if(mutation=="same-generation")resume=resume with{GenerationRoot=workflow.GenerationRoot};if(mutation=="prior-pin")resume=resume with{TrustedPriorIndex=null};
        if(mutation=="recipe")workflow=workflow with{Sources=workflow.Sources with{Recipe=workflow.Sources.Recipe with{Sha256=new('0',64)}}};
        var outer1=new StoredArtifact("workflow/first.json",new('1',64),317,"application/json");var outer2=new StoredArtifact("workflow/resume.json",new('2',64),419,"application/json");
        var arguments=new[]{new[]{"exec","AndroidMcp.Build.dll","--root","/runtime","--target","Sources","--operation","Plan","--project-root","/project"},
            new[]{"exec","AndroidMcp.Build.dll","--root","/runtime","--target","Sources","--operation","Fetch","--project-root","/project"},
            new[]{"exec","AndroidMcp.Build.dll","--root","/runtime","--target","Sources","--operation","Inventory","--project-root","/project"},
            SourcesExtractCliCases.Arguments("/runtime","/project",outer1),SourcesExtractCliCases.Arguments("/runtime","/project",outer2),SourcesExtractCliCases.Arguments("/runtime","/project",outer1)};
        if(mutation=="child-build")arguments[3][0]="build";if(mutation=="parent-root")arguments[3][Array.IndexOf(arguments[3],"--root")+1]="/parent";
        if(mutation=="outer-hash")arguments[3][Array.IndexOf(arguments[3],"--pipeline-request-sha256")+1]="wrong";
        if(mutation=="outer-length")arguments[4][Array.IndexOf(arguments[4],"--pipeline-request-length")+1]="1";
        if(mutation=="outer-path")arguments[3][Array.IndexOf(arguments[3],"--pipeline-request")+1]="first.json";
        return Task.FromResult(new SourcesExtractCliObservation(mutation=="exit"?[0,0,0,1,0,1]:mutation=="bad-exit"?[0,0,0,0,0,0]:[0,0,0,0,0,1],JsonSerializer.SerializeToElement(first),JsonSerializer.SerializeToElement(second),records,mutation=="records"?new byte[]{0}:records,
            data,mutation=="cache-data"?data with{Sha256=new('0',64)}:data,sidecar,mutation=="sidecar"?sidecar with{Length=1}:sidecar,before,mutation=="commit"?new byte[]{0}:before,
            mutation!="inputs",mutation!="bundle",mutation!="raw",mutation!="inventory",mutation!="prior-retained",mutation!="producer",mutation!="pin-error",mutation!="pin-write",workflow,resume,outer1,outer2,arguments,"/runtime","/project"));
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesExtractCliMockTests
{
    [Fact]public Task CorrectSdkFixtureAndSealedGenerationHit()=>SourcesExtractCliCases.AssertCase(new ScriptedSourcesExtractCliProbe());
    [Fact]public async Task EveryPinCounterOriginalAndInvocationAssertionDiscriminates()
    {
        var mutations=new[]{"exit","bad-exit","records","cache-data","sidecar","commit","inputs","bundle","raw","inventory","prior-retained","producer","pin-error","pin-write","same-generation","prior-pin","recipe","child-build","parent-root","outer-hash","outer-length","outer-path"}
            .Concat(new[]{"Index","SelectedLogicalCacheRows","UniqueCacheAliases","CacheHits","RawExtractions"}.SelectMany(name=>new[]{"first:"+name,"second:"+name}));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesExtractCliCases.AssertCase(new ScriptedSourcesExtractCliProbe(mutation))));
    }
}
