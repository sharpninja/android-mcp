namespace AndroidMcp.BuildTool.Core;

public sealed record TestGateSdkPin(string SdkDirectory,IReadOnlyList<StoredArtifact> AssemblyFiles);
public sealed record TestGateNativeFactsConfiguration(TestGateSdkPin Sdk,IReadOnlyList<string> ApprovedReadRoots,
    string Configuration,string TargetFramework);
public enum TestGateSdkEvaluationPhase { SourceInputs,ResolvedBuildOutputs }
public sealed record TestGateSdkEvaluationRequest(TestGateProducerRequest Run,TestGateSdkPin Sdk,
    string Configuration,string TargetFramework,TestGateSdkEvaluationPhase Phase=TestGateSdkEvaluationPhase.SourceInputs);
public sealed record TestGateEvaluatedInput(string Kind,string AbsolutePath,string DefiningProject);
public sealed record TestGateResolvedCopy(string SourcePath,string DestinationPath,string ReferencedProject,
    StoredArtifact NativeProof);
public sealed record TestGateResolvedCopyProof(string RunDirectory,string ProjectPath,string SourcePath,
    string DestinationPath,string ReferencedProject,ToolIdentity DotNet,string Configuration,string TargetFramework);
public sealed record TestGateEvaluatedProject(string ProjectPath,string TargetPath,
    IReadOnlyList<TestGateEvaluatedInput> Inputs,IReadOnlyList<string> Imports,
    IReadOnlyList<string> ProjectReferences,IReadOnlyList<string> Issues,
    IReadOnlyList<TestGateResolvedCopy>? ResolvedCopies=null);
public sealed record TestGateSdkEvaluation(IReadOnlyList<TestGateEvaluatedProject> Projects,
    IReadOnlyList<StoredArtifact> SdkFiles);
public interface ITestGateSdkEvaluator
{
    // Actual pinned native SDK evaluation, including resolved Project.Imports and evaluated items.
    // A directory/extension scan, incomplete MSBuildAllProjects string or public receipt is not evaluation.
    // ResolvedBuildOutputs must project actual post-build/ResolveReferences item metadata with
    // immutable same-run proof. Never derive a copied destination from a project/output basename.
    Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default);
}
public sealed record TestGateNativeFileRequest(string AbsolutePath,string Role,IReadOnlyList<string> ApprovedReadRoots);
public interface ITestGateNativeFileReader
{
    // Exact scope/reparse validation, actual bytes, no mtime-based cache or unrestricted root fallback.
    Task<ReadOnlyMemory<byte>> ReadAsync(TestGateNativeFileRequest request,CancellationToken cancellationToken=default);
}
public interface ITestGateLoadedModuleSource { Guid ReadHostModuleId(); }
public interface ITestGateNonceSource { string CreateNonce(); }
public interface ITestGateOriginArtifactWriter
{
    // Strict CreateNew, including rejection of an identical existing artifact. This is a
    // narrow capability of the shared artifact authority, not another general-purpose store.
    Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default);
}
public sealed record TestGateOriginChallenge(string RunPath,string Nonce,string ReceiptPath,
    TestGateBinaryIdentity Tests,TestGateBinaryIdentity Host);
public sealed record TestGateOriginResponse(string RunPath,string Nonce,string ChallengeSha256,
    string TestsPath,Guid LoadedTestsModuleId,string TestsSha256,
    string HostPath,Guid LoadedHostModuleId,string HostSha256);
public interface ITestGateTestHostOriginProvider
{
    // Produces a CreateNew challenge after actual output observations, before starting tests.
    // Exactly one public env key points to this scoped immutable challenge; no secret/env dump.
    Task<IReadOnlyDictionary<string,string>> PrepareTestHostEnvironmentAsync(TestGateProducerRequest request,
        CancellationToken cancellationToken=default);
}
public sealed record TestGateHashedInput(string Kind,string AbsolutePath,string DefiningProject,string Sha256,long Length);
public sealed record TestGateSourceManifest(string HostProject,string TestsProject,string Configuration,
    string TargetFramework,IReadOnlyList<TestGateHashedInput> Inputs);
public sealed class TestGateFactsException(string code,string message):InvalidOperationException(message)
{ public string Code { get; }=code; }
