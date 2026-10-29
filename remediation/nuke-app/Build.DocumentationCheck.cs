using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ValidateRepositoryDocumentation()
    {
        var selectedRoot=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var hashes=new NativeContentHash();
        var store=new FileArtifactStore(selectedRoot,bytes=>hashes.Sha256(bytes.Span));
        var html=new SourceHtmlDecoder();
        var validator=new RepositoryDocumentationValidator(store,new ArtifactReferenceResolver(html.Decode),
            new ArtifactReceiptVerifier(hashes),html);
        var check=new RepositoryDocumentationCheck(new NativeArtifactWorkspace(selectedRoot),validator);
        var result=await check.CheckAsync();
        Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        if(result.ExitCode!=0)throw new InvalidOperationException("Repository documentation validation reported errors.");
    }
}
