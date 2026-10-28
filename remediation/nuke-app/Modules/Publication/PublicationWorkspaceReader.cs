using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Publication.PublicationGitCheckErrors;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>Read-only policy/store and lstat-style metadata admission, never file generation.</summary>
public sealed class PublicationWorkspaceReader:IPublicationWorkspaceReader
{
    public async Task<ReadOnlyMemory<byte>> ReadPolicyAsync(string checkoutRoot,string relativePath,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if(!Path.IsPathFullyQualified(checkoutRoot))throw new ArgumentException("Absolute checkout required.");
            IArtifactStore store=new FileArtifactStore(checkoutRoot,bytes=>ContentHash.Sha256(bytes.Span));
            return await store.ReadAsync(relativePath,cancellationToken);
        }
        catch(Exception error) when(IsObservationFailure(error))
        {throw new PublicationGitCheckException("PUBLICATION_POLICY","Policy input is missing, unreadable or outside the admitted checkout.");}
    }
    public Task<IReadOnlyList<PublicationFile>> ObserveAsync(string checkoutRoot,IReadOnlyList<string> trackedPaths,CancellationToken cancellationToken=default)
    {
        var files=new List<PublicationFile>();
        foreach(var relative in trackedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string full;
            try
            {
                if(!Path.IsPathFullyQualified(checkoutRoot) || !PublicationNames.Relative(relative))throw new ArgumentException("Portable path required.");
                full=ArtifactPaths.Resolve(checkoutRoot,relative);
            }
            catch(ArgumentException){throw new PublicationGitCheckException("PUBLICATION_PATH","Tracked path crosses an unsafe component or link boundary.");}
            catch(Exception error) when(IsObservationFailure(error))
            {throw new PublicationGitCheckException("PUBLICATION_SNAPSHOT","Tracked path could not be admitted for metadata observation.");}
            try
            {
                var attributes=File.GetAttributes(full);
                if((attributes&FileAttributes.ReparsePoint)!=0)files.Add(new(relative,0,PublicationFileKind.SymbolicLink));
                else if((attributes&FileAttributes.Directory)!=0)files.Add(new(relative,0,PublicationFileKind.Directory));
                else files.Add(new(relative,new FileInfo(full).Length,PublicationFileKind.Regular));
            }
            catch(FileNotFoundException){files.Add(new(relative,0,PublicationFileKind.Missing));}
            catch(DirectoryNotFoundException){files.Add(new(relative,0,PublicationFileKind.Missing));}
            catch(Exception error) when(IsObservationFailure(error))
            {throw new PublicationGitCheckException("PUBLICATION_SNAPSHOT","Tracked file metadata is unavailable.");}
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<PublicationFile>>(Array.AsReadOnly(files.ToArray()));
    }
}
