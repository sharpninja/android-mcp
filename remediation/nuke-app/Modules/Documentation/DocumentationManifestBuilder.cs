using System.Text;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>The single checksum-row authority, extracted from the existing deterministic package builder.</summary>
public sealed class DocumentationManifestBuilder(IArtifactReceiptVerifier receipts):IDocumentationManifestBuilder
{
    private const string ChecksumPath="docs/checksums.sha256";

    public DocumentationManifest Build(IReadOnlyList<Artifact> artifacts)
    {
        var covered=Admit(artifacts);
        return new(new(ChecksumPath,Rows(covered),"text/plain"),Array.AsReadOnly(covered));
    }

    public void Verify(IReadOnlyList<Artifact> artifacts,DocumentationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var covered=Admit(artifacts);
        // The public record is untrusted. Identity equality includes the entire
        // ordered set, paths, hash, size and media; no partial manifest is admitted.
        if(manifest.Checksums.RelativePath!=ChecksumPath||manifest.Checksums.MediaType!="text/plain"||
            !covered.SequenceEqual(manifest.CoveredInputs)||!manifest.Checksums.Bytes.Span.SequenceEqual(Rows(covered)))
            throw new ArchitectureAssetException("MANIFEST_MISMATCH","Prebuilt checksums do not cover exactly the admitted current artifacts");
    }

    private StoredArtifact[] Admit(IReadOnlyList<Artifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var artifact in artifacts)
        {
            if(!ArtifactPaths.IsRelative(artifact.RelativePath))throw new ArchitectureAssetException("MANIFEST_INPUT_PATH","Checksum input path must be a confined relative artifact path");
            if(string.Equals(artifact.RelativePath,ChecksumPath,StringComparison.OrdinalIgnoreCase))throw new ArchitectureAssetException("MANIFEST_RESERVED","Checksums cannot include their own output");
            if(!paths.Add(artifact.RelativePath))throw new ArchitectureAssetException("MANIFEST_INPUT_DUPLICATE","Checksum inputs must have unique portable paths");
        }
        return artifacts.OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).Select(receipts.Describe).ToArray();
    }

    private static byte[] Rows(IReadOnlyList<StoredArtifact> ordered)=>Encoding.UTF8.GetBytes(
        string.Concat(ordered.Select(artifact=>artifact.Sha256+"  "+artifact.RelativePath+"\n")));
}
