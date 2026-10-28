using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesFetchCliProbe:ISourcesFetchCliProbe
{
    public async Task<SourcesFetchCliObservation> RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-sources-fetch",Guid.NewGuid().ToString("N"),"fixture with spaces");
        var input=Path.Combine(root,"locked inputs");var bundle=Path.Combine(root,"owner bundle");
        var output=Path.Combine(root,"raw outputs");var runtime=Path.Combine(root,"isolated nuke runtime");
        Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var inputStore=new FileArtifactStore(input,bytes=>ContentHash.Sha256(bytes.Span));
        var bundleStore=new FileArtifactStore(bundle,bytes=>ContentHash.Sha256(bytes.Span));
        var fixture=SourcesFetchCliCases.Request();
        await inputStore.WriteAsync(fixture.Recipe);await inputStore.WriteAsync(fixture.Lock);
        await bundleStore.WriteAsync(new("snapshots/a.txt",Encoding.ASCII.GetBytes("abc"),"application/octet-stream"));
        await bundleStore.WriteAsync(new("snapshots/api.txt",Encoding.ASCII.GetBytes("def"),"application/octet-stream"));
        var originalInputs=Hashes(input);var originalBundle=Hashes(bundle);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host is unavailable.");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var args=SourcesFetchCliCases.Arguments(runtime,input,bundle,output).ToList();
        args[1]=typeof(Build).Assembly.Location;
        args[args.IndexOf("--dotnet-host")+1]=host;
        args.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
            "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var runner=new NativeProcessRunner(new SystemClock());
        var tool=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        async Task<ProcessResult> Invoke(int index)
        {
            // Execute the current compiled monolith; no build, run, restore or legacy launcher.
            var result=await runner.RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),Path.Combine(root,"process-"+index)));
            Assert.True(result.ExitCode==0,"Sources/Fetch CLI failed. Fixture retained at "+root+Environment.NewLine+
                await File.ReadAllTextAsync(result.StandardOutputPath)+Environment.NewLine+await File.ReadAllTextAsync(result.StandardErrorPath));
            return result;
        }
        var first=await Invoke(1);
        var outputStore=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));
        var before=(await outputStore.ReadAsync("sources/acquisition.json")).ToArray();var outputsBefore=Hashes(output);
        var repeated=await Invoke(2);
        var after=(await outputStore.ReadAsync("sources/acquisition.json")).ToArray();var outputsAfter=Hashes(output);
        return new(first.ExitCode,repeated.ExitCode,before,after,outputsBefore,outputsAfter,
            Same(originalInputs,Hashes(input)),Same(originalBundle,Hashes(bundle)),[args.ToArray(),args.ToArray()],runtime,input,bundle,output);
    }
    private static Dictionary<string,string> Hashes(string root)=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/'),path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> first,IReadOnlyDictionary<string,string> second)
        =>first.OrderBy(x=>x.Key).SequenceEqual(second.OrderBy(x=>x.Key));
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchCliRealTests
{
    [Fact]
    public Task SameBundledSnapshotAndSealedResumeAssertions()=>SourcesFetchCliCases.AssertCase(new NativeSourcesFetchCliProbe());
}
