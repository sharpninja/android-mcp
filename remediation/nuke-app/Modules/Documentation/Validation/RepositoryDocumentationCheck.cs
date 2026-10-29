using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Documentation.Validation;

// Full root scope is obtained from the one shared native tree capability.
// The existing validator/store own contents and original checker predicates.
public sealed class RepositoryDocumentationCheck(IArtifactTreeReader tree,IRepositoryDocumentationValidator validator):IRepositoryDocumentationCheck
{
    public async Task<RepositoryDocumentationCheckResult> CheckAsync(CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries=await tree.EnumerateAsync(null,true,cancellationToken);
        if(entries.Any(entry=>entry.Kind is not (NativeArtifactKind.RegularFile or NativeArtifactKind.Directory)))
            throw new ArchitectureAssetException("DOCS_REPOSITORY_REFERENCE_SCOPE","Repository tree contains an unadmitted nonregular entry");
        var files=entries.Select(entry=>new PublicationFile(entry.RelativePath,entry.Length,
            entry.Kind==NativeArtifactKind.RegularFile?PublicationFileKind.Regular:PublicationFileKind.Directory)).ToArray();
        return await validator.CheckAsync(files,cancellationToken);
    }
}
