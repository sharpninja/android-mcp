using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Catalog.Migration;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogMigrationPlannerRealTests
{
    [Theory,MemberData(nameof(CatalogMigrationPlannerCases.All),MemberType=typeof(CatalogMigrationPlannerCases))]
    public void SameExactPlansBindRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
    [Fact]
    public void SingleMigrateExecutorIsAppendedAndUsesExistingOperation()
    {var definitions=new OperationRegistry().Definitions;var route=Assert.Single(definitions,row=>row.Key==new OperationKey(Category.Catalog,Operation.Migrate));Assert.Equal("CatalogMigration",route.Executor.ToString());}
}
public sealed class NativeMigrationWorkflowProvider:IMigrationWorkflowProvider
{
    private static string Hash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
    private static void NewBytes(string path,ReadOnlySpan<byte> bytes){using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);stream.Flush(true);}
    public async Task<JsonObject> ExecuteAsync(MigrationWorkflowCall call)
    {
        var root=Path.Combine(Path.GetTempPath(),"migration-cli-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var project=Path.Combine(root,"project");Directory.CreateDirectory(project);var runtime=Path.Combine(root,"isolated runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var catalog=call.Variant=="current-success"?Path.Combine(NativeCatalogMigrationProvider.Root(),"remediation","catalog"):Path.Combine(NativeCatalogMigrationProvider.Root(),"publication","android-mcp-git","rehydration","producers","release-011");
        var source=Path.Combine(project,"old.sqlite");var destination=Path.Combine(project,"new.sqlite");var stage=destination+".staging";
        var scripts=NativeCatalogMigrationProvider.OldScripts();
        using(var old=CatalogDatabase.CreatePrivateFile(source,call.Variant=="version-one"?scripts[..1]:scripts))
        {using var command=old.Command("INSERT INTO registries VALUES('r',5,'1.1.0','fixture',37,$hash,'now','published','{}');INSERT INTO active_registry VALUES(1,'r')",null,("$hash",CatalogMigrationCases.Hash));command.ExecuteNonQuery();}
        if(call.Variant=="source-reparse")File.CreateSymbolicLink(Path.Combine(project,"linked.sqlite"),source);
        ReadOnlySpan<byte> retained="independent existing bytes"u8;
        if(call.Variant=="destination-exists")NewBytes(destination,retained);
        if(call.Variant=="stage-exists")NewBytes(stage,retained);
        var sourceHash=Hash(source);var payload=CatalogMigrationWorkflowCases.Payload(call,catalog);var canonical=new CanonicalJson();byte[] request;
        if(call.Variant=="duplicate-member")
        {
            using var memory=new MemoryStream();using(var writer=new Utf8JsonWriter(memory))
            {writer.WriteStartObject();foreach(var field in payload){writer.WritePropertyName(field.Key);field.Value!.WriteTo(writer);}writer.WriteString("CatalogRoot",catalog);writer.WriteEndObject();}
            request=memory.ToArray();
        }
        else request=canonical.Serialize(JsonSerializer.SerializeToElement(payload));
        if(call.Variant=="malformed")request=request[..^1];
        var requestPath=Path.Combine(project,"migration request.json");NewBytes(requestPath,request);
        var pin=new StoredArtifact("migration request.json",ContentHash.Sha256(request),request.LongLength,"application/json");
        if(call.Variant=="bad-sha")pin=pin with{Sha256=new string('0',64)};
        if(call.Variant=="bad-length")pin=pin with{Length=pin.Length+1};
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host required");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;var tool=new ToolIdentity(host,Hash(host),Environment.Version.ToString());var assemblyHash=Hash(dll);
        string[] args=["exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json"),dll,
            "--root",runtime,"--target","Catalog","--operation","Migrate","--dotnet-host",host,"--project-root",project,"--pipeline-request",pin.RelativePath,"--pipeline-request-sha256",pin.Sha256,"--pipeline-request-length",pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        var process=await new NativeProcessRunner(new SystemClock()).RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),Path.Combine(root,"process receipts")));
        var stdout=await File.ReadAllTextAsync(process.StandardOutputPath);var stderr=await File.ReadAllTextAsync(process.StandardErrorPath);var combined=stdout+stderr;
        var error="";if(process.ExitCode!=0)
        {
            error=new[]{"Destination must be new","Only storage user_version=2 can migrate to v3","ARTIFACT_PIN_HASH","ARTIFACT_PIN_LENGTH",CatalogMigrationWorkflowCases.RequestError,"reparse points","Null character"}.FirstOrDefault(value=>combined.Contains(value,StringComparison.Ordinal))??combined;
        }
        var lines=stdout.Split('\n').Select(value=>value.Trim()).Where(value=>value.StartsWith("{\"Operation\":\"Migrate\",",StringComparison.Ordinal)).ToArray();
        var artifactPins=true;var targets=true;long generation=0;var rebuilt=false;
        if(process.ExitCode==0&&lines.Length==1)
        {
            using var document=JsonDocument.Parse(lines[0]);var body=document.RootElement;
            foreach(var (key,path,name) in new[]{("Source",source,"old.sqlite"),("Destination",destination,"new.sqlite"),("RetainedStage",stage,"new.sqlite.staging")})
            {var actual=body.GetProperty(key).Deserialize<StoredArtifact>()!;artifactPins&=actual.RelativePath==name&&actual.Sha256==Hash(path)&&actual.Length==new FileInfo(path).Length&&actual.MediaType=="application/vnd.sqlite3";}
            artifactPins&=Hash(destination)==Hash(stage);
            var migrations=body.GetProperty("TargetMigrations").Deserialize<StoredArtifact[]>()!;
            var names=CatalogMigrationCases.TargetNames.Concat(call.Variant=="current-success"?new[]{"source_metadata_ddl.sql"}:Array.Empty<string>()).ToArray();
            var hashes=CatalogMigrationCases.TargetHashes.Concat(call.Variant=="current-success"?new[]{CatalogMigrationCases.MetadataHash}:Array.Empty<string>()).ToArray();
            targets=migrations.Select(value=>value.RelativePath).SequenceEqual(names.Select(name=>"migrations/"+name))&&migrations.Select(value=>value.Sha256).SequenceEqual(hashes);
            using var migrated=CatalogDatabase.OpenExisting(destination);using var query=migrated.Command("SELECT registry_generation FROM catalog_view");generation=(long)query.ExecuteScalar()!;
            using var flag=migrated.Command("SELECT value FROM catalog_meta WHERE key='rebuilt_with_corrected_extractor'");rebuilt=(string)flag.ExecuteScalar()! =="false";
        }
        string Value(string option)=>args[Array.IndexOf(args,option)+1];
        var requestFailure=call.Variant is not ("frozen-success" or "current-success" or "destination-exists" or "stage-exists" or "version-one");
        var existing=call.Variant switch{"destination-exists"=>File.ReadAllBytes(destination).AsSpan().SequenceEqual("independent existing bytes"u8),"stage-exists"=>File.ReadAllBytes(stage).AsSpan().SequenceEqual("independent existing bytes"u8),_=>true};
        return new JsonObject
        {
            ["exit"]=process.ExitCode,["error"]=error,["request_retained"]=request.AsSpan().SequenceEqual(File.ReadAllBytes(requestPath)),["source_bytes_retained"]=Hash(source)==sourceHash,["host_dll_pinned"]=Hash(host)==tool.Sha256&&Hash(dll)==assemblyHash,["runtime_root_separate"]=runtime!=project,
            ["arguments_exact"]=Value("--target")=="Catalog"&&Value("--operation")=="Migrate"&&Value("--pipeline-request")==pin.RelativePath&&Value("--pipeline-request-sha256")==pin.Sha256&&Value("--pipeline-request-length")==pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)&&Value("--project-root")==project&&Value("--root")==runtime,
            ["no_build_restore_or_probe"]=args[0]=="exec"&&!args.Any(value=>value is "run" or "build" or "restore" or "--internal-process-probe"),["existing_files_retained"]=existing,["final_absent"]=!File.Exists(destination),
            ["receipt_count"]=lines.Length,["observed_artifact_pins_exact"]=artifactPins,["target_script_pins_exact"]=targets,["registry_generation"]=generation,["rebuilt_false"]=rebuilt,["request_failure_no_stage"]=!requestFailure||!File.Exists(stage)
        };
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationWorkflowRealTests
{
    [Theory,MemberData(nameof(CatalogMigrationWorkflowCases.All),MemberType=typeof(CatalogMigrationWorkflowCases))]
    public Task SamePinnedRequestCasesReachActualCompiledCatalogMigrateCaller(MigrationWorkflowCase test)=>CatalogMigrationWorkflowCases.AssertCase(test,new NativeMigrationWorkflowProvider());
}
