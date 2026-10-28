using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesPlanCliProbe:ISourcesPlanCliProbe
{
    public async Task<SourcesPlanCliObservation> RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-sources-plan",Guid.NewGuid().ToString("N"),"fixture with spaces");
        var input=Path.Combine(root,"locked inputs");var output=Path.Combine(root,"pure plan output");var runtime=Path.Combine(root,"isolated nuke runtime");
        Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var store=new FileArtifactStore(input,bytes=>ContentHash.Sha256(bytes.Span));
        var fixture=SourcesPlanCliCases.Request();await store.WriteAsync(fixture.Recipe);await store.WriteAsync(fixture.Lock);
        var originals=InputHashes(input);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host is unavailable.");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var args=SourcesPlanCliCases.Arguments(runtime,input,output).ToList();
        args[1]=typeof(Build).Assembly.Location;args[args.IndexOf("--dotnet-host")+1]=host;
        args.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var tool=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        var runner=new NativeProcessRunner(new SystemClock());
        Task<ProcessResult> Invoke(int index)=>runner.RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),Path.Combine(root,"process-"+index)));
        var first=await Invoke(1);var stdout=await File.ReadAllTextAsync(first.StandardOutputPath);var stderr=await File.ReadAllTextAsync(first.StandardErrorPath);
        Assert.True(first.ExitCode==0,"Sources/Plan failed. Fixture retained at "+root+Environment.NewLine+stdout+Environment.NewLine+stderr);
        var summaryLine=Assert.Single(stdout.Split('\n').Select(x=>x.Trim()),x=>x.StartsWith("{\"Manifest\":",StringComparison.Ordinal));
        using var summary=JsonDocument.Parse(summaryLine);
        var destination=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));
        var before=(await destination.ReadAsync("rehydration-plan.json")).ToArray();
        var rejected=await Invoke(2);
        var rejection=(await File.ReadAllTextAsync(rejected.StandardOutputPath))+Environment.NewLine+await File.ReadAllTextAsync(rejected.StandardErrorPath);
        var after=(await destination.ReadAsync("rehydration-plan.json")).ToArray();
        return new(first.ExitCode,rejected.ExitCode,before,after,summary.RootElement.Clone(),
            Directory.EnumerateFiles(output,"*",SearchOption.AllDirectories).Select(path=>Path.GetRelativePath(output,path).Replace(Path.DirectorySeparatorChar,'/')).Order().ToArray(),
            originals.OrderBy(x=>x.Key).SequenceEqual(InputHashes(input).OrderBy(x=>x.Key)),rejection.Contains("new or empty output root",StringComparison.Ordinal),
            [args.ToArray(),args.ToArray()],runtime,input,output);
    }
    private static Dictionary<string,string> InputHashes(string root)=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path),path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesPlanCliRealTests
{
    [Fact]public Task SamePurePlanPrerequisitesAndFreshOutputAssertions()=>SourcesPlanCliCases.AssertCase(new NativeSourcesPlanCliProbe());
}
