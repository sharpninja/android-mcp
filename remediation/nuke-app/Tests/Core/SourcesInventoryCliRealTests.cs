using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesInventoryCliProbe:ISourcesInventoryCliProbe
{
    public async Task<SourcesInventoryCliObservation> RunAsync()
    {
        // Reuse the already proven clean locked-recipe/bundle acquisition fixture and all its assertions.
        var fetched=await new NativeSourcesFetchCliProbe().RunAsync();
        await SourcesFetchCliCases.AssertCase(new RetainedFetchObservation(fetched));
        var root=Path.GetDirectoryName(fetched.RuntimeRoot)??throw new InvalidOperationException("Fixture root missing");
        var runtime=Path.Combine(root,"isolated inventory runtime");var output=Path.Combine(root,"inventory output");var badOutput=Path.Combine(root,"wrong pin inventory output");
        Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var originalInputs=Hashes(fetched.InputRoot);var originalBundle=Hashes(fetched.BundleRoot);var originalAcquisition=Hashes(fetched.OutputRoot);
        var pin=SourcesInventoryCliCases.AcquisitionPin(fetched.ReceiptAfter);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host unavailable");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        List<string> Arguments(string destination,bool wrongPin)
        {
            var arguments=SourcesInventoryCliCases.Arguments(runtime,fetched.InputRoot,fetched.OutputRoot,destination,wrongPin?pin with{Sha256=new string('0',64)}:pin).ToList();
            arguments[1]=typeof(Build).Assembly.Location;arguments[arguments.IndexOf("--dotnet-host")+1]=host;
            arguments.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
            return arguments;
        }
        var args=Arguments(output,false);var badArgs=Arguments(badOutput,true);
        var runner=new NativeProcessRunner(new SystemClock());
        var tool=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        Task<ProcessResult> Invoke(IReadOnlyList<string> arguments,int index)=>runner.RunAsync(new(tool,arguments,runtime,new Dictionary<string,string>(),Path.Combine(root,"process-inventory-"+index)));
        var first=await Invoke(args,1);var stdout=await File.ReadAllTextAsync(first.StandardOutputPath);var stderr=await File.ReadAllTextAsync(first.StandardErrorPath);
        Assert.True(first.ExitCode==0,"Sources/Inventory failed. Fixture retained at "+root+Environment.NewLine+stdout+Environment.NewLine+stderr);
        var summaryLine=Assert.Single(stdout.Split('\n').Select(line=>line.Trim()),line=>line.StartsWith("{\"Manifest\":",StringComparison.Ordinal));
        using var summary=JsonDocument.Parse(summaryLine);
        var destination=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));
        var before=(await destination.ReadAsync(SourcesInventoryCliCases.ManifestPath)).ToArray();
        var repeat=await Invoke(args,2);
        var repeatFailure=await File.ReadAllTextAsync(repeat.StandardOutputPath)+Environment.NewLine+await File.ReadAllTextAsync(repeat.StandardErrorPath);
        var wrong=await Invoke(badArgs,3);
        var wrongFailure=await File.ReadAllTextAsync(wrong.StandardOutputPath)+Environment.NewLine+await File.ReadAllTextAsync(wrong.StandardErrorPath);
        var after=(await destination.ReadAsync(SourcesInventoryCliCases.ManifestPath)).ToArray();
        return new(first.ExitCode,repeat.ExitCode,wrong.ExitCode,before,after,fetched.ReceiptAfter,summary.RootElement.Clone(),
            Paths(output),Paths(badOutput),originalAcquisition,Hashes(fetched.OutputRoot),
            fetched.InputsRetained&&Same(originalInputs,Hashes(fetched.InputRoot)),fetched.BundleRetained&&Same(originalBundle,Hashes(fetched.BundleRoot)),
            repeatFailure.Contains("new or empty output root",StringComparison.Ordinal),wrongFailure.Contains("INVENTORY_ACQUISITION_PIN",StringComparison.Ordinal),
            [args.ToArray(),args.ToArray(),badArgs.ToArray()],runtime,fetched.InputRoot,fetched.OutputRoot,output,badOutput,host);
    }
    private sealed class RetainedFetchObservation(SourcesFetchCliObservation observation):ISourcesFetchCliProbe
    {
        public Task<SourcesFetchCliObservation> RunAsync()=>Task.FromResult(observation);
    }
    private static string[] Paths(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .Select(path=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/')).Order().ToArray():[];
    private static Dictionary<string,string> Hashes(string root)=>Paths(root).ToDictionary(path=>path,
        path=>ContentHash.Sha256(File.ReadAllBytes(Path.Combine(root,path))),StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> first,IReadOnlyDictionary<string,string> second)
        =>first.OrderBy(item=>item.Key).SequenceEqual(second.OrderBy(item=>item.Key));
}
[Trait("Gate","Real")]
public sealed class SourcesInventoryCliRealTests
{
    [Fact]public Task SamePinnedAcquisitionProvenanceAndCliAdmissionAssertions()=>SourcesInventoryCliCases.AssertCase(new NativeSourcesInventoryCliProbe());
}
