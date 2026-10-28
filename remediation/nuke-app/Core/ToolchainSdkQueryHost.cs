using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Thin normal NUKE host: shared pinned input, strict JSON, scoped native query.</summary>
public sealed class ToolchainSdkQueryHost(IPinnedArtifactReader input,ITestGateResolvedReferenceQuery query):IToolchainSdkQueryHost
{
    private readonly CanonicalJson canonical=new();
    private static readonly JsonSerializerOptions JsonOptions=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public async Task<ToolchainSdkQueryPublicResult> ExecuteAsync(ToolchainSdkQueryCall call,CancellationToken cancellationToken=default)
    {
        var receipts=new List<StoredArtifact>();
        ToolchainSdkQueryPublicResult Result(string code,int copies=0)=>new(call.Check.ToString(),code,copies,receipts.ToArray());
        if(call.Check!=ToolchainCheck.SdkReferenceCopy)return Result("UNSUPPORTED_TOOLCHAIN_CHECK");
        ReadOnlyMemory<byte> bytes;
        try{bytes=await input.ReadAsync(call.PipelineRequest,cancellationToken);}
        catch(PinnedArtifactException error){return Result(error.Code);}
        receipts.Add(call.PipelineRequest);
        TestGateResolvedReferenceQueryRequest? request;
        try{request=JsonSerializer.Deserialize<TestGateResolvedReferenceQueryRequest>(canonical.Serialize(bytes),JsonOptions);}
        catch(Exception error)when(error is JsonException or FormatException){return Result("PIPELINE_REQUEST_INVALID");}
        if(request?.Run?.DotNet is null||request.Sdk is null)return Result("PIPELINE_REQUEST_INVALID");
        try
        {
            if(Normalize(request.Run.WorkspaceRoot)!=Normalize(call.ProjectRoot))return Result("PIPELINE_WORKSPACE_MISMATCH");
            if(Normalize(request.Run.DotNet.ExecutablePath)!=Normalize(call.DotNetHost))return Result("PIPELINE_DOTNET_MISMATCH");
            var run=Relative(call.ProjectRoot,request.Run.ReceiptDirectory);
            var components=ArtifactPaths.Segments(run);
            if(components.Length!=3||components[0]!="artifacts"||components[1]!="tests")return Result("PIPELINE_SCOPE_INVALID");
            Relative(call.ProjectRoot,request.Run.HostProject);Relative(call.ProjectRoot,request.Run.TestsProject);
            Relative(call.ProjectRoot,request.HostOutputPath);
        }
        catch(ArgumentException){return Result("PIPELINE_SCOPE_INVALID");}
        var resolved=await query.QueryAsync(request,cancellationToken);
        receipts.AddRange(resolved.PrivateReceipts);
        // Public normal-check output has no SDK source/destination metadata or raw response body.
        return Result(resolved.Code,resolved.Copies.Count);
    }
    private static string Normalize(string path)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static string Relative(string root,string path)
    {
        var relative=Path.GetRelativePath(Normalize(root),Path.GetFullPath(path)).Replace('\\','/');
        ArtifactPaths.Resolve(root,relative);return relative;
    }
}
