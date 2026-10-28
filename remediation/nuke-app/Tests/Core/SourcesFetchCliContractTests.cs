using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesFetchCliObservation(int FirstExit,int ResumeExit,byte[] ReceiptBefore,byte[] ReceiptAfter,
    IReadOnlyDictionary<string,string> OutputsBefore,IReadOnlyDictionary<string,string> OutputsAfter,
    bool InputsRetained,bool BundleRetained,IReadOnlyList<IReadOnlyList<string>> Arguments,
    string RuntimeRoot,string InputRoot,string BundleRoot,string OutputRoot);
public interface ISourcesFetchCliProbe { Task<SourcesFetchCliObservation> RunAsync(); }
public static class SourcesFetchCliCases
{
    public const string Generation="cli-bundle-generation";
    public static RehydrationPlanRequest Request()=>RehydrationContractCases.Request(root=>
    {
        for(var index=0;index<2;index++)
        {
            var source=RehydrationContractCases.Source(root,index);
            source["revision"]="fixture-bundle-v1";source["kind"]="signature";
            source.Remove("metadata_cache");
            source["transport"]=new JsonObject{["immutable_url"]=false,["required_local_snapshot"]=index==0?"snapshots/a.txt":"snapshots/api.txt"};
        }
    });
    public static string RecipeSha=>ContentHash.Sha256(Request().Recipe.Bytes.Span);
    public static string LeasePath=>".leases/"+ContentHash.Sha256(Encoding.UTF8.GetBytes("source-batch/sources/acquisition.json"))+".lock";
    public static IReadOnlyDictionary<string,string> ExpectedData=>new Dictionary<string,string>
    {
        ["blobs/a"]=ContentHash.Sha256(Encoding.ASCII.GetBytes("abc")),["blobs/b"]=ContentHash.Sha256(Encoding.ASCII.GetBytes("def")),
        ["snapshots/a.txt"]=ContentHash.Sha256(Encoding.ASCII.GetBytes("abc")),["snapshots/api.txt"]=ContentHash.Sha256(Encoding.ASCII.GetBytes("def"))
    };
    public static JsonObject MockReceipt()=>JsonSerializer.SerializeToNode(new{GenerationId=Generation,TotalRows=2,SelectedRows=2,ProcessedRows=2,
        TotalPhysicalSources=2,SelectedPhysicalSources=2,Complete=true,HistoricalByteIdentityClaimed=false,
        Projection=new{RecipeSha256=RecipeSha,Profile=AcquisitionCompatibilityProfile.CanonicalRestore,
            PhysicalSources=new[]{new{Pin=new{LocalPath="blobs/a",RequiredLocalSnapshot="snapshots/a.txt",Url="https://example.invalid/a"}},new{Pin=new{LocalPath="blobs/b",RequiredLocalSnapshot="snapshots/api.txt",Url="https://example.invalid/mutable"}}}}})!.AsObject();
    public static string[] Arguments(string runtime,string input,string bundle,string output)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Fetch",
        "--dotnet-host","/native/dotnet","--project-root",input,"--source-manifest",Path.Combine(input,"rehydration/fixture.json"),
        "--recipe-lock",Path.Combine(input,"rehydration/fixture.lock.json"),"--output-root",output,"--bundle-root",bundle,
        "--generation",Generation,"--release","fixture-release","--limit","2","--acquisition-profile","CanonicalRestore","--cache-policy","PreserveAliases"];
    public static async Task AssertCase(ISourcesFetchCliProbe provider)
    {
        var observed=await provider.RunAsync();Assert.Equal(0,observed.FirstExit);Assert.Equal(0,observed.ResumeExit);
        Assert.True(observed.InputsRetained);Assert.True(observed.BundleRetained);Assert.Equal(observed.ReceiptBefore,observed.ReceiptAfter);
        Assert.Equal(ExpectedData.Keys.Append(LeasePath).Append("sources/acquisition.json").Order(),observed.OutputsBefore.Keys.Order());
        Assert.Equal(observed.OutputsBefore.OrderBy(x=>x.Key),observed.OutputsAfter.OrderBy(x=>x.Key));
        foreach(var item in ExpectedData)Assert.Equal(item.Value,observed.OutputsBefore[item.Key]);
        Assert.Equal(ContentHash.Sha256(observed.ReceiptBefore),observed.OutputsBefore["sources/acquisition.json"]);
        using var receipt=JsonDocument.Parse(observed.ReceiptBefore);var root=receipt.RootElement;
        Assert.Equal(Generation,root.GetProperty("GenerationId").GetString());
        foreach(var field in new[]{"TotalRows","SelectedRows","ProcessedRows","TotalPhysicalSources","SelectedPhysicalSources"})Assert.Equal(2,root.GetProperty(field).GetInt32());
        Assert.True(root.GetProperty("Complete").GetBoolean());Assert.False(root.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
        var projection=root.GetProperty("Projection");Assert.Equal(RecipeSha,projection.GetProperty("RecipeSha256").GetString());
        Assert.Equal((int)AcquisitionCompatibilityProfile.CanonicalRestore,projection.GetProperty("Profile").GetInt32());
        var pins=projection.GetProperty("PhysicalSources").EnumerateArray().Select(x=>x.GetProperty("Pin")).ToArray();Assert.Equal(2,pins.Length);
        Assert.Equal(new[]{"blobs/a","blobs/b"},pins.Select(x=>x.GetProperty("LocalPath").GetString()));
        Assert.Equal(new[]{"snapshots/a.txt","snapshots/api.txt"},pins.Select(x=>x.GetProperty("RequiredLocalSnapshot").GetString()));
        Assert.All(pins,pin=>Assert.Equal("example.invalid",new Uri(pin.GetProperty("Url").GetString()!).Host));
        Assert.Equal(2,observed.Arguments.Count);
        foreach(var args in observed.Arguments)
        {
            Assert.Equal("exec",args[0]);Assert.Contains(args,x=>Path.GetFileName(x)=="AndroidMcp.Build.dll");
            Assert.DoesNotContain(args,x=>x is "build" or "run" or "--internal-process-probe");
            string Value(string name){var index=args.ToList().IndexOf(name);Assert.InRange(index,0,args.Count-2);return args[index+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal("Fetch",Value("--operation"));
            Assert.Equal(observed.RuntimeRoot,Value("--root"));Assert.Equal(observed.InputRoot,Value("--project-root"));
            Assert.Equal(observed.BundleRoot,Value("--bundle-root"));Assert.Equal(observed.OutputRoot,Value("--output-root"));
            Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/fixture.json"),Value("--source-manifest"));
            Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/fixture.lock.json"),Value("--recipe-lock"));
            Assert.NotEqual(observed.InputRoot,observed.RuntimeRoot);Assert.NotEqual(observed.OutputRoot,observed.RuntimeRoot);Assert.NotEqual(observed.BundleRoot,observed.RuntimeRoot);
        }
    }
}
public sealed class ScriptedSourcesFetchCliProbe(string? mutation=null):ISourcesFetchCliProbe
{
    public Task<SourcesFetchCliObservation> RunAsync()
    {
        var receipt=SourcesFetchCliCases.MockReceipt();
        if(mutation?.StartsWith("receipt:",StringComparison.Ordinal)==true)receipt.Remove(mutation[8..]);
        if(mutation=="recipe-sha")receipt["Projection"]!["RecipeSha256"]="wrong";
        if(mutation=="profile")receipt["Projection"]!["Profile"]=99;
        if(mutation=="snapshot")receipt["Projection"]!["PhysicalSources"]![0]!["Pin"]!["RequiredLocalSnapshot"]=null;
        if(mutation=="upstream")receipt["Projection"]!["PhysicalSources"]![0]!["Pin"]!["Url"]="https://mutable.test/a";
        var before=JsonSerializer.SerializeToUtf8Bytes(receipt);var after=mutation=="repeat-bytes"?new byte[]{0}:before;
        var outputs=SourcesFetchCliCases.ExpectedData.ToDictionary(x=>x.Key,x=>x.Value);outputs[SourcesFetchCliCases.LeasePath]=ContentHash.Sha256(Array.Empty<byte>());outputs["sources/acquisition.json"]=ContentHash.Sha256(before);
        if(mutation=="raw-bytes")outputs["blobs/a"]="wrong";if(mutation=="extra-output")outputs["unwanted"]="wrong";
        var second=outputs.ToDictionary(x=>x.Key,x=>x.Value);if(mutation=="repeat-output")second["blobs/b"]="wrong";
        var args=SourcesFetchCliCases.Arguments("/test/runtime","/test/input","/test/bundle","/test/output");
        if(mutation=="child-build")args[0]="build";if(mutation=="root")args[Array.IndexOf(args,"--root")+1]="/parent";
        if(mutation=="recipe-basename")args[Array.IndexOf(args,"--source-manifest")+1]="fixture.json";
        if(mutation=="lock-basename")args[Array.IndexOf(args,"--recipe-lock")+1]="fixture.lock.json";
        return Task.FromResult(new SourcesFetchCliObservation(mutation=="first-exit"?1:0,mutation=="resume-exit"?1:0,before,after,outputs,second,
            mutation!="inputs",mutation!="bundle",[args,args],"/test/runtime","/test/input","/test/bundle","/test/output"));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchCliMockTests
{
    [Fact]public Task CorrectBundledResumeFixture()=>SourcesFetchCliCases.AssertCase(new ScriptedSourcesFetchCliProbe());
    [Fact]public async Task EveryOutputPinCounterAndInvocationAssertionDiscriminates()
    {
        var mutations=new[]{"first-exit","resume-exit","inputs","bundle","repeat-bytes","raw-bytes","extra-output","repeat-output","recipe-sha","profile","snapshot","upstream","child-build","root","recipe-basename","lock-basename"}
            .Concat(new[]{"GenerationId","TotalRows","SelectedRows","ProcessedRows","TotalPhysicalSources","SelectedPhysicalSources","Complete","HistoricalByteIdentityClaimed","Projection"}.Select(x=>"receipt:"+x));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesFetchCliCases.AssertCase(new ScriptedSourcesFetchCliProbe(mutation))));
    }
}
