namespace AndroidMcp.BuildTool.Core;

// The initializer obtains CompiledWorkspaceRoot from trusted compiled test assembly metadata.
// ChallengePath is the sole fixed origin environment key, never a source of read authorization.
public sealed record TestGateOriginRuntimeRequest(string CompiledWorkspaceRoot,string? ChallengePath);
public sealed record TestGateLoadedAssembly(string AbsolutePath,Guid LoadedModuleId);
public sealed record TestGateLoadedAssemblyPair(TestGateLoadedAssembly Tests,TestGateLoadedAssembly Host);
public interface ITestGateLoadedAssemblyPairSource { TestGateLoadedAssemblyPair ObserveLoadedAssemblies(); }
public sealed record TestGateOriginRuntimeResult(string Code,StoredArtifact? Reply);
public interface ITestGateOriginRuntimeWriter
{
    Task<TestGateOriginRuntimeResult> WriteIfRequestedAsync(TestGateOriginRuntimeRequest request,
        CancellationToken cancellationToken=default);
}
