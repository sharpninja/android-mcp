using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record DocumentationCapturedSource(DocumentationModelSourceObservation Observation,StoredArtifact? Identity);
public sealed record DocumentationModelSourceSnapshot(IReadOnlyList<DocumentationCapturedSource> Sources,Artifact Receipt);
public interface IDocumentationSourceCollection
{
    Task<DocumentationModelSourceSnapshot> CaptureAsync(CancellationToken cancellationToken=default);
    Task VerifyUnchangedAsync(DocumentationModelSourceSnapshot snapshot,CancellationToken cancellationToken=default);
}
public sealed class DocumentationSourceCollectionException(string code,IReadOnlyList<string> artifactIds,Exception? inner=null)
    :IOException(code+": "+string.Join(", ",artifactIds),inner)
{
    public string Code {get;}=code;
    public IReadOnlyList<string> ArtifactIds {get;}=Array.AsReadOnly(artifactIds.ToArray());
}
