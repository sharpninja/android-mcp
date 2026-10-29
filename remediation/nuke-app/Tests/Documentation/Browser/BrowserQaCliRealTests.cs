using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed class NativeBrowserQaCliProbe:IBrowserQaCliProbe
{
    public async Task<BrowserQaCliObservation> RunAsync(BrowserQaCliInvocation call,CancellationToken cancellationToken=default)
    {
        var fixtureRoot=Path.GetDirectoryName(call.Request.OutputRoot)!;
        var before=new[]{call.Request.ProjectRoot,call.ArchitectureRoot}.SelectMany(root=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
            .ToDictionary(path=>path,path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
        var hostHash=ContentHash.Sha256(File.ReadAllBytes(call.Host));var dllHash=ContentHash.Sha256(File.ReadAllBytes(call.Dll));
        var argv=BrowserQaCliCases.Arguments(call).ToList();argv.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var request=new ProcessRequest(new(call.Host,hostHash,Environment.Version.ToString()),argv,call.RuntimeRoot,
            new Dictionary<string,string>(),Path.Combine(fixtureRoot,"cli-process"));
        var process=await new NativeProcessRunner(new SystemClock()).RunAsync(request,cancellationToken);
        var evidence=new FileArtifactStore(fixtureRoot,bytes=>ContentHash.Sha256(bytes.Span));
        await evidence.WriteAsync(new("cli-invocation.json",JsonSerializer.SerializeToUtf8Bytes(new{request,process,dll_sha256=dllHash}),"application/json"),cancellationToken);
        var output=new FileArtifactStore(call.Request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));var receiptPath=Path.Combine(call.Request.OutputRoot,"browser-qa.json");
        if(!File.Exists(receiptPath))throw new InvalidOperationException("Registered browser CLI did not retain a QA receipt. Fixture: "+fixtureRoot+Environment.NewLine+
            await File.ReadAllTextAsync(process.StandardOutputPath,cancellationToken)+Environment.NewLine+await File.ReadAllTextAsync(process.StandardErrorPath,cancellationToken));
        var bytes=await output.ReadAsync("browser-qa.json",cancellationToken);var report=JsonNode.Parse(bytes.Span)!.AsObject();
        var captures=report["captures"]!.Deserialize<StoredArtifact[]>()!;var failures=report["failures"]!.Deserialize<string[]>()!;
        var result=new BrowserQaResult(new("browser-qa.json",ContentHash.Sha256(bytes.Span),bytes.Length,"application/json"),report["pass"]!.GetValue<bool>(),report["shots"]!.AsArray().Count,
            report["embedded"]!.AsArray().Count,report["svg_checks"]!.AsArray().Count,captures,failures);
        var generations=report["generation_inputs"]!.Deserialize<BrowserQaVerifiedGeneration[]>()!;
        var ui=generations.Single(generation=>generation.Role=="ui");var architecture=generations.Single(generation=>generation.Role=="architecture");
        var after=new[]{call.Request.ProjectRoot,call.ArchitectureRoot}.SelectMany(root=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)).Order(StringComparer.Ordinal).ToArray();
        var unchanged=before.Keys.Order(StringComparer.Ordinal).SequenceEqual(after)&&before.All(pair=>ContentHash.Sha256(File.ReadAllBytes(pair.Key))==pair.Value);
        return new(result,process.ExitCode,File.Exists(process.StandardOutputPath),File.Exists(process.StandardErrorPath),ContentHash.Sha256(File.ReadAllBytes(call.Dll))==dllHash,
            ContentHash.Sha256(File.ReadAllBytes(call.Host))==hostHash,report["producer"]!.GetValue<string>()=="androidmcp-native-browser-qa-v1",unchanged,
            argv[0]=="exec"&&!argv.Contains("build",StringComparer.Ordinal)&&!argv.Contains("restore",StringComparer.Ordinal)&&!argv.Contains("run",StringComparer.Ordinal),argv,
            ui.Manifest.Sha256,architecture.Manifest.Sha256,ui.GenerationId,architecture.GenerationId);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaCliRealTests(NativeBrowserFixture fixture):IClassFixture<NativeBrowserFixture>
{
    public static IEnumerable<object[]> Cases=>BrowserQaCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task CurrentMonolithCliUsesSameSevenQaCasesAndAssertions(string id)
    {
        var root=ProcessSessionFixturePaths.NewRoot("browser-cli-tests");var test=BrowserQaCases.Get(id);var call=BrowserQaCliCases.Input(root,test,fixture.Browser);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host unavailable.");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var runtime=Path.Combine(root,"isolated nuke runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        call=call with{Host=host,Dll=typeof(Build).Assembly.Location,RuntimeRoot=runtime};
        await BrowserQaCliCases.AssertContract(test,new NativeBrowserQaCliProbe(),call,new FileArtifactStore(call.Request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span)));
    }
}
