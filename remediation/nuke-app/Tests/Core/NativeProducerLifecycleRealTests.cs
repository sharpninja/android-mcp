using System.Runtime.InteropServices;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Review.Staging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidMcp.BuildTool.Tests.Core;

// One current-workspace integration fixture. It calls the SAME Root composition,
// not another runner. Its nested class selection cannot select this probe.
[Collection("SDK native binding isolation")]
public sealed class NativeProducerLifecycleRealTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Existing_class_has_current_source_build_and_executing_module_origin()
    {
        var root=new RuntimeTestGateOriginBootstrapSource(typeof(NativeProducerLifecycleRealTests).Assembly).ReadCompiledWorkspaceRoot();
        var workspace=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        var relative="artifacts/tests/native-producer-proof-"+DateTime.Now.ToString("yyyyMMddTHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N");
        var stage="PrepareInputs";
        try
        {
            var runtime=Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
            var dotnetRoot=Path.GetFullPath(Path.Combine(runtime,"..","..",".."));
            var sdkRoot=Path.Combine(dotnetRoot,"sdk",SdkBindingFixture.SelectedSdkVersion);
            var sdkStore=new FileArtifactStore(sdkRoot,bytes=>ContentHash.Sha256(bytes.Span));
            var version=System.Text.Encoding.UTF8.GetString((await sdkStore.ReadAsync(".version")).Span)
                .Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            Assert.True(version.Length>1,"The selected SDK version receipt is incomplete.");
            Assert.Equal(SdkBindingFixture.SelectedSdkVersion,version[1]);
            var sdkPins=new List<StoredArtifact>();
            foreach(var path in SdkBindingFixture.RequiredPins)
            {
                var bytes=await sdkStore.ReadAsync(path);
                sdkPins.Add(new(path,ContentHash.Sha256(bytes.Span),bytes.Length,
                    path.EndsWith(".targets",StringComparison.Ordinal) ? "application/xml" : "application/octet-stream"));
            }
            var hostName=OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            var dotnet=await new FileArtifactStore(dotnetRoot,bytes=>ContentHash.Sha256(bytes.Span)).ReadAsync(hostName);
            var roots=new HashSet<string>(StringComparer.Ordinal){Path.GetFullPath(root),Path.GetFullPath(sdkRoot)};
            // The selected installed SDK imports public workload metadata from
            // this observed band (including Android 36.1.2) outside its sdk/
            // directory. Admit only that metadata band, not the dotnet/home root.
            // The native source collector still pins each actual imported file.
            var workloadMetadata=Path.Combine(dotnetRoot,"sdk-manifests","10.0.100");
            if(Directory.Exists(workloadMetadata))
            {
                _=ArtifactPaths.Resolve(workloadMetadata,".source-scope-admission");
                roots.Add(Path.GetFullPath(workloadMetadata));
            }
            var restorePins=new List<StoredArtifact>();
            foreach(var path in new[]{"obj/project.assets.json","Tests/obj/project.assets.json"})
            {
                var bytes=await workspace.ReadAsync(path);restorePins.Add(new(path,ContentHash.Sha256(bytes.Span),bytes.Length,"application/json"));
                using var document=JsonDocument.Parse(new CanonicalJson().Serialize(bytes));
                var libraries=document.RootElement.GetProperty("libraries");
                foreach(var folder in document.RootElement.GetProperty("packageFolders").EnumerateObject())
                {
                    Assert.True(Path.IsPathFullyQualified(folder.Name),"Restore package folder must be absolute.");
                    foreach(var library in libraries.EnumerateObject())
                    {
                        if(!library.Value.TryGetProperty("type",out var kind)||kind.GetString()!="package")continue;
                        var packagePath=library.Value.GetProperty("path").GetString();
                        Assert.False(string.IsNullOrWhiteSpace(packagePath),"Restore package version path is missing.");
                        Assert.True(ArtifactPaths.IsRelative(packagePath!.Replace('\\','/')),"Restore package version path must remain scoped.");
                        // Explicit restored package/version paths only. No cache/home scan.
                        roots.Add(ArtifactPaths.Resolve(folder.Name,packagePath));
                    }
                }
            }
            const string selected="AndroidMcp.BuildTool.Tests.Core.PinnedArtifactReaderRealTests";
            var run=new TestGateProducerRequest(root,Path.Combine(root,"AndroidMcp.Build.csproj"),
                Path.Combine(root,"Tests","AndroidMcp.Build.Tests.csproj"),
                new(Path.Combine(dotnetRoot,hostName),ContentHash.Sha256(dotnet.Span),version[1]),
                ArtifactPaths.Resolve(root,relative),"FullyQualifiedName~"+selected,[selected]);
            var configuration=new TestGateNativeFactsConfiguration(new(sdkRoot,sdkPins),
                roots.Order(StringComparer.Ordinal).ToArray(),"Debug","net10.0");
            var runPin=await SaveAsync(workspace,relative+"/probe.run.json",run);
            var configurationPin=await SaveAsync(workspace,relative+"/probe.configuration.json",configuration);
            var restorePin=await SaveAsync(workspace,relative+"/probe.restore-inputs.json",restorePins);
            output.WriteLine(JsonSerializer.Serialize(new{Stage="PinnedInputs",Run=runPin,Configuration=configurationPin,RestoreInputs=restorePin}));
            stage="NativeTestGateEvidenceProducer.ProduceAsync";
            var producer=await Build.ComposeNativeTestEvidenceProducer(run,configuration);
            var result=await producer.ProduceAsync(run);
            var admission=await SaveAsync(workspace,relative+"/admission.json",result);
            output.WriteLine(JsonSerializer.Serialize(new{Stage="ProducerTerminal",result.Admitted,result.Code,Admission=admission}));
            Assert.True(result.Admitted,"Native producer rejected this exact run: "+result.Code);
            stage="CurrentTrxAndExecutingOrigin";
            var trx=await workspace.ReadAsync(relative+"/tests.trx");
            var origin=await workspace.ReadAsync(relative+"/origin-response.json");
            Assert.NotEmpty(trx.ToArray());Assert.NotEmpty(origin.ToArray());
            output.WriteLine(JsonSerializer.Serialize(new{Stage="ExactRuntimeEvidence",
                Trx=new StoredArtifact(relative+"/tests.trx",ContentHash.Sha256(trx.Span),trx.Length,"application/xml"),
                Origin=new StoredArtifact(relative+"/origin-response.json",ContentHash.Sha256(origin.Span),origin.Length,"application/json")}));
        }
        catch(Exception error)
        {
            var failure=await SaveAsync(workspace,relative+"/failure.json",new{Stage=stage,
                Code=error is TestGateFactsException fact ? fact.Code : error.GetType().Name,
                Error=error.GetType().FullName,error.Message,error.StackTrace,RetryAttempted=false});
            output.WriteLine(JsonSerializer.Serialize(new{Stage="FailedClosed",Failure=failure}));
            throw;
        }
        // All original input, source, process, TRX and origin artifacts remain retained.
    }
    private static Task<StoredArtifact> SaveAsync<T>(ICreateNewArtifactStore store,string path,T value)
        =>store.WriteNewAsync(new(path,new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value)),"application/json"));
}
