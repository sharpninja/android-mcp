using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesExtractCliProbe:ISourcesExtractCliProbe
{
    public async Task<SourcesExtractCliObservation> RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-sources-extract",Guid.NewGuid().ToString("N"),"SDK fixture with spaces");
        var project=Path.Combine(root,"project inputs");var bundle=Path.Combine(root,"snapshot bundle");
        var rawRoot=Path.Combine(root,"acquisition raw");var inventoryRoot=Path.Combine(root,"verified inventory");
        var generationRoot=Path.Combine(root,"native generations");var runtime=Path.Combine(root,"isolated runtime");
        Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        FileArtifactStore Store(string path)=>new(path,bytes=>ContentHash.Sha256(bytes.Span));
        var inputs=Store(project);var bundles=Store(bundle);var raw=Store(rawRoot);var inventories=Store(inventoryRoot);var generations=Store(generationRoot);
        var canonical=new CanonicalJson();var recipe=SourcesExtractCliCases.Recipe();
        var protectedInputs=new Dictionary<string,string>(StringComparer.Ordinal);
        async Task<StoredArtifact> Input(Artifact artifact)
        {var pin=await inputs.WriteAsync(artifact);protectedInputs.Add(pin.RelativePath,pin.Sha256);return pin;}
        await Input(recipe.Recipe);await Input(recipe.Lock);
        await bundles.WriteAsync(new("snapshots/api.xml",SourcesExtractCliCases.SourceBytes,"application/octet-stream"));
        var originalBundle=Hashes(bundle);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host unavailable");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var runner=new NativeProcessRunner(new SystemClock());
        var tool=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        var arguments=new List<IReadOnlyList<string>>();var exits=new List<int>();
        async Task<(ProcessResult Process,string Stdout,string Stderr)> Invoke(IReadOnlyList<string> planned,bool requireSuccess=true)
        {
            var args=planned.ToList();args[1]=typeof(Build).Assembly.Location;
            args[args.IndexOf("--dotnet-host")+1]=host;
            args.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
            arguments.Add(args.ToArray());
            // Current compiled monolith only. No child build, restore, script or source execution.
            var process=await runner.RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),Path.Combine(root,"process-"+arguments.Count)));
            exits.Add(process.ExitCode);var stdout=await File.ReadAllTextAsync(process.StandardOutputPath);var stderr=await File.ReadAllTextAsync(process.StandardErrorPath);
            if(requireSuccess)Assert.True(process.ExitCode==0,"Sources CLI failed; fixture retained at "+root+Environment.NewLine+stdout+Environment.NewLine+stderr);
            return(process,stdout,stderr);
        }
        static JsonElement Summary(string stdout,string member)
        {
            var line=Assert.Single(stdout.Split('\n').Select(value=>value.Trim()),value=>value.StartsWith("{\""+member+"\":",StringComparison.Ordinal));
            using var parsed=JsonDocument.Parse(line);return parsed.RootElement.Clone();
        }
        await Invoke(SourcesPlanCliCases.Arguments(runtime,project,Path.Combine(root,"pure recipe plan")));
        var fetched=await Invoke(SourcesFetchCliCases.Arguments(runtime,project,bundle,rawRoot));
        var acquired=Summary(fetched.Stdout,"Manifest").GetProperty("Manifest").Deserialize<StoredArtifact>()!;
        var inventoried=await Invoke(SourcesInventoryCliCases.Arguments(runtime,project,rawRoot,inventoryRoot,acquired));
        var inventoryPin=Summary(inventoried.Stdout,"Manifest").GetProperty("Manifest").Deserialize<StoredArtifact>()!;
        var rawBefore=Hashes(rawRoot);var inventoryBefore=Hashes(inventoryRoot);
        var firstWorkflow=SourcesExtractCliCases.Workflow(inventoryRoot,rawRoot,generationRoot,inventoryPin);
        var firstWorkflowPin=await Input(new("workflows/extract-first.json",canonical.Serialize(JsonSerializer.SerializeToElement(firstWorkflow)),"application/json"));
        var firstProcess=await Invoke(SourcesExtractCliCases.Arguments(runtime,project,firstWorkflowPin));
        var firstSummary=Summary(firstProcess.Stdout,"Index");var firstIndex=firstSummary.GetProperty("Index").Deserialize<StoredArtifact>()!;
        var pinnedGenerations=new PinnedArtifactReader(generations);
        var firstCommitBefore=(await pinnedGenerations.ReadAsync(firstIndex)).ToArray();
        var priorRoot=ArtifactPaths.Resolve(generationRoot,SourcesExtractCliCases.FirstGeneration);
        var priorBefore=Hashes(priorRoot);
        var secondWorkflow=firstWorkflow with{GenerationRoot=SourcesExtractCliCases.SecondGeneration,TrustedPriorIndex=firstIndex};
        var secondWorkflowPin=await Input(new("workflows/extract-second.json",canonical.Serialize(JsonSerializer.SerializeToElement(secondWorkflow)),"application/json"));
        var secondProcess=await Invoke(SourcesExtractCliCases.Arguments(runtime,project,secondWorkflowPin));
        var secondSummary=Summary(secondProcess.Stdout,"Index");var secondIndex=secondSummary.GetProperty("Index").Deserialize<StoredArtifact>()!;
        const string refusedGeneration="sdk-generation-bad-pin";
        var badWorkflow=firstWorkflow with{GenerationRoot=refusedGeneration};
        var badPin=await Input(new("workflows/extract-wrong-pin.json",canonical.Serialize(JsonSerializer.SerializeToElement(badWorkflow)),"application/json"));
        var refused=await Invoke(SourcesExtractCliCases.Arguments(runtime,project,badPin with{Sha256=new('0',64)}),false);
        var firstCommitAfter=(await pinnedGenerations.ReadAsync(firstIndex)).ToArray();
        // These are successful child-produced pins, rather than hashes minted from an editable index.
        var inventoryBytes=await new PinnedArtifactReader(inventories).ReadAsync(inventoryPin);
        var inventory=JsonSerializer.Deserialize<SourcesInventoryReceipt>(canonical.Serialize(inventoryBytes))!;
        var projection=inventory.Acquisition.Receipt.Projection;
        var observed=new SourceProducerIdentityBuilder().Create();
        var leases=new FileArtifactLeaseProvider(Path.Combine(runtime,".nuke","source-readback-leases"));
        var service=new SourcesExtractService(raw,generations,new SourcesRuntimeFactory(),new SourceCache(generations,leases,canonical),canonical,leases);
        async Task<(SourceExtractRow Row,SourceExtractIndex Index)> ReadBack(StoredArtifact pin)
        {
            var manifest=JsonSerializer.Deserialize<SourceExtractGenerationManifest>(canonical.Serialize(await pinnedGenerations.ReadAsync(pin)))!;
            var data=JsonSerializer.Deserialize<SourceExtractIndex>(canonical.Serialize(await pinnedGenerations.ReadAsync(manifest.IndexData)))!;
            using var generation=await service.OpenAsync(new(projection,SourceExtractionProfile.CurrentCandidate,observed.Identity,pin));
            return(await generation.ReadAsync(Assert.Single(projection.Rows)),data);
        }
        // Reuse the Source-owned generation reader and sole sealed cache codec for declaration readback.
        var firstRead=await ReadBack(firstIndex);var secondRead=await ReadBack(secondIndex);
        var producerBytes=canonical.Serialize(JsonSerializer.SerializeToElement(observed.Identity));
        bool SameProducer(SourceExtractIndex index)=>producerBytes.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(index.Producer)));
        var inputsRetained=true;
        foreach(var pin in protectedInputs)inputsRetained&=ContentHash.Sha256((await inputs.ReadAsync(pin.Key)).Span)==pin.Value;
        return new(exits.ToArray(),firstSummary,secondSummary,firstRead.Row.RecordsJson.ToArray(),secondRead.Row.RecordsJson.ToArray(),
            firstRead.Row.CacheSeal!.Data,secondRead.Row.CacheSeal!.Data,firstRead.Row.CacheSeal.Sidecar,secondRead.Row.CacheSeal.Sidecar,
            firstCommitBefore,firstCommitAfter,inputsRetained,Same(originalBundle,Hashes(bundle)),Same(rawBefore,Hashes(rawRoot)),Same(inventoryBefore,Hashes(inventoryRoot)),
            Same(priorBefore,Hashes(priorRoot)),SameProducer(firstRead.Index)&&SameProducer(secondRead.Index),
            (refused.Stdout+refused.Stderr).Contains("ARTIFACT_PIN_HASH",StringComparison.Ordinal),!Directory.Exists(ArtifactPaths.Resolve(generationRoot,refusedGeneration)),
            firstWorkflow,secondWorkflow,firstWorkflowPin,secondWorkflowPin,arguments.ToArray(),runtime,project);
    }
    private static Dictionary<string,string> Hashes(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/'),path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal):new(StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> before,IReadOnlyDictionary<string,string> after)
        =>before.OrderBy(pair=>pair.Key).SequenceEqual(after.OrderBy(pair=>pair.Key));
}
[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesExtractCliRealTests
{
    [Fact]public Task SameSdkNativeGenerationAndTrustedCacheHitCliAssertions()=>SourcesExtractCliCases.AssertCase(new NativeSourcesExtractCliProbe());
}
