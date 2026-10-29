using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesSeedCliProbe:ISourcesSeedCliProbe
{
    public async Task<SourcesSeedCliObservation> RunAsync()
    {
        // The sole existing fixture prepares and verifies the pinned SDK chain.
        // Its raw store is now an explicit immutable bundle for a separate seed target.
        var fixture=await SourcesExtractCliFixture.CreateAsync();
        var project=fixture.ProjectRoot;var runtime=fixture.RuntimeRoot;
        var bundle=fixture.Workflow.Stores.AcquisitionRawRoot;
        var output=Path.Combine(Path.GetDirectoryName(bundle)??throw new InvalidOperationException("Fixture root missing"),"seeded acquisition");
        var originalInputs=Hashes(project);var originalBundle=Hashes(bundle);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host unavailable");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var runner=new NativeProcessRunner(new SystemClock());
        var tool=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        var arguments=new List<IReadOnlyList<string>>();
        async Task<(ProcessResult Process,string Stdout,string Stderr)> Invoke(IReadOnlyList<string> planned,bool success=true)
        {
            var args=planned.ToList();args[1]=typeof(Build).Assembly.Location;args[args.IndexOf("--dotnet-host")+1]=host;
            args.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
            arguments.Add(args.ToArray());
            var captures=Path.Combine(Path.GetDirectoryName(output)!,"seed-cli-process-"+arguments.Count);
            // Execute the compiled monolith under the same test-owned NUKE root, never rebuild it.
            var process=await runner.RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),captures));
            var stdout=await File.ReadAllTextAsync(process.StandardOutputPath);var stderr=await File.ReadAllTextAsync(process.StandardErrorPath);
            if(success)Assert.True(process.ExitCode==0,"Seed continuation failed; fixture retained at "+Path.GetDirectoryName(output)+Environment.NewLine+stdout+Environment.NewLine+stderr);
            return(process,stdout,stderr);
        }
        static JsonElement Summary(string stdout)
        {
            var line=Assert.Single(stdout.Split('\n').Select(value=>value.Trim()),value=>value.StartsWith("{\"Manifest\":",StringComparison.Ordinal));
            using var document=JsonDocument.Parse(line);return document.RootElement.Clone();
        }
        var first=await Invoke(SourcesSeedCliCases.Arguments(runtime,project,bundle,output));
        var firstSummary=Summary(first.Stdout);var seedPin=firstSummary.GetProperty("Manifest").Deserialize<StoredArtifact>()!;
        var store=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));var pinned=new PinnedArtifactReader(store);
        var before=(await pinned.ReadAsync(seedPin)).ToArray();
        var repeated=await Invoke(SourcesSeedCliCases.Arguments(runtime,project,bundle,output));
        var repeatedSummary=Summary(repeated.Stdout);
        var rejected=await Invoke(SourcesSeedCliCases.Arguments(runtime,project,bundle,output,true),false);
        var paths=DataPaths(output);var acquisitionAbsent=!File.Exists(ArtifactPaths.Resolve(output,"sources/acquisition.json"));
        var rawBefore=ContentHash.Sha256((await store.ReadAsync("raw/api.xml")).Span);
        // Existing Fetch rechecks the seeded raw target and creates its own acquisition manifest.
        var fetched=await Invoke(SourcesFetchCliCases.Arguments(runtime,project,bundle,output));
        var acquisitionPin=Summary(fetched.Stdout).GetProperty("Manifest").Deserialize<StoredArtifact>()!;
        var acquired=(await pinned.ReadAsync(acquisitionPin)).ToArray();
        var after=(await pinned.ReadAsync(seedPin)).ToArray();
        var rawAfter=ContentHash.Sha256((await store.ReadAsync("raw/api.xml")).Span);
        return new(first.Process.ExitCode,repeated.Process.ExitCode,rejected.Process.ExitCode,fetched.Process.ExitCode,
            firstSummary,repeatedSummary,before,after,acquired,paths,acquisitionAbsent,Same(originalInputs,Hashes(project)),Same(originalBundle,Hashes(bundle)),
            (rejected.Stdout+rejected.Stderr).Contains("SEED_PLAN_INVALID",StringComparison.Ordinal),rawBefore,rawAfter,arguments.ToArray(),runtime,project,bundle,output);
    }
    private static string[] DataPaths(string root)=>Hashes(root).Keys.Where(path=>!path.StartsWith(".leases/",StringComparison.Ordinal)).Order().ToArray();
    private static Dictionary<string,string> Hashes(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/'),path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal):new(StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> before,IReadOnlyDictionary<string,string> after)
        =>before.OrderBy(pair=>pair.Key).SequenceEqual(after.OrderBy(pair=>pair.Key));
}
[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesSeedCliRealTests
{
    [Fact]public Task SameFixtureSeedReplayAndFetchSealAssertions()=>SourcesSeedCliCases.AssertCase(new NativeSourcesSeedCliProbe());
}
