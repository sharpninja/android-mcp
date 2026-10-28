using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesPlanCliObservation(int FirstExit,int ExistingOutputExit,byte[] ManifestBefore,byte[] ManifestAfter,
    JsonElement Summary,IReadOnlyList<string> OutputPaths,bool InputsRetained,bool FreshOutputError,
    IReadOnlyList<IReadOnlyList<string>> Arguments,string RuntimeRoot,string InputRoot,string OutputRoot);
public interface ISourcesPlanCliProbe{Task<SourcesPlanCliObservation> RunAsync();}
public static class SourcesPlanCliCases
{
    public static readonly ProjectionCase Fixture=new("cli-plan",MissingCacheCount:true);
    public static RehydrationPlanRequest Request()=>RehydrationProjectionCases.Request(Fixture);
    public static string RecipeSha=>ContentHash.Sha256(Request().Recipe.Bytes.Span);
    public static JsonObject MockManifest()=>JsonSerializer.SerializeToNode(new{recipe_file="rehydration/fixture.json",recipe_sha256=RecipeSha,
        source_rows=5,unique_source_paths=4,selected_paths=4,network=false,complete=false,historical_byte_identity_claimed=false,
        required_snapshots=new[]{new{bundle_path=(string?)null}},archive_members=new[]{new{archive_sha256=(string?)null,archive_verified=false},new{archive_sha256=(string?)new string('a',64),archive_verified=false}}})!.AsObject();
    public static JsonObject MockSummary(byte[] bytes)=>JsonSerializer.SerializeToNode(new{Manifest=new StoredArtifact("rehydration-plan.json",ContentHash.Sha256(bytes),bytes.Length,"application/json"),
        RecipeSha256=RecipeSha,SourceRows=5,UniqueSourcePaths=4,SelectedPaths=4,Network=false,Complete=false,HistoricalByteIdentityClaimed=false,
        SourceProjection=RehydrationProjectionCases.Correct(Fixture).Plan!.SourceProjection,Caveats=new[]{"Planning does not establish source availability."}})!.AsObject();
    public static string[] Arguments(string runtime,string input,string output)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Plan","--dotnet-host","/native/dotnet",
        "--project-root",input,"--source-manifest",Path.Combine(input,"rehydration/fixture.json"),"--recipe-lock",Path.Combine(input,"rehydration/fixture.lock.json"),
        "--output-root",output,"--release","fixture-release","--limit","4","--acquisition-profile","CanonicalRestore"];
    public static async Task AssertCase(ISourcesPlanCliProbe provider)
    {
        var actual=await provider.RunAsync();Assert.Equal(0,actual.FirstExit);Assert.NotEqual(0,actual.ExistingOutputExit);
        Assert.True(actual.InputsRetained);Assert.True(actual.FreshOutputError);Assert.Equal(actual.ManifestBefore,actual.ManifestAfter);
        Assert.Equal(new[]{"rehydration-plan.json"},actual.OutputPaths);
        using var document=JsonDocument.Parse(actual.ManifestBefore);var manifest=document.RootElement;
        Assert.Equal("rehydration/fixture.json",manifest.GetProperty("recipe_file").GetString());Assert.Equal(RecipeSha,manifest.GetProperty("recipe_sha256").GetString());
        foreach(var (name,count) in new[]{("source_rows",5),("unique_source_paths",4),("selected_paths",4)})Assert.Equal(count,manifest.GetProperty(name).GetInt32());
        foreach(var flag in new[]{"network","complete","historical_byte_identity_claimed"})Assert.False(manifest.GetProperty(flag).GetBoolean());
        Assert.Equal(JsonValueKind.Null,Assert.Single(manifest.GetProperty("required_snapshots").EnumerateArray()).GetProperty("bundle_path").ValueKind);
        var archives=manifest.GetProperty("archive_members").EnumerateArray().ToArray();Assert.Equal(2,archives.Length);Assert.All(archives,x=>Assert.False(x.GetProperty("archive_verified").GetBoolean()));Assert.Equal(JsonValueKind.Null,archives[0].GetProperty("archive_sha256").ValueKind);
        var summary=actual.Summary;
        Assert.Equal(RecipeSha,summary.GetProperty("RecipeSha256").GetString());
        foreach(var (name,count) in new[]{("SourceRows",5),("UniqueSourcePaths",4),("SelectedPaths",4)})Assert.Equal(count,summary.GetProperty(name).GetInt32());
        foreach(var flag in new[]{"Network","Complete","HistoricalByteIdentityClaimed"})Assert.False(summary.GetProperty(flag).GetBoolean());
        var stored=summary.GetProperty("Manifest");Assert.Equal("rehydration-plan.json",stored.GetProperty("RelativePath").GetString());Assert.Equal(ContentHash.Sha256(actual.ManifestBefore),stored.GetProperty("Sha256").GetString());Assert.Equal(actual.ManifestBefore.Length,stored.GetProperty("Length").GetInt32());
        var projection=summary.GetProperty("SourceProjection");Assert.Equal(5,projection.GetProperty("Rows").GetArrayLength());Assert.Equal(4,projection.GetProperty("PhysicalSources").GetArrayLength());
        var required=projection.GetProperty("PhysicalSources").EnumerateArray().SelectMany(x=>x.GetProperty("Prerequisites").EnumerateArray()).Select(x=>x.GetProperty("Code").GetInt32()).ToArray();
        Assert.Equal(new[]{0,1,2,3,4},required.Order());
        var cache=projection.GetProperty("CacheAliases")[0];Assert.Equal(JsonValueKind.Null,cache.GetProperty("Pin").ValueKind);Assert.Contains(cache.GetProperty("Prerequisites").EnumerateArray(),x=>x.GetProperty("Code").GetInt32()==(int)RehydrationPrerequisiteCode.HistoricalCacheRecordCount);
        Assert.NotEmpty(summary.GetProperty("Caveats").EnumerateArray());
        Assert.Equal(2,actual.Arguments.Count);
        foreach(var args in actual.Arguments)
        {
            Assert.Equal("exec",args[0]);Assert.Contains(args,x=>Path.GetFileName(x)=="AndroidMcp.Build.dll");Assert.DoesNotContain(args,x=>x is "build" or "run" or "--internal-process-probe" or "--bundle-root");
            string Value(string key){var index=args.ToList().IndexOf(key);Assert.InRange(index,0,args.Count-2);return args[index+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal("Plan",Value("--operation"));Assert.Equal(actual.RuntimeRoot,Value("--root"));
            Assert.Equal(actual.InputRoot,Value("--project-root"));Assert.Equal(actual.OutputRoot,Value("--output-root"));
            Assert.Equal(Path.Combine(actual.InputRoot,"rehydration/fixture.json"),Value("--source-manifest"));Assert.Equal(Path.Combine(actual.InputRoot,"rehydration/fixture.lock.json"),Value("--recipe-lock"));
            Assert.NotEqual(actual.InputRoot,actual.RuntimeRoot);Assert.NotEqual(actual.OutputRoot,actual.RuntimeRoot);
        }
    }
}
public sealed class ScriptedSourcesPlanCliProbe(string? mutation=null):ISourcesPlanCliProbe
{
    public Task<SourcesPlanCliObservation> RunAsync()
    {
        var manifest=SourcesPlanCliCases.MockManifest();
        if(mutation?.StartsWith("manifest:",StringComparison.Ordinal)==true)manifest.Remove(mutation[9..]);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest);var summary=SourcesPlanCliCases.MockSummary(bytes);
        if(mutation?.StartsWith("summary:",StringComparison.Ordinal)==true)summary.Remove(mutation[8..]);
        if(mutation=="snapshot-invented")manifest["required_snapshots"]![0]!["bundle_path"]="invented";
        if(mutation=="archive-claimed")manifest["archive_members"]![0]!["archive_verified"]=true;
        if(mutation=="missing-proof")summary["SourceProjection"]!["PhysicalSources"]![1]!["Prerequisites"]=new JsonArray();
        if(mutation=="cache-count-invented")summary["SourceProjection"]!["CacheAliases"]![0]!["Pin"]=new JsonObject{["RecordCount"]=0};
        if(mutation is "snapshot-invented" or "archive-claimed"){bytes=JsonSerializer.SerializeToUtf8Bytes(manifest);summary["Manifest"]!["Sha256"]=ContentHash.Sha256(bytes);summary["Manifest"]!["Length"]=bytes.Length;}
        var args=SourcesPlanCliCases.Arguments("/test/runtime","/test/input","/test/output");
        if(mutation=="root")args[Array.IndexOf(args,"--root")+1]="/parent";if(mutation=="child-build")args[0]="build";
        if(mutation=="basename")args[Array.IndexOf(args,"--source-manifest")+1]="fixture.json";
        return Task.FromResult(new SourcesPlanCliObservation(mutation=="first-exit"?1:0,mutation=="overwrite"?0:1,bytes,mutation=="changed-existing"?[0]:bytes,
            JsonSerializer.SerializeToElement(summary),mutation=="extra-output"?["rehydration-plan.json","blobs/a"]:["rehydration-plan.json"],mutation!="inputs",mutation!="fresh-error",[args,args],"/test/runtime","/test/input","/test/output"));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesPlanCliMockTests
{
    [Fact]public Task CorrectPurePlanMetadataAndFreshOutput()=>SourcesPlanCliCases.AssertCase(new ScriptedSourcesPlanCliProbe());
    [Fact]public async Task EveryWrongClaimMissingProofOrInvocationFails()
    {
        var mutations=new[]{"first-exit","overwrite","changed-existing","extra-output","inputs","fresh-error","snapshot-invented","archive-claimed","missing-proof","cache-count-invented","root","child-build","basename"}
            .Concat(new[]{"recipe_file","recipe_sha256","source_rows","unique_source_paths","selected_paths","network","complete","historical_byte_identity_claimed","required_snapshots","archive_members"}.Select(x=>"manifest:"+x))
            .Concat(new[]{"RecipeSha256","SourceRows","UniqueSourcePaths","SelectedPaths","Network","Complete","HistoricalByteIdentityClaimed","Manifest","SourceProjection","Caveats"}.Select(x=>"summary:"+x));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesPlanCliCases.AssertCase(new ScriptedSourcesPlanCliProbe(mutation))));
    }
}
