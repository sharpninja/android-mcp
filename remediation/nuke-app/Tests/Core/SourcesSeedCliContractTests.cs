using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesSeedCliObservation(int FirstExit,int RepeatExit,int RejectedExit,int FetchExit,
    JsonElement FirstSummary,JsonElement RepeatSummary,byte[] SeedBefore,byte[] SeedAfter,byte[] AcquisitionReceipt,
    IReadOnlyList<string> SeedDataPaths,bool NoAcquisitionBeforeFetch,bool InputsRetained,bool BundleRetained,bool BadReleaseError,
    string RawShaBeforeFetch,string RawShaAfterFetch,IReadOnlyList<IReadOnlyList<string>> Arguments,
    string RuntimeRoot,string ProjectRoot,string BundleRoot,string OutputRoot);
public interface ISourcesSeedCliProbe{Task<SourcesSeedCliObservation> RunAsync();}
public static class SourcesSeedCliCases
{
    public const string Generation="cli-source-seed";
    public static string RecipeSha=>ContentHash.Sha256(SourcesExtractCliCases.Recipe().Recipe.Bytes.Span);
    public static string[] Arguments(string runtime,string project,string bundle,string output,bool wrongRelease=false)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Seed","--dotnet-host","/native/dotnet",
        "--project-root",project,"--source-manifest","rehydration/fixture.json","--recipe-lock","rehydration/fixture.lock.json",
        "--bundle-root",bundle,"--output-root",output,"--generation",Generation,"--release",wrongRelease?"wrong-release":"fixture-release","--acquisition-profile","CanonicalRestore"];
    public static JsonObject Receipt()=>JsonSerializer.SerializeToNode(new{Acquisition=new{GenerationId=Generation,CachePolicy=0,
        Projection=new{RecipeSha256=RecipeSha}},OriginalCounts=new{Imported=1,Reused=0,Absent=0},RawComplete=true,HistoricalByteIdentityClaimed=false,
        Entries=new[]{new{PhysicalSourceIndex=0,Status=0,Artifact=new StoredArtifact("raw/api.xml",ContentHash.Sha256(SourcesExtractCliCases.SourceBytes),SourcesExtractCliCases.SourceBytes.Length,"application/octet-stream"),BundleCandidate="raw/api.xml"}}})!.AsObject();
    public static JsonObject Summary(byte[] bytes,bool reused)=>JsonSerializer.SerializeToNode(new{
        Manifest=new StoredArtifact("sources/seed.json",ContentHash.Sha256(bytes),bytes.Length,"application/json"),Reused=reused,GenerationId=Generation,
        CurrentCounts=new{Imported=reused?0:1,Reused=reused?1:0,Absent=0},OriginalCounts=new{Imported=1,Reused=0,Absent=0},RawComplete=true,HistoricalByteIdentityClaimed=false})!.AsObject();
    public static JsonObject Acquired()=>JsonSerializer.SerializeToNode(new{Complete=true,HistoricalByteIdentityClaimed=false,
        Projection=new{RecipeSha256=RecipeSha},Sources=new[]{new{PhysicalSourceIndex=0,Source=new{Status="reused",Artifact=new StoredArtifact("raw/api.xml",ContentHash.Sha256(SourcesExtractCliCases.SourceBytes),SourcesExtractCliCases.SourceBytes.Length,"application/octet-stream")}}}})!.AsObject();
    private static void Counts(JsonElement value,int imported,int reused,int absent)
    {Assert.Equal(imported,value.GetProperty("Imported").GetInt32());Assert.Equal(reused,value.GetProperty("Reused").GetInt32());Assert.Equal(absent,value.GetProperty("Absent").GetInt32());}
    public static async Task AssertCase(ISourcesSeedCliProbe provider)
    {
        var actual=await provider.RunAsync();Assert.Equal(0,actual.FirstExit);Assert.Equal(0,actual.RepeatExit);Assert.NotEqual(0,actual.RejectedExit);Assert.Equal(0,actual.FetchExit);
        Assert.True(actual.NoAcquisitionBeforeFetch);Assert.True(actual.InputsRetained);Assert.True(actual.BundleRetained);Assert.True(actual.BadReleaseError);
        Assert.Equal(new[]{"raw/api.xml","sources/seed.json"},actual.SeedDataPaths.Order());Assert.Equal(actual.SeedBefore,actual.SeedAfter);
        var sourceSha=ContentHash.Sha256(SourcesExtractCliCases.SourceBytes);Assert.Equal(sourceSha,actual.RawShaBeforeFetch);Assert.Equal(sourceSha,actual.RawShaAfterFetch);
        var summaries=new[]{actual.FirstSummary,actual.RepeatSummary};
        for(var index=0;index<2;index++){
            var summary=summaries[index];Assert.Equal(index==1,summary.GetProperty("Reused").GetBoolean());Assert.Equal(Generation,summary.GetProperty("GenerationId").GetString());
            Counts(summary.GetProperty("CurrentCounts"),index==0?1:0,index==0?0:1,0);Counts(summary.GetProperty("OriginalCounts"),1,0,0);
            Assert.True(summary.GetProperty("RawComplete").GetBoolean());Assert.False(summary.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
            var pin=summary.GetProperty("Manifest").Deserialize<StoredArtifact>()!;Assert.Equal("sources/seed.json",pin.RelativePath);Assert.Equal(ContentHash.Sha256(actual.SeedBefore),pin.Sha256);Assert.Equal(actual.SeedBefore.Length,pin.Length);
        }
        using var seeded=JsonDocument.Parse(actual.SeedBefore);var seed=seeded.RootElement;
        Assert.Equal(Generation,seed.GetProperty("Acquisition").GetProperty("GenerationId").GetString());Assert.Equal(0,seed.GetProperty("Acquisition").GetProperty("CachePolicy").GetInt32());
        Assert.Equal(RecipeSha,seed.GetProperty("Acquisition").GetProperty("Projection").GetProperty("RecipeSha256").GetString());Counts(seed.GetProperty("OriginalCounts"),1,0,0);
        Assert.True(seed.GetProperty("RawComplete").GetBoolean());Assert.False(seed.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
        var entry=Assert.Single(seed.GetProperty("Entries").EnumerateArray());Assert.Equal(0,entry.GetProperty("PhysicalSourceIndex").GetInt32());Assert.Equal(0,entry.GetProperty("Status").GetInt32());Assert.Equal("raw/api.xml",entry.GetProperty("BundleCandidate").GetString());
        var artifact=entry.GetProperty("Artifact").Deserialize<StoredArtifact>()!;Assert.Equal("raw/api.xml",artifact.RelativePath);Assert.Equal(sourceSha,artifact.Sha256);Assert.Equal(SourcesExtractCliCases.SourceBytes.Length,artifact.Length);
        using var acquired=JsonDocument.Parse(actual.AcquisitionReceipt);var acquisition=acquired.RootElement;
        Assert.True(acquisition.GetProperty("Complete").GetBoolean());Assert.False(acquisition.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());Assert.Equal(RecipeSha,acquisition.GetProperty("Projection").GetProperty("RecipeSha256").GetString());
        var raw=Assert.Single(acquisition.GetProperty("Sources").EnumerateArray()).GetProperty("Source");Assert.Equal("reused",raw.GetProperty("Status").GetString());Assert.Equal(artifact,raw.GetProperty("Artifact").Deserialize<StoredArtifact>());
        Assert.Equal(4,actual.Arguments.Count);
        for(var index=0;index<4;index++){
            var args=actual.Arguments[index];Assert.Equal("exec",args[0]);Assert.Contains(args,value=>Path.GetFileName(value)=="AndroidMcp.Build.dll");Assert.DoesNotContain(args,value=>value is "build" or "run" or "--internal-process-probe");
            string Value(string name){var offset=args.ToList().IndexOf(name);Assert.InRange(offset,0,args.Count-2);return args[offset+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal(index==3?"Fetch":"Seed",Value("--operation"));Assert.Equal(actual.RuntimeRoot,Value("--root"));Assert.Equal(actual.ProjectRoot,Value("--project-root"));Assert.Equal(actual.BundleRoot,Value("--bundle-root"));Assert.Equal(actual.OutputRoot,Value("--output-root"));
            Assert.NotEqual(actual.ProjectRoot,actual.OutputRoot);Assert.NotEqual(actual.BundleRoot,actual.OutputRoot);Assert.NotEqual(actual.RuntimeRoot,actual.OutputRoot);
            if(index<3){Assert.Equal("rehydration/fixture.json",Value("--source-manifest"));Assert.Equal("rehydration/fixture.lock.json",Value("--recipe-lock"));Assert.Equal(Generation,Value("--generation"));Assert.Equal(index==2?"wrong-release":"fixture-release",Value("--release"));Assert.DoesNotContain("--limit",args);Assert.DoesNotContain("--cache-policy",args);}
        }
    }
}
public sealed class ScriptedSourcesSeedCliProbe(string mutation=""):ISourcesSeedCliProbe
{
    public Task<SourcesSeedCliObservation> RunAsync()
    {
        var receipt=SourcesSeedCliCases.Receipt();var acquired=SourcesSeedCliCases.Acquired();
        if(mutation=="seed-policy")receipt["Acquisition"]!["CachePolicy"]=1;if(mutation=="seed-recipe")receipt["Acquisition"]!["Projection"]!["RecipeSha256"]="wrong";
        if(mutation=="seed-candidate")receipt["Entries"]![0]!["BundleCandidate"]="snapshots/ignored";
        if(mutation=="fetch-status")acquired["Sources"]![0]!["Source"]!["Status"]="downloaded";if(mutation=="fetch-recipe")acquired["Projection"]!["RecipeSha256"]="wrong";
        var before=JsonSerializer.SerializeToUtf8Bytes(receipt);var first=SourcesSeedCliCases.Summary(before,false);var second=SourcesSeedCliCases.Summary(before,true);
        if(mutation.StartsWith("first:",StringComparison.Ordinal))first.Remove(mutation[6..]);if(mutation.StartsWith("second:",StringComparison.Ordinal))second.Remove(mutation[7..]);
        if(mutation=="current-counts")second["CurrentCounts"]!["Imported"]=1;if(mutation=="original-counts")second["OriginalCounts"]!["Reused"]=1;
        var args=new[]{SourcesSeedCliCases.Arguments("/runtime","/project","/bundle","/output"),SourcesSeedCliCases.Arguments("/runtime","/project","/bundle","/output"),SourcesSeedCliCases.Arguments("/runtime","/project","/bundle","/output",true),SourcesFetchCliCases.Arguments("/runtime","/project","/bundle","/output")};
        if(mutation=="root")args[0][Array.IndexOf(args[0],"--root")+1]="/parent";if(mutation=="child-build")args[0][0]="build";if(mutation=="recipe-name")args[0][Array.IndexOf(args[0],"--source-manifest")+1]="fixture.json";
        var sha=ContentHash.Sha256(SourcesExtractCliCases.SourceBytes);
        return Task.FromResult(new SourcesSeedCliObservation(mutation=="first-exit"?1:0,mutation=="repeat-exit"?1:0,mutation=="reject-exit"?0:1,mutation=="fetch-exit"?1:0,
            JsonSerializer.SerializeToElement(first),JsonSerializer.SerializeToElement(second),before,mutation=="seed-changed"?new byte[]{0}:before,JsonSerializer.SerializeToUtf8Bytes(acquired),
            mutation=="extra-data"?["raw/api.xml","sources/seed.json","unwanted"]:["raw/api.xml","sources/seed.json"],mutation!="acquisition-before-fetch",mutation!="inputs",mutation!="bundle",mutation!="bad-release-error",
            mutation=="raw-before"?"wrong":sha,mutation=="raw-after"?"wrong":sha,args,"/runtime","/project","/bundle","/output"));
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesSeedCliMockTests
{
    [Fact]public Task CorrectSameFixtureSeedReplayAndFetchSeal()=>SourcesSeedCliCases.AssertCase(new ScriptedSourcesSeedCliProbe());
    [Fact]public async Task EveryCounterPinReceiptAndCallerBoundaryDiscriminates()
    {
        var mutations=new[]{"first-exit","repeat-exit","reject-exit","fetch-exit","seed-changed","extra-data","acquisition-before-fetch","inputs","bundle","bad-release-error","raw-before","raw-after","seed-policy","seed-recipe","seed-candidate","fetch-status","fetch-recipe","current-counts","original-counts","root","child-build","recipe-name"}
            .Concat(new[]{"Manifest","Reused","GenerationId","CurrentCounts","OriginalCounts","RawComplete","HistoricalByteIdentityClaimed"}.SelectMany(name=>new[]{"first:"+name,"second:"+name}));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesSeedCliCases.AssertCase(new ScriptedSourcesSeedCliProbe(mutation))));
    }
}
