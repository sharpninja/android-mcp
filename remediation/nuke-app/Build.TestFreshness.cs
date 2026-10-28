using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

// Root review handoff only. Existing models and services; this does not add a
// second test runner or another public operation/parameter/source model.
public sealed partial class Build
{
    internal static async Task<ITestGateEvidenceProducer> ComposeNativeTestEvidenceProducer(TestGateProducerRequest run,
        TestGateNativeFactsConfiguration configuration)
    {
        var store=new FileArtifactStore(run.WorkspaceRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var runner=new NativeProcessRunner(new SystemClock());
        var captures=new ProcessCaptureReader(run.WorkspaceRoot);
        // The current monolith output contract is bin/<configuration>, without
        // a framework suffix. Never search for another SDK or binary.
        var output=Path.Combine(Path.GetDirectoryName(run.HostProject)!,"bin",configuration.Configuration);
        var imagePath=Path.Combine(output,"AndroidMcp.Build.dll");
        string Relative(string path)
        {
            var relative=Path.GetRelativePath(run.WorkspaceRoot,path).Replace('\\','/');
            ArtifactPaths.Resolve(run.WorkspaceRoot,relative);return relative;
        }
        var imageBytes=await store.ReadAsync(Relative(imagePath));
        var image=TestGateManagedPeIdentity.Observe(imagePath,imageBytes);
        if(image.ModuleId!=typeof(Build).Assembly.ManifestModule.ModuleVersionId)
            throw new TestGateFactsException("SDK_CHILD_CODE_MVID_INVALID","The fixed monolith output differs from the executing host module.");
        var runtimeFiles=new List<StoredArtifact>();
        foreach(var name in new[]{"AndroidMcp.Build.runtimeconfig.json","AndroidMcp.Build.deps.json"})
        {
            var path=Relative(Path.Combine(output,name));var bytes=await store.ReadAsync(path);
            runtimeFiles.Add(new(path,ContentHash.Sha256(bytes.Span),bytes.Length,"application/json"));
        }
        var evaluator=new NativeTestGateSdkChildEvaluator(configuration,image,imageBytes.Length,runtimeFiles,
            new PinnedArtifactReader(store),store,runner,captures,new CryptographicTestGateNonceSource());
        var facts=new SdkEvaluatedTestGateNativeFacts(configuration,evaluator,
            new ScopedTestGateNativeFileReader(configuration.ApprovedReadRoots),
            new RuntimeHostTestGateModuleSource(typeof(Build).Assembly),store,
            new CryptographicTestGateNonceSource(),new SharedStoreTestGateOriginArtifactWriter(store));
        return new NativeTestGateEvidenceProducer(runner,captures,facts,new TestBuildPlanAdmission(),
            new TrxResultAdmission(),store);
    }
}
