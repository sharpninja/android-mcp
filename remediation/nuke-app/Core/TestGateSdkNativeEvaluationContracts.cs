namespace AndroidMcp.BuildTool.Core;

// This is the native SDK API boundary, not another evaluator DTO authority. The
// existing ITestGateSdkEvaluator remains the sole collector-facing interface.
// The actual binding owns a private collectible AssemblyLoadContext and a new
// ProjectCollection for this request. It verifies every installed SDK assembly
// pin before loading and disposes/unloads on every terminal path. It has no
// process-environment, installed-SDK or process-wide resolver mutation API.
public sealed record TestGateSdkNativeOpenRequest(TestGateSdkEvaluationRequest Evaluation,
    IReadOnlyList<string> ApprovedReadRoots);
public sealed record TestGateSdkNativeItem(string ItemType,string EvaluatedInclude,
    string FullPath,string DefiningProjectFullPath);
public sealed record TestGateSdkNativeImport(string ImportedProjectFullPath,string ImportingProjectFullPath);
public sealed record TestGateSdkNativeProject(string ProjectFullPath,string ToolsPath,
    string Configuration,string TargetFramework,string TargetPath,
    IReadOnlyList<TestGateSdkNativeItem> Items,IReadOnlyList<TestGateSdkNativeImport> Imports,
    IReadOnlyDictionary<string,string> ParticipatingFileProperties,IReadOnlyList<string> EvaluationIssues);
public interface ITestGateSdkNativeSession : IAsyncDisposable
{
    // Project.LoadProject and actual evaluated items/Project.Imports only. No
    // build targets execute here. Resolved copies use the proven query seam.
    Task<TestGateSdkNativeProject> LoadProjectAsync(string absoluteProjectPath,
        CancellationToken cancellationToken=default);
}
public interface ITestGateSdkNativeSessionFactory
{
    Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,
        CancellationToken cancellationToken=default);
}
