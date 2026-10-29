using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationRealTests
{
    [Theory,MemberData(nameof(RepositoryDocumentationCases.Cases),MemberType=typeof(RepositoryDocumentationCases))]
    public async Task SameOriginalRepositoryCasesBindNativeCheckerAndExistingFileStore(string id)
    {
        var test=new RepositoryDocumentationCase(id);var e=new RepositoryDocumentationEnvironment(test);
        var root=Path.Combine(Path.GetTempPath(),"repository-documentation-native",Guid.NewGuid().ToString("N"));
        var native=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        foreach(var directory in test.Entries.Where(entry=>entry.Kind==PublicationFileKind.Directory))Directory.CreateDirectory(ArtifactPaths.Resolve(root,directory.Path));
        foreach(var file in test.Files)await native.WriteAsync(new(file.Key,file.Value,"application/octet-stream"));
        var provider=new RepositoryDocumentationValidator(e.Store(native),e.Resolver,new ArtifactReceiptVerifier(new NativeContentHash()),new SourceHtmlDecoder());
        await RepositoryDocumentationCases.AssertContract(e,provider);
        foreach(var file in test.Files)Assert.Equal(file.Value,(await native.ReadAsync(file.Key)).ToArray());
    }
}
