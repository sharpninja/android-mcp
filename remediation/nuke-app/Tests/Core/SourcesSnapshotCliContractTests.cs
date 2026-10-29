using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SnapshotCliCase(string Id,string Change="default",string Mode="Restore",bool Download=false,int Existing=0,
    int Exit=0,bool Json=true,bool Bundle=false,bool Lease=false,bool Seal=false,string? Error=null,bool Generic=false)
{public override string ToString()=>Id;}
public sealed record SnapshotCliObservation(int Exit,string? Json,string Transcript,IReadOnlyDictionary<string,string> Before,
    IReadOnlyDictionary<string,string> After,byte[]? Seal,bool InputsRetained,bool BundleRetained,bool ProofsRetained,
    bool OutputExists,IReadOnlyList<string> Arguments,string RuntimeRoot,string InputRoot,string BundleRoot,string OutputRoot,
    int? RepeatedExit=null,int? RepeatedDownloaded=null,bool RepeatedOutputsRetained=true);
public interface ISnapshotCliProbe{Task<SnapshotCliObservation> RunAsync(SnapshotCliCase test);}
public static class SourcesSnapshotCliCases
{
    public const string SealPath="sources/snapshot.json";
    public const string EnvelopePath="proof-inputs.json";
    public const string ProofReceipt="proof/receipt.json";
    public const string ProofXml="repository2-3.xml";
    public static readonly byte[] Receipt=Encoding.UTF8.GetBytes("{\"etag\":\"\\\"one\\\"\"}");
    public static readonly byte[] Xml=Encoding.UTF8.GetBytes("<repository><remotePackage path=\"wrong\"><archives><archive><complete><size>170000</size></complete></archive></archives></remotePackage></repository>");
    public static string LeasePath=>".leases/"+ContentHash.Sha256(Encoding.UTF8.GetBytes("source-batch/"+SealPath))+".lock";
    public static bool Proof(SnapshotCliCase c)=>c.Change.StartsWith("proof-",StringComparison.Ordinal);
    public static bool RecipeBundle(SnapshotCliCase c)=>c.Change.StartsWith("bundle",StringComparison.Ordinal)||c.Change=="fetch-bundle";
    public static IEnumerable<object[]> Cases=>new[]{
        new SnapshotCliCase("restore-default-no-bundle-no-output"),
        new SnapshotCliCase("fetch-dry-run-is-one",Mode:"Fetch",Exit:1),
        new SnapshotCliCase("restore-one-existing-first-git-winner",Existing:1),
        new SnapshotCliCase("restore-all-existing-download-no-seal",Download:true,Existing:2),
        new SnapshotCliCase("fetch-existing-corrupt-is-not-verified",Change:"fetch-existing",Mode:"Fetch",Download:true,Existing:2),
        new SnapshotCliCase("fetch-existing-dry-run-still-one",Change:"fetch-existing",Mode:"Fetch",Existing:2,Exit:1),
        new SnapshotCliCase("restore-corrupt-original-retained",Change:"corrupt",Download:true,Existing:1,Exit:1,Json:false,Error:"Source SHA256 differs"),
        new SnapshotCliCase("explicit-bundle-download-and-retained-reuse",Change:"bundle",Download:true,Bundle:true,Lease:true,Seal:true),
        new SnapshotCliCase("missing-bundle-member-no-seal",Change:"bundle-missing",Download:true,Bundle:true,Lease:true,Exit:1,Json:false,Error:"Source batch prerequisite"),
        new SnapshotCliCase("corrupt-bundle-member-no-seal",Change:"bundle-corrupt",Download:true,Bundle:true,Lease:true,Exit:1,Json:false,Error:"Source SHA256 differs"),
        new SnapshotCliCase("absent-bundle-never-defaults-to-other-root",Change:"bundle-absent",Download:true,Lease:true,Exit:1,Json:false,Error:"Source batch prerequisite"),
        new SnapshotCliCase("fetch-never-uses-bundle-fallback",Change:"fetch-bundle",Mode:"Fetch",Download:true,Bundle:true,Exit:1,Json:false,Error:"Snapshot caller admission"),
        new SnapshotCliCase("limit-rejected-before-effects",Change:"limit",Exit:1,Json:false,Error:"UNSUPPORTED_PARAMETER: limit"),
        new SnapshotCliCase("partial-envelope-rejected-before-effects",Change:"partial-envelope",Exit:1,Json:false,Error:"MISSING_PARAMETER: pipeline-request-sha256"),
        new SnapshotCliCase("recipe-lock-rejected-before-effects",Change:"lock",Exit:1,Json:false,Error:"Snapshot caller admission"),
        new SnapshotCliCase("unadmitted-file-url-is-rejected",Change:"file-uri",Exit:1,Json:false,Error:"Snapshot caller admission"),
        new SnapshotCliCase("no-downloaded-rows",Change:"none"),
        new SnapshotCliCase("proof-envelope-not-read-in-default-dry-run",Change:"proof-dry"),
        new SnapshotCliCase("proof-envelope-not-read-for-exact-reuse",Change:"proof-existing",Download:true,Existing:2),
        new SnapshotCliCase("proof-envelope-not-read-for-explicit-generic",Change:"proof-generic",Generic:true),
        new SnapshotCliCase("proof-envelope-pin-required",Change:"proof-envelope-hash",Download:true,Exit:1,Json:false,Error:"ARTIFACT_PIN_HASH"),
        new SnapshotCliCase("proof-root-defaults-to-raw-output",Change:"proof-default-root",Download:true,Exit:1,Json:false,Error:"Exact SDK platform package absent"),
        new SnapshotCliCase("explicit-proof-root-binding",Change:"proof-explicit-root",Download:true,Exit:1,Json:false,Error:"Exact SDK platform package absent"),
        new SnapshotCliCase("receipt-pin-is-not-self-discovered",Change:"proof-receipt-hash",Download:true,Exit:1,Json:false,Error:"ARTIFACT_PIN_HASH")
    }.Select(c=>new object[]{c});
    public static RehydrationPlanRequest Request(SnapshotCliCase c)
    {
        var source=NativeSourceSnapshotProbe.Recipe(new("cli",Change:c.Change is "lock" or "none"?c.Change:null));
        var root=JsonNode.Parse(source.Recipe.Bytes.Span)!.AsObject();
        foreach(var node in root["inventory"]!["sources"]!.AsArray())
        {
            var row=node!.AsObject();var first=row["local_path"]!.GetValue<string>()!="raw/second";
            if(RecipeBundle(c))row["transport"]=new JsonObject{["immutable_url"]=false,["required_local_snapshot"]=first?"bundle/first":"bundle/second"};
            if(Proof(c)){row["platform_api_version"]="37.2";row["transport"]=new JsonObject{["kind"]="bounded-sdk-zip-range-member",["archive_url"]="https://example.invalid/platform.zip",["archive_member"]=first?"data/api-versions.xml":"source.properties",["metadata_receipt"]=ProofReceipt};}
            if(c.Change=="file-uri")row["url"]="file:///unadmitted/source";
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(root);var pin=JsonNode.Parse(source.Lock.Bytes.Span)!.AsObject();
        pin["sha256"]=c.Change=="lock"?new string('0',64):ContentHash.Sha256(bytes);
        return source with{Recipe=source.Recipe with{Bytes=bytes},Lock=source.Lock with{Bytes=JsonSerializer.SerializeToUtf8Bytes(pin)},Profile=c.Generic?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore};
    }
    public static byte[] Envelope(SnapshotCliCase c,string? proofRoot)=>JsonSerializer.SerializeToUtf8Bytes(new{Pins=new[]{
        new StoredArtifact(ProofReceipt,c.Change=="proof-receipt-hash"?new string('0',64):ContentHash.Sha256(Receipt),Receipt.Length,"application/json"),
        new StoredArtifact(ProofXml,ContentHash.Sha256(Xml),Xml.Length,"application/xml")},ProofRoot=proofRoot});
    public static string[] Arguments(SnapshotCliCase c,string runtime,string input,string bundle,string output,string envelopeSha,long envelopeLength)
    {
        var args=new List<string>{"exec","AndroidMcp.Build.dll","--root",runtime,"--target","Sources","--operation","Snapshot","--dotnet-host","/native/dotnet","--project-root",input,"--source-manifest",Path.Combine(input,"rehydration/snapshot.json"),"--recipe-lock",Path.Combine(input,"rehydration/snapshot.lock.json"),"--output-root",output,"--generation","snapshot-cli"};
        if(c.Mode!="Restore")args.AddRange(["--snapshot-mode",c.Mode]);if(c.Download)args.Add("--download");if(c.Bundle)args.AddRange(["--bundle-root",bundle]);if(c.Generic)args.AddRange(["--acquisition-profile","GenericRehydrate"]);if(c.Change=="limit")args.AddRange(["--limit","1"]);
        if(Proof(c)||c.Change=="partial-envelope"){args.AddRange(["--pipeline-request",EnvelopePath]);if(c.Change!="partial-envelope")args.AddRange(["--pipeline-request-sha256",envelopeSha,"--pipeline-request-length",envelopeLength.ToString(System.Globalization.CultureInfo.InvariantCulture)]);}
        return args.ToArray();
    }
    public static Dictionary<string,string> Before(SnapshotCliCase c)
    {
        var result=new Dictionary<string,string>();if(c.Existing>0)result["raw/first"]=ContentHash.Sha256(c.Change is "corrupt" or "fetch-existing"?new byte[]{0}:SourceSnapshotCases.First);if(c.Existing==2)result["raw/second"]=ContentHash.Sha256(SourceSnapshotCases.Second);
        if(c.Change=="proof-default-root"){result[ProofReceipt]=ContentHash.Sha256(Receipt);result[ProofXml]=ContentHash.Sha256(Xml);}return result;
    }
    public static JsonObject Result(SnapshotCliCase c)=>new(){["Release"]="snapshot-fixture",["RecipeSha256"]=ContentHash.Sha256(Request(c).Recipe.Bytes.Span),["Mode"]=c.Mode=="Fetch"?1:0,["DryRun"]=!c.Download,["ExitCode"]=c.Exit,["Counts"]=new JsonObject{["DownloadedRows"]=c.Change=="none"?0:3,["BlobGroups"]=c.Change=="none"?0:2,["Existing"]=c.Existing,["MissingBefore"]=c.Change=="none"?0:2-c.Existing,["Downloaded"]=c.Seal?2:0},["MissingPaths"]=new JsonArray((c.Change=="none"?[]:c.Existing==2?[]:c.Existing==1?new[]{"raw/second"}:new[]{"raw/first","raw/second"}).Select(p=>(JsonNode?)JsonValue.Create(p)).ToArray()),["Acquisition"]=c.Seal?new JsonObject{["Manifest"]=new JsonObject{["RelativePath"]=SealPath}}:null};
    public static byte[] MockSeal(SnapshotCliCase c)
    {
        var sources=new[]{
            new{Source=new{Status="downloaded",Artifact=new{RelativePath="raw/first",Sha256=ContentHash.Sha256(SourceSnapshotCases.First)}}},
            new{Source=new{Status="downloaded",Artifact=new{RelativePath="raw/second",Sha256=ContentHash.Sha256(SourceSnapshotCases.Second)}}}
        };
        return JsonSerializer.SerializeToUtf8Bytes(new{GenerationId="snapshot-cli",SelectedPhysicalSources=2,Complete=true,
            HistoricalByteIdentityClaimed=false,Projection=new{RecipeSha256=ContentHash.Sha256(Request(c).Recipe.Bytes.Span)},Sources=sources});
    }
    public static async Task AssertCase(SnapshotCliCase c,ISnapshotCliProbe probe)
    {
        var observed=await probe.RunAsync(c);Assert.Equal(c.Exit,observed.Exit);Assert.True(observed.InputsRetained);Assert.True(observed.BundleRetained);Assert.True(observed.ProofsRetained);Assert.True(observed.RepeatedOutputsRetained);
        Assert.Equal(Before(c).OrderBy(p=>p.Key),observed.Before.OrderBy(p=>p.Key));foreach(var file in observed.Before)Assert.Equal(file.Value,observed.After[file.Key]);
        var expected=Before(c);if(c.Lease)expected[LeasePath]=ContentHash.Sha256(Array.Empty<byte>());
        if(c.Seal){expected["bundle/first"]=ContentHash.Sha256(SourceSnapshotCases.First);expected["bundle/second"]=ContentHash.Sha256(SourceSnapshotCases.Second);expected["raw/first"]=ContentHash.Sha256(SourceSnapshotCases.First);expected["raw/second"]=ContentHash.Sha256(SourceSnapshotCases.Second);Assert.NotNull(observed.Seal);expected[SealPath]=ContentHash.Sha256(observed.Seal);}else Assert.Null(observed.Seal);
        Assert.Equal(expected.OrderBy(p=>p.Key),observed.After.OrderBy(p=>p.Key));Assert.Equal(expected.Count>0,observed.OutputExists);
        if(c.Seal){using var wire=JsonDocument.Parse(observed.Seal!);var receipt=wire.RootElement;Assert.Equal("snapshot-cli",receipt.GetProperty("GenerationId").GetString());Assert.Equal(2,receipt.GetProperty("SelectedPhysicalSources").GetInt32());Assert.True(receipt.GetProperty("Complete").GetBoolean());Assert.False(receipt.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());Assert.Equal(ContentHash.Sha256(Request(c).Recipe.Bytes.Span),receipt.GetProperty("Projection").GetProperty("RecipeSha256").GetString());var sources=receipt.GetProperty("Sources").EnumerateArray().Select(p=>p.GetProperty("Source")).ToArray();Assert.Equal(new[]{"raw/first","raw/second"},sources.Select(p=>p.GetProperty("Artifact").GetProperty("RelativePath").GetString()));Assert.All(sources,p=>Assert.Equal("downloaded",p.GetProperty("Status").GetString()));Assert.Equal(new[]{ContentHash.Sha256(SourceSnapshotCases.First),ContentHash.Sha256(SourceSnapshotCases.Second)},sources.Select(p=>p.GetProperty("Artifact").GetProperty("Sha256").GetString()));}
        Assert.Equal(c.Json,observed.Json is not null);if(c.Error is not null)Assert.Contains(c.Error,observed.Transcript,StringComparison.Ordinal);
        if(c.Json){var actual=JsonNode.Parse(observed.Json!)!.AsObject();var result=Result(c);foreach(var key in new[]{"Release","RecipeSha256","Mode","DryRun","ExitCode","Counts","MissingPaths"})Assert.True(JsonNode.DeepEquals(result[key],actual[key]),key);Assert.False(actual.ContainsKey("MetadataProofs"));Assert.Equal(c.Seal,actual["Acquisition"] is not null);if(c.Seal)Assert.Equal(SealPath,actual["Acquisition"]!["Manifest"]!["RelativePath"]!.GetValue<string>());}
        Assert.Equal(c.Seal?0:null,observed.RepeatedExit);Assert.Equal(c.Seal?0:null,observed.RepeatedDownloaded);
        var args=observed.Arguments;Assert.Equal("exec",args[0]);Assert.DoesNotContain(args,p=>p is "run" or "build" or "--internal-process-probe");
        string Value(string key){var i=args.ToList().IndexOf(key);Assert.InRange(i,0,args.Count-2);return args[i+1];}
        Assert.Equal("Sources",Value("--target"));Assert.Equal("Snapshot",Value("--operation"));Assert.Equal(observed.RuntimeRoot,Value("--root"));Assert.Equal(observed.InputRoot,Value("--project-root"));Assert.Equal(observed.OutputRoot,Value("--output-root"));Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/snapshot.json"),Value("--source-manifest"));Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/snapshot.lock.json"),Value("--recipe-lock"));Assert.Equal(c.Download,args.Contains("--download"));Assert.Equal(c.Bundle,args.Contains("--bundle-root"));Assert.NotEqual(observed.RuntimeRoot,observed.OutputRoot);Assert.NotEqual(observed.RuntimeRoot,observed.InputRoot);
    }
}
public sealed class ScriptedSnapshotCliProbe(string? mutation=null):ISnapshotCliProbe
{
    public Task<SnapshotCliObservation> RunAsync(SnapshotCliCase c)
    {
        var before=SourcesSnapshotCliCases.Before(c);var after=new Dictionary<string,string>(before);var result=SourcesSnapshotCliCases.Result(c);byte[]? seal=null;
        if(c.Lease)after[SourcesSnapshotCliCases.LeasePath]=ContentHash.Sha256(Array.Empty<byte>());
        if(c.Seal){after["raw/first"]=after["bundle/first"]=ContentHash.Sha256(SourceSnapshotCases.First);after["raw/second"]=after["bundle/second"]=ContentHash.Sha256(SourceSnapshotCases.Second);seal=SourcesSnapshotCliCases.MockSeal(c);after[SourcesSnapshotCliCases.SealPath]=ContentHash.Sha256(seal);}
        var args=SourcesSnapshotCliCases.Arguments(c,"/fixture/runtime","/fixture/input","/fixture/bundle","/fixture/output",new string('a',64),1);
        if(mutation=="args")args[Array.IndexOf(args,"--operation")+1]="Fetch";if(mutation=="output")after["wrong"]="wrong";if(mutation=="before")before["wrong"]="wrong";if(mutation=="count")result["Counts"]!["DownloadedRows"]=99;if(mutation=="recipe")result["RecipeSha256"]="wrong";
        if(mutation?.StartsWith("result:",StringComparison.Ordinal)==true)result.Remove(mutation[7..]);
        if(c.Seal&&mutation?.StartsWith("sealed:",StringComparison.Ordinal)==true){var node=JsonNode.Parse(seal!)!.AsObject();switch(mutation[7..]){case "count":node["SelectedPhysicalSources"]=99;break;case "path":node["Sources"]![0]!["Source"]!["Artifact"]!["RelativePath"]="raw/duplicate";break;case "hash":node["Sources"]![0]!["Source"]!["Artifact"]!["Sha256"]="wrong";break;case "complete":node["Complete"]=false;break;case "identity":node["HistoricalByteIdentityClaimed"]=true;break;}seal=JsonSerializer.SerializeToUtf8Bytes(node);after[SourcesSnapshotCliCases.SealPath]=ContentHash.Sha256(seal);}
        return Task.FromResult(new SnapshotCliObservation(mutation=="exit"?99:c.Exit,mutation=="json"?(c.Json?null:"{}"):c.Json?result.ToJsonString():null,mutation=="error"?"wrong":c.Error??"",before,after,mutation=="seal"?(c.Seal?null:new byte[]{0}):seal,mutation!="input",mutation!="bundle",mutation!="proof",mutation=="exists"?after.Count==0:after.Count>0,args,"/fixture/runtime","/fixture/input","/fixture/bundle","/fixture/output",c.Seal?0:null,c.Seal?0:null,mutation!="repeat"));
    }
}
[Trait("Gate","Mock")]
public sealed class SourcesSnapshotCliMockTests
{
    [Theory,MemberData(nameof(SourcesSnapshotCliCases.Cases),MemberType=typeof(SourcesSnapshotCliCases))]
    public Task CorrectActualCliObservationContract(SnapshotCliCase c)=>SourcesSnapshotCliCases.AssertCase(c,new ScriptedSnapshotCliProbe());
    [Theory,MemberData(nameof(SourcesSnapshotCliCases.Cases),MemberType=typeof(SourcesSnapshotCliCases))]
    public async Task IndependentExitInputsOutputsAndInvocationControls(SnapshotCliCase c)
    {var mutations=new[]{"exit","json","args","output","before","seal","input","bundle","proof","exists","repeat"}.Concat(c.Json?new[]{"count","recipe","result:Release","result:RecipeSha256","result:Mode","result:DryRun","result:ExitCode","result:Counts","result:MissingPaths"}:c.Error is not null?new[]{"error"}:[]).Concat(c.Seal?new[]{"sealed:count","sealed:path","sealed:hash","sealed:complete","sealed:identity"}:[]);foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesSnapshotCliCases.AssertCase(c,new ScriptedSnapshotCliProbe(mutation))));}
}
