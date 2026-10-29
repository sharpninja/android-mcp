namespace AndroidMcp.BuildTool.Core;

// Normal verification only. Caller supplies locations and selection, never positive build facts.
// The root-owned native producer uses the existing process/store/hash and admission services.
// Its IArtifactStore is scoped to WorkspaceRoot; current TRX is the relative path of
// ReceiptDirectory/tests.trx within that root, verified in scope, never a latest-file lookup.
public sealed record TestGateProducerRequest(string WorkspaceRoot,string HostProject,string TestsProject,
    ToolIdentity DotNet,string ReceiptDirectory,string? Filter,IReadOnlyList<string> ExpectedClasses);
public enum TestGateSnapshotPoint { BeforeBuild,AfterHostBuild,AfterTestsBuild,AfterExecution }
public sealed record TestGateSourceSnapshot(StoredArtifact Manifest,string ContentFingerprint,
    StoredArtifact NativeEvaluationReceipt);
public sealed record TestGateBinaryIdentity(string AbsolutePath,string Sha256,Guid ModuleId);
public sealed record TestGateTestsOutput(TestGateBinaryIdentity Tests,TestGateBinaryIdentity HostReference,
    StoredArtifact NativeResolutionReceipt);
public interface ITestGateNativeFacts
{
    // The implementation must read native evaluated compile/import/dependency inputs and actual bytes.
    // Timestamp-only discovery or a caller assertion of completeness is not an implementation.
    Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,
        TestGateSnapshotPoint point,CancellationToken cancellationToken=default);
    // From the assembly already executing in this process, never the overwritten on-disk file.
    Guid ReadRunningHostModuleId();
    // On-disk identity is read from bytes/PE metadata, without assembly-loader identity caching.
    Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default);
    Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default);
    // Origin evidence must bind the actual selected/executed test host, not only project configuration.
    Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,
        CancellationToken cancellationToken=default);
}
public sealed record TestGateProducerResult(bool Admitted,string Code,TestBuildEvidence? BuildEvidence,
    TrxAdmissionResult? TrxAdmission,IReadOnlyList<StoredArtifact> Evidence);
public interface ITestGateEvidenceProducer
{
    Task<TestGateProducerResult> ProduceAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default);
}
