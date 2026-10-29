namespace AndroidMcp.BuildTool.Core;

// Transport envelopes only. Actual SDK source/graph data remains exclusively the
// existing TestGateSdkNativeOpenRequest and TestGateSdkEvaluation models.
// RuntimeFiles is an explicit ordered pair: runtimeconfig, dependency manifest.
public sealed record TestGateSdkChildRequest(TestGateSdkNativeOpenRequest Native,
    TestGateBinaryIdentity Monolith,IReadOnlyList<StoredArtifact> RuntimeFiles,string EvaluationRelativePath);
public sealed record TestGateSdkChildReceipt(string RequestSha256,TestGateBinaryIdentity LoadedMonolith,
    StoredArtifact Evaluation);
