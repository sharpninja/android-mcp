using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogValidationCliProbe:ICatalogValidationCliProbe
{
    private sealed record Captured(CatalogValidationCliObservation Observation,CatalogValidationResult? Validation,string Output);
    private static string Hash(string path){using var file=File.OpenRead(path);return ContentHash.Sha256(file);}
    public async Task<CatalogValidationCliObservation> RunAsync(CatalogValidationCliCase test)
    {
        if(test.Original is not null)
        {
            Captured? captured=null;
            var original=new NativeCatalogFullValidationProvider
            {
                Execution=async invocation=>
                {
                    var root=Path.GetDirectoryName(invocation.OutputDirectory)!;
                    var file=Bindings(test,root,invocation.OutputDirectory,invocation.InventoryPath,invocation.DatabasePath,invocation.ReportPath);
                    captured=await Invoke(test,root,file);
                    if(captured.Observation.Exit!=0)throw new IOException(captured.Output);
                    return captured.Validation??throw new InvalidOperationException("Successful CLI omitted validation receipt");
                }
            };
            var reply=await original.ExecuteAsync(test.Original.Call);Assert.NotNull(captured);
            return captured!.Observation with{Validation=reply};
        }
        using var fixture=new NativeCatalogBuildProvider();
        CatalogBuildCases.AssertCase(new("validation-cli-intake",new(),CatalogBuildCases.Base),fixture);
        var fixtureRoot=fixture.EvidenceRoot;var output=Path.Combine(fixtureRoot,"out");var report=Path.Combine(fixtureRoot,"validation report.json");
        if(test.ExistingReportRetained)await File.WriteAllTextAsync(report,"retained preexisting validation report");
        var bindings=Bindings(test,fixtureRoot,output,Path.Combine(fixtureRoot,"inventory.json"),Path.Combine(output,"catalog.sqlite"),report);
        return (await Invoke(test,fixtureRoot,bindings)).Observation;
    }
    private static CatalogValidationWorkflowFile Bindings(CatalogValidationCliCase test,string root,string output,string inventory,string database,string report)
    {
        if(test.Mode!=CatalogValidationMode.Database)return new(CatalogValidationMode.Release,report,output,inventory);
        // Standalone database directory deliberately has neither export nor inventory.
        var isolated=Path.Combine(root,"database only");Directory.CreateDirectory(isolated);var copied=Path.Combine(isolated,"standalone.sqlite");
        File.Copy(database,copied,false);Assert.Equal(Hash(database),Hash(copied));
        return new(CatalogValidationMode.Database,report,DatabasePath:copied);
    }
    private static async Task<Captured> Invoke(CatalogValidationCliCase test,string root,CatalogValidationWorkflowFile file)
    {
        var project=Path.Combine(root,"validation caller project");var contracts=Path.Combine(project,"contracts");Directory.CreateDirectory(contracts);
        foreach(var source in Directory.EnumerateFiles(Path.Combine(NativeSchemaProvider.Root(),"contracts"),"*.schema.json"))
        {
            var destination=Path.Combine(contracts,Path.GetFileName(source));File.Copy(source,destination,false);Assert.Equal(Hash(source),Hash(destination));
        }
        var input=CatalogValidationCliCases.Input(test,file);var store=new FileArtifactStore(project,bytes=>ContentHash.Sha256(bytes.Span));
        await store.WriteAsync(new(input.Pin.RelativePath,input.Bytes,"application/json"));
        var reportPath=file.ReportPath!;var priorReport=File.Exists(reportPath)?Hash(reportPath):null;
        var runtime=Path.Combine(root,"validation isolated runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var receipts=Path.Combine(root,"validation process receipts");
        var protectedFiles=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(path=>path,Hash,StringComparer.Ordinal);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host required");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;var dllHash=Hash(dll);var tool=new ToolIdentity(host,Hash(host),Environment.Version.ToString());
        var arguments=CatalogValidationCliCases.Arguments(runtime,project,input.Pin).ToList();arguments[1]=dll;arguments.AddRange(["--dotnet-host",host]);
        arguments.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var result=await new NativeProcessRunner(new SystemClock()).RunAsync(new(tool,arguments,runtime,new Dictionary<string,string>(),receipts));
        var stdout=await File.ReadAllTextAsync(result.StandardOutputPath);var stderr=await File.ReadAllTextAsync(result.StandardErrorPath);var combined=stdout+Environment.NewLine+stderr;
        var unchanged=protectedFiles.All(pair=>File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value);
        var requestPath=ArtifactPaths.Resolve(project,input.Pin.RelativePath);var requestRetained=(await File.ReadAllBytesAsync(requestPath)).AsSpan().SequenceEqual(input.Bytes.Span);
        var newFiles=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(path=>!protectedFiles.ContainsKey(path));
        var noEffects=newFiles.All(path=>path==reportPath || path.StartsWith(runtime+Path.DirectorySeparatorChar,StringComparison.Ordinal) || path.StartsWith(receipts+Path.DirectorySeparatorChar,StringComparison.Ordinal));
        var created=priorReport is null && File.Exists(reportPath);var oldRetained=priorReport is not null && File.Exists(reportPath) && Hash(reportPath)==priorReport;
        CatalogValidationResult? validation=null;ReadOnlyMemory<byte> bytes=ReadOnlyMemory<byte>.Empty;StoredArtifact? reportPin=null;CatalogValidationMode? reportMode=null;
        if(result.ExitCode==0)
        {
            var line=Assert.Single(stdout.Split('\n').Select(value=>value.Trim()),value=>value.StartsWith("{\"Mode\":",StringComparison.Ordinal));
            using var summary=JsonDocument.Parse(line);reportMode=Enum.Parse<CatalogValidationMode>(summary.RootElement.GetProperty("Mode").GetString()!);
            reportPin=summary.RootElement.GetProperty("Artifact").Deserialize<StoredArtifact>();Assert.NotNull(reportPin);
            bytes=await File.ReadAllBytesAsync(reportPath);var report=JsonNode.Parse(bytes.Span)!.AsObject();
            Assert.True(JsonNode.DeepEquals(report,JsonNode.Parse(summary.RootElement.GetProperty("Report").GetRawText())));
            validation=new(report,reportPin!,summary.RootElement.GetProperty("DatabaseReadOnly").GetBoolean());
        }
        else
        {
            Assert.True(test.Error is not null,"Unexpected validation CLI failure; retained fixture "+root+Environment.NewLine+combined);
            Assert.Contains(test.Error!,combined,StringComparison.OrdinalIgnoreCase);
        }
        var databaseOnly=test.Mode==CatalogValidationMode.Database && file.DatabasePath is not null &&
            !File.Exists(Path.Combine(Path.GetDirectoryName(file.DatabasePath)!,"entries.jsonl")) && !File.Exists(Path.Combine(Path.GetDirectoryName(file.DatabasePath)!,"inventory.json"));
        return new(new(result.ExitCode,result.ExitCode==0?null:test.Error,null,arguments,input.Pin,project,runtime,
            unchanged,requestRetained,noEffects,Hash(host)==tool.Sha256&&Hash(dll)==dllHash&&arguments[5]==dll,databaseOnly,created,oldRetained,
            reportMode,bytes,reportPin,dllHash),validation,combined);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogValidationCliRealTests
{
    [Theory,MemberData(nameof(CatalogValidationCliCases.All),MemberType=typeof(CatalogValidationCliCases))]
    public Task SameSeventeenCasesBindTheRegisteredMonolithicValidationCaller(CatalogValidationCliCase test)=>CatalogValidationCliCases.AssertCase(test,new NativeCatalogValidationCliProbe());
}
