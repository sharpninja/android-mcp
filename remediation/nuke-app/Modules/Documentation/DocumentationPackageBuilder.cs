using System.IO.Compression;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure deterministic source package; policy, metadata and checksums have shared authorities.</summary>
public sealed class DocumentationPackageBuilder(IPublicationChecker checker,IArtifactReceiptVerifier receipts,
    IDocumentationManifestBuilder? manifests=null):IDocumentationManifestPackageBuilder
{
    private const string ChecksumPath="docs/checksums.sha256",ArchivePath="package/android-mcp-design-source.zip";
    private readonly IDocumentationManifestBuilder manifestAuthority=manifests??new DocumentationManifestBuilder(receipts);

    public DocumentationPackage Build(DocumentationPackageInput input)
    {
        AdmitPackage(input);
        return Archive(input.Artifacts,manifestAuthority.Build(input.Artifacts).Checksums);
    }

    public DocumentationPackage Build(DocumentationPackageInput input,DocumentationManifest manifest)
    {
        // An independently supplied record cannot bypass full coverage admission.
        // This verifies with the existing authority and never builds a second manifest.
        manifestAuthority.Verify(input.Artifacts,manifest);
        AdmitPackage(input);
        return Archive(input.Artifacts,manifest.Checksums);
    }

    private void AdmitPackage(DocumentationPackageInput input)
    {
        if(input.RequiredPaths.Except(input.Artifacts.Select(a=>a.RelativePath),StringComparer.Ordinal).Any())
            throw new ArchitectureAssetException("PACKAGE_MISSING","Required source or vector artifact is absent");
        if(input.Artifacts.Any(a=>string.Equals(a.RelativePath,ChecksumPath,StringComparison.OrdinalIgnoreCase)||string.Equals(a.RelativePath,ArchivePath,StringComparison.OrdinalIgnoreCase)))
            throw new ArchitectureAssetException("PACKAGE_RESERVED","Package inputs cannot supply their checksum or archive output");
        var check=checker.Check(input.Policy,input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)).ToArray());
        if(!check.IsValid)throw new ArchitectureAssetException("PACKAGE_POLICY","Source package policy rejected input: "+string.Join(", ",check.Issues.Select(i=>i.Code+":"+i.Path)));
    }

    private static DocumentationPackage Archive(IReadOnlyList<Artifact> artifacts,Artifact checksums)
    {
        using var buffer=new MemoryStream();
        using(var archive=new ZipArchive(buffer,ZipArchiveMode.Create,true))
        {
            foreach(var artifact in artifacts.Append(checksums).OrderBy(a=>a.RelativePath,StringComparer.Ordinal))
            {
                var entry=archive.CreateEntry(artifact.RelativePath,CompressionLevel.NoCompression);
                entry.LastWriteTime=new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero);entry.ExternalAttributes=0;
                using var stream=entry.Open();stream.Write(artifact.Bytes.Span);
            }
        }
        return new(new(ArchivePath,buffer.ToArray(),"application/zip"),checksums);
    }
}
