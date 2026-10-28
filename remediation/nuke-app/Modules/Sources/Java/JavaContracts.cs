using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources.Java;

public interface IJavaFileContext
{
    void Attach(JsonArray sources, JsonArray reports);
}

public interface IJvmSourceLinker
{
    string? Encode(string? value, string owner, IReadOnlyDictionary<string, string> owners);
    string? Key(JsonObject declaration, IReadOnlyDictionary<string, string> owners);
}

public sealed record JavaNativeBuildRequest(string SourceRoot, IReadOnlyDictionary<string,string> SourceHashes,
    ToolIdentity Compiler, string WorkingDirectory, string ReceiptDirectory, string ArtifactRelativePath);
public sealed record JavaNativeBuildResult(StoredArtifact Artifact, ProcessRequest Compilation, ProcessResult Process,
    string RuntimeCommit, string GrammarCommit, int LanguageAbi);
public interface IJavaNativeBuilder
{
    Task<JavaNativeBuildResult> BuildAsync(JavaNativeBuildRequest request, CancellationToken cancellationToken = default);
}
public sealed record JavaNativeWorkflowRequest(string OutputRoot,ToolIdentity Compiler,string? CacheRoot=null,string? ReceiptRoot=null,string? ProjectRoot=null);
public sealed record JavaNativeWorkflowResult(StoredArtifact Manifest,JavaNativeBuildResult Build,JsonObject SourceLock);
public interface IJavaNativeWorkflow
{
    Task<JavaNativeWorkflowResult> RunAsync(JavaNativeWorkflowRequest request,CancellationToken cancellationToken=default);
}
