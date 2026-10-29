using System.Text.Json;

namespace AndroidMcp.BuildTool.Documentation.Validation;

// Root parsing and operation dispatch stay in the existing NUKE registry.
// This is the original main's print/exit boundary over its bound checker.
public sealed class RepositoryDocumentationCaller(IRepositoryDocumentationCheck checker)
{
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public async Task<int> RunAsync(TextWriter output,CancellationToken cancellationToken=default)
    {
        var result=await checker.CheckAsync(cancellationToken);
        await output.WriteLineAsync(JsonSerializer.Serialize(result,Options).AsMemory(),cancellationToken);
        return result.ExitCode;
    }
}
