using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesInventoryCliObservation(int FirstExit,int ExistingOutputExit,int WrongPinExit,
    byte[] ManifestBefore,byte[] ManifestAfter,byte[] AcquisitionBytes,JsonElement Summary,
    IReadOnlyList<string> OutputPaths,IReadOnlyList<string> WrongPinOutputPaths,
    IReadOnlyDictionary<string,string> AcquisitionBefore,IReadOnlyDictionary<string,string> AcquisitionAfter,
    bool InputsRetained,bool BundleRetained,bool FreshOutputError,bool WrongPinError,
    IReadOnlyList<IReadOnlyList<string>> Arguments,string RuntimeRoot,string InputRoot,string AcquisitionRoot,
    string OutputRoot,string WrongPinOutputRoot,string DotNetHost);
public interface ISourcesInventoryCliProbe{Task<SourcesInventoryCliObservation> RunAsync();}
public static class SourcesInventoryCliCases
{
    public const string ManifestPath="sources/inventory.json";
    public const string AcquisitionPath="sources/acquisition.json";
    public static JsonArray RecipeRows()=>JsonNode.Parse(SourcesFetchCliCases.Request().Recipe.Bytes.Span)!["inventory"]!["sources"]!.AsArray();
    public static StoredArtifact AcquisitionPin(byte[] bytes)=>new(AcquisitionPath,ContentHash.Sha256(bytes),bytes.Length,"application/json");
    public static string[] Arguments(string runtime,string input,string acquisition,string output,StoredArtifact pin)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Inventory","--dotnet-host","/native/dotnet",
        "--project-root",input,"--source-manifest",Path.Combine(input,"rehydration/fixture.json"),"--recipe-lock",Path.Combine(input,"rehydration/fixture.lock.json"),
        "--acquisition-root",acquisition,"--acquisition-manifest",pin.RelativePath,"--acquisition-manifest-sha256",pin.Sha256,
        "--acquisition-manifest-length",pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),"--output-root",output,
        "--release","fixture-release","--limit","2","--acquisition-profile","CanonicalRestore"];
    public static JsonObject MockManifest(StoredArtifact pin)
    {
        var rows=RecipeRows();var physical=new JsonArray();var logical=new JsonArray();var sources=new JsonArray();var entries=new JsonArray();
        for(var index=0;index<2;index++)
        {
            var row=rows[index]!;var local=row["local_path"]!.GetValue<string>();var sha=row["sha256"]!.GetValue<string>();
            var snapshot=row["transport"]!["required_local_snapshot"]!.GetValue<string>();var size=row["size_bytes"]?.GetValue<long>();
            var acquired=JsonSerializer.SerializeToNode(new AcquiredSource(new(local,sha,3,"application/octet-stream"),"downloaded",new Uri(row["url"]!.GetValue<string>()),"fixture-bundle-v1",null))!;
            physical.Add(JsonSerializer.SerializeToNode(new{PhysicalSourceIndex=index,Pin=new SourcePin(local,new Uri(row["url"]!.GetValue<string>()),"fixture-bundle-v1",sha,size,RequiredLocalSnapshot:snapshot),
                RequiredSnapshots=new[]{new SnapshotRequirement(row["path"]!.GetValue<string>(),local,snapshot,sha,size)}}));
            logical.Add(new JsonObject{["RowIndex"]=index,["LogicalPath"]=row["path"]!.GetValue<string>(),["Kind"]="signature",["PhysicalSourceIndex"]=index,["CacheAliasIndex"]=null,
                ["Context"]=JsonSerializer.SerializeToNode(new{Repository=(string?)null,Revision="fixture-bundle-v1"}),["OriginalRow"]=row.DeepClone()});
            sources.Add(new JsonObject{["PhysicalSourceIndex"]=index,["Source"]=acquired.DeepClone()});
            entries.Add(new JsonObject{["PhysicalSourceIndex"]=index,["Disposition"]=(int)SourceInventoryDisposition.VerifiedSelected,["VerifiedSource"]=acquired});
        }
        var receipt=SourcesFetchCliCases.MockReceipt();receipt["Sources"]=sources;receipt["HistoricalCaches"]=new JsonArray();
        receipt["Projection"]!["Rows"]=logical;receipt["Projection"]!["PhysicalSources"]=physical;
        receipt["Projection"]!["SelectedPhysicalSourceIndices"]=new JsonArray(0,1);receipt["Projection"]!["CacheAliases"]=new JsonArray();
        return new(){["Acquisition"]=new JsonObject{["Manifest"]=JsonSerializer.SerializeToNode(pin),["Reused"]=true,["Receipt"]=receipt},["Entries"]=entries,
            ["VerifiedPhysicalSources"]=2,["UnselectedPhysicalSources"]=0,["RawComplete"]=true,["ExtractionComplete"]=false,["HistoricalByteIdentityClaimed"]=false};
    }
    public static JsonObject MockSummary(byte[] bytes,StoredArtifact acquisition)=>JsonSerializer.SerializeToNode(new{
        Manifest=new StoredArtifact(ManifestPath,ContentHash.Sha256(bytes),bytes.Length,"application/json"),RecipeSha256=SourcesFetchCliCases.RecipeSha,
        SourceRows=2,VerifiedPhysicalSources=2,UnselectedPhysicalSources=0,RawComplete=true,ExtractionComplete=false,HistoricalByteIdentityClaimed=false,AcquisitionManifest=acquisition})!.AsObject();
    private static void AssertPin(StoredArtifact expected,JsonElement actual)
    {
        Assert.Equal(expected.RelativePath,actual.GetProperty("RelativePath").GetString());Assert.Equal(expected.Sha256,actual.GetProperty("Sha256").GetString());
        Assert.Equal(expected.Length,actual.GetProperty("Length").GetInt64());Assert.Equal(expected.MediaType,actual.GetProperty("MediaType").GetString());
    }
    public static async Task AssertCase(ISourcesInventoryCliProbe provider)
    {
        var actual=await provider.RunAsync();Assert.Equal(0,actual.FirstExit);Assert.NotEqual(0,actual.ExistingOutputExit);Assert.NotEqual(0,actual.WrongPinExit);
        Assert.True(actual.FreshOutputError);Assert.True(actual.WrongPinError);Assert.True(actual.InputsRetained);Assert.True(actual.BundleRetained);
        Assert.Equal(actual.ManifestBefore,actual.ManifestAfter);Assert.Equal(new[]{ManifestPath},actual.OutputPaths);Assert.Empty(actual.WrongPinOutputPaths);
        Assert.Equal(actual.AcquisitionBefore.OrderBy(item=>item.Key),actual.AcquisitionAfter.OrderBy(item=>item.Key));
        Assert.Equal(ContentHash.Sha256(actual.AcquisitionBytes),actual.AcquisitionBefore[AcquisitionPath]);
        foreach(var item in SourcesFetchCliCases.ExpectedData)Assert.Equal(item.Value,actual.AcquisitionBefore[item.Key]);
        var pin=AcquisitionPin(actual.AcquisitionBytes);
        using var document=JsonDocument.Parse(actual.ManifestBefore);var report=document.RootElement;
        Assert.Equal(2,report.GetProperty("VerifiedPhysicalSources").GetInt32());Assert.Equal(0,report.GetProperty("UnselectedPhysicalSources").GetInt32());
        Assert.True(report.GetProperty("RawComplete").GetBoolean());Assert.False(report.GetProperty("ExtractionComplete").GetBoolean());Assert.False(report.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
        var acquisition=report.GetProperty("Acquisition");Assert.True(acquisition.GetProperty("Reused").GetBoolean());AssertPin(pin,acquisition.GetProperty("Manifest"));
        var receipt=acquisition.GetProperty("Receipt");Assert.Equal(SourcesFetchCliCases.Generation,receipt.GetProperty("GenerationId").GetString());
        Assert.True(receipt.GetProperty("Complete").GetBoolean());Assert.False(receipt.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());Assert.Empty(receipt.GetProperty("HistoricalCaches").EnumerateArray());
        var projection=receipt.GetProperty("Projection");Assert.Equal(SourcesFetchCliCases.RecipeSha,projection.GetProperty("RecipeSha256").GetString());
        Assert.Equal((int)AcquisitionCompatibilityProfile.CanonicalRestore,projection.GetProperty("Profile").GetInt32());Assert.Empty(projection.GetProperty("CacheAliases").EnumerateArray());
        Assert.Equal(new[]{0,1},projection.GetProperty("SelectedPhysicalSourceIndices").EnumerateArray().Select(item=>item.GetInt32()));
        var logical=projection.GetProperty("Rows").EnumerateArray().ToArray();var physical=projection.GetProperty("PhysicalSources").EnumerateArray().ToArray();var entries=report.GetProperty("Entries").EnumerateArray().ToArray();
        Assert.Equal(2,logical.Length);Assert.Equal(2,physical.Length);Assert.Equal(2,entries.Length);
        var expectedRows=RecipeRows();var canonical=new CanonicalJson();
        for(var index=0;index<2;index++)
        {
            var expected=expectedRows[index]!;var local=expected["local_path"]!.GetValue<string>();var sha=expected["sha256"]!.GetValue<string>();var snapshot=expected["transport"]!["required_local_snapshot"]!.GetValue<string>();
            Assert.Equal(index,logical[index].GetProperty("RowIndex").GetInt32());Assert.Equal(index,logical[index].GetProperty("PhysicalSourceIndex").GetInt32());
            Assert.Equal(expected["path"]!.GetValue<string>(),logical[index].GetProperty("LogicalPath").GetString());Assert.Equal("signature",logical[index].GetProperty("Kind").GetString());Assert.Equal(JsonValueKind.Null,logical[index].GetProperty("CacheAliasIndex").ValueKind);
            Assert.Equal(canonical.Serialize(JsonSerializer.SerializeToElement(expected)),canonical.Serialize(logical[index].GetProperty("OriginalRow")));
            Assert.Equal("fixture-bundle-v1",logical[index].GetProperty("Context").GetProperty("Revision").GetString());Assert.Equal(JsonValueKind.Null,logical[index].GetProperty("Context").GetProperty("Repository").ValueKind);
            Assert.Equal(index,physical[index].GetProperty("PhysicalSourceIndex").GetInt32());var sourcePin=physical[index].GetProperty("Pin");
            Assert.Equal(local,sourcePin.GetProperty("LocalPath").GetString());Assert.Equal(sha,sourcePin.GetProperty("Sha256").GetString());Assert.Equal(snapshot,sourcePin.GetProperty("RequiredLocalSnapshot").GetString());
            Assert.Equal(expected["url"]!.GetValue<string>(),sourcePin.GetProperty("Url").GetString());Assert.Equal("fixture-bundle-v1",sourcePin.GetProperty("Revision").GetString());
            if(index==0)Assert.Equal(3,sourcePin.GetProperty("SizeBytes").GetInt32());else Assert.Equal(JsonValueKind.Null,sourcePin.GetProperty("SizeBytes").ValueKind);
            var required=Assert.Single(physical[index].GetProperty("RequiredSnapshots").EnumerateArray());Assert.Equal(snapshot,required.GetProperty("BundlePath").GetString());Assert.Equal(local,required.GetProperty("LocalPath").GetString());Assert.Equal(sha,required.GetProperty("Sha256").GetString());
            Assert.Equal(index,entries[index].GetProperty("PhysicalSourceIndex").GetInt32());Assert.Equal((int)SourceInventoryDisposition.VerifiedSelected,entries[index].GetProperty("Disposition").GetInt32());
            var verified=entries[index].GetProperty("VerifiedSource");AssertPin(new(local,sha,3,"application/octet-stream"),verified.GetProperty("Artifact"));
            Assert.Equal("downloaded",verified.GetProperty("Status").GetString());Assert.Equal(expected["url"]!.GetValue<string>(),verified.GetProperty("Url").GetString());Assert.Equal("fixture-bundle-v1",verified.GetProperty("Revision").GetString());
        }
        var summary=actual.Summary;AssertPin(new(ManifestPath,ContentHash.Sha256(actual.ManifestBefore),actual.ManifestBefore.Length,"application/json"),summary.GetProperty("Manifest"));AssertPin(pin,summary.GetProperty("AcquisitionManifest"));
        Assert.Equal(SourcesFetchCliCases.RecipeSha,summary.GetProperty("RecipeSha256").GetString());Assert.Equal(2,summary.GetProperty("SourceRows").GetInt32());Assert.Equal(2,summary.GetProperty("VerifiedPhysicalSources").GetInt32());Assert.Equal(0,summary.GetProperty("UnselectedPhysicalSources").GetInt32());
        Assert.True(summary.GetProperty("RawComplete").GetBoolean());Assert.False(summary.GetProperty("ExtractionComplete").GetBoolean());Assert.False(summary.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
        Assert.Equal(3,actual.Arguments.Count);
        for(var index=0;index<3;index++)
        {
            var args=actual.Arguments[index];Assert.Equal("exec",args[0]);Assert.Contains(args,item=>Path.GetFileName(item)=="AndroidMcp.Build.dll");Assert.DoesNotContain(args,item=>item is "build" or "run" or "restore" or "--bundle-root" or "--internal-process-probe");
            string Value(string name){var at=args.ToList().IndexOf(name);Assert.InRange(at,0,args.Count-2);return args[at+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal("Inventory",Value("--operation"));Assert.Equal(actual.RuntimeRoot,Value("--root"));Assert.Equal(actual.DotNetHost,Value("--dotnet-host"));
            Assert.Equal(actual.InputRoot,Value("--project-root"));Assert.Equal(actual.AcquisitionRoot,Value("--acquisition-root"));Assert.Equal(index==2?actual.WrongPinOutputRoot:actual.OutputRoot,Value("--output-root"));
            Assert.Equal(Path.Combine(actual.InputRoot,"rehydration/fixture.json"),Value("--source-manifest"));Assert.Equal(Path.Combine(actual.InputRoot,"rehydration/fixture.lock.json"),Value("--recipe-lock"));
            Assert.Equal(AcquisitionPath,Value("--acquisition-manifest"));Assert.Equal(index==2?new string('0',64):pin.Sha256,Value("--acquisition-manifest-sha256"));Assert.Equal(pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),Value("--acquisition-manifest-length"));
            Assert.Equal("fixture-release",Value("--release"));Assert.Equal("2",Value("--limit"));Assert.Equal("CanonicalRestore",Value("--acquisition-profile"));
            Assert.NotEqual(actual.InputRoot,actual.RuntimeRoot);Assert.NotEqual(actual.AcquisitionRoot,actual.RuntimeRoot);Assert.NotEqual(actual.OutputRoot,actual.RuntimeRoot);
        }
    }
}
public sealed class ScriptedSourcesInventoryCliProbe(string? mutation=null):ISourcesInventoryCliProbe
{
    public Task<SourcesInventoryCliObservation> RunAsync()
    {
        var acquisition=JsonSerializer.SerializeToUtf8Bytes(SourcesFetchCliCases.MockReceipt());var pin=SourcesInventoryCliCases.AcquisitionPin(acquisition);
        var report=SourcesInventoryCliCases.MockManifest(pin);
        if(mutation?.StartsWith("report:",StringComparison.Ordinal)==true)report.Remove(mutation[7..]);
        if(mutation=="recipe")report["Acquisition"]!["Receipt"]!["Projection"]!["RecipeSha256"]="wrong";
        if(mutation=="context")report["Acquisition"]!["Receipt"]!["Projection"]!["Rows"]![0]!["Context"]!["Revision"]="wrong";
        if(mutation=="original-row")report["Acquisition"]!["Receipt"]!["Projection"]!["Rows"]![0]!["OriginalRow"]!["path"]="wrong";
        if(mutation=="unknown-size")report["Acquisition"]!["Receipt"]!["Projection"]!["PhysicalSources"]![1]!["Pin"]!["SizeBytes"]=3;
        if(mutation=="snapshot")report["Acquisition"]!["Receipt"]!["Projection"]!["PhysicalSources"]![0]!["RequiredSnapshots"]![0]!["BundlePath"]="wrong";
        if(mutation=="reused")report["Acquisition"]!["Reused"]=false;
        if(mutation=="status")report["Entries"]![0]!["VerifiedSource"]!["Status"]="wrong";
        if(mutation=="source-sha")report["Entries"]![0]!["VerifiedSource"]!["Artifact"]!["Sha256"]="wrong";
        if(mutation=="acquisition-pin")report["Acquisition"]!["Manifest"]!["Sha256"]="wrong";
        var bytes=JsonSerializer.SerializeToUtf8Bytes(report);var summary=SourcesInventoryCliCases.MockSummary(bytes,pin);
        if(mutation?.StartsWith("summary:",StringComparison.Ordinal)==true)summary.Remove(mutation[8..]);
        var outputs=SourcesFetchCliCases.ExpectedData.ToDictionary(item=>item.Key,item=>item.Value);outputs[SourcesInventoryCliCases.AcquisitionPath]=pin.Sha256;
        var after=outputs.ToDictionary(item=>item.Key,item=>item.Value);if(mutation=="acquisition-changed")after["blobs/a"]="wrong";
        var args=SourcesInventoryCliCases.Arguments("/test/runtime","/test/input","/test/acquisition","/test/output",pin);
        var bad=SourcesInventoryCliCases.Arguments("/test/runtime","/test/input","/test/acquisition","/test/wrong-pin",pin with{Sha256=new string('0',64)});
        if(mutation?.StartsWith("argument:",StringComparison.Ordinal)==true)args[Array.IndexOf(args,mutation[9..])+1]="wrong";
        if(mutation=="child-build")args[0]="build";
        return Task.FromResult(new SourcesInventoryCliObservation(mutation=="exit"?1:0,mutation=="existing"?0:1,mutation=="wrong-pin-exit"?0:1,
            bytes,mutation=="overwrite"?[0]:bytes,acquisition,JsonSerializer.SerializeToElement(summary),mutation=="extra-output"?[SourcesInventoryCliCases.ManifestPath,"extra"]:[SourcesInventoryCliCases.ManifestPath],
            mutation=="bad-output"?["unexpected"]:[],outputs,after,mutation!="inputs",mutation!="bundle",mutation!="fresh-error",mutation!="pin-error",
            [args,args,bad],"/test/runtime","/test/input","/test/acquisition","/test/output","/test/wrong-pin","/native/dotnet"));
    }
}
[Trait("Gate","Mock")]
public sealed class SourcesInventoryCliMockTests
{
    [Fact]public Task CorrectPinnedAcquisitionProvenanceAndCliAdmission()=>SourcesInventoryCliCases.AssertCase(new ScriptedSourcesInventoryCliProbe());
    [Fact]public async Task EveryWrongClaimPinMutationOrInvocationIsRejected()
    {
        var mutations=new[]{"exit","existing","wrong-pin-exit","overwrite","extra-output","bad-output","inputs","bundle","fresh-error","pin-error","acquisition-changed","recipe","context","original-row","unknown-size","snapshot","reused","status","source-sha","acquisition-pin","child-build"}
            .Concat(new[]{"Acquisition","Entries","VerifiedPhysicalSources","UnselectedPhysicalSources","RawComplete","ExtractionComplete","HistoricalByteIdentityClaimed"}.Select(name=>"report:"+name))
            .Concat(new[]{"Manifest","AcquisitionManifest","RecipeSha256","SourceRows","VerifiedPhysicalSources","UnselectedPhysicalSources","RawComplete","ExtractionComplete","HistoricalByteIdentityClaimed"}.Select(name=>"summary:"+name))
            .Concat(new[]{"--root","--dotnet-host","--project-root","--source-manifest","--recipe-lock","--acquisition-root","--acquisition-manifest","--acquisition-manifest-sha256","--acquisition-manifest-length","--output-root","--release","--limit","--acquisition-profile","--target","--operation"}.Select(name=>"argument:"+name));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesInventoryCliCases.AssertCase(new ScriptedSourcesInventoryCliProbe(mutation))));
    }
}
