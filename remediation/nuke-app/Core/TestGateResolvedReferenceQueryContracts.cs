namespace AndroidMcp.BuildTool.Core;

public sealed record TestGateResolvedReferenceQueryRequest(TestGateProducerRequest Run,TestGateSdkPin Sdk,
    string HostOutputPath,string Configuration,string TargetFramework);
public sealed record TestGateResolvedReferenceQueryResult(string Code,IReadOnlyList<TestGateResolvedCopy> Copies,
    IReadOnlyList<StoredArtifact> PrivateReceipts);
public interface ITestGateResolvedReferenceQuery
{
    // Fixed native ReferenceCopyLocalPathsOutputGroup purpose only. The SDK facade first
    // verifies its target definition belongs to the pinned supported SDK, not a custom override.
    // Uses IProcessRunner/capture/store, full private raw receipts, no shell or public raw metadata.
    Task<TestGateResolvedReferenceQueryResult> QueryAsync(TestGateResolvedReferenceQueryRequest request,
        CancellationToken cancellationToken=default);
}
