using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure deterministic source package; policy and metadata semantics are shared.</summary>
public sealed class DocumentationPackageBuilder(IPublicationChecker checker,IArtifactReceiptVerifier receipts):IDocumentationPackageBuilder
{
    public DocumentationPackage Build(DocumentationPackageInput input)
    {
        const string checksumPath="docs/checksums.sha256";const string archivePath="package/android-mcp-design-source.zip";
        if(input.RequiredPaths.Except(input.Artifacts.Select(a=>a.RelativePath),StringComparer.Ordinal).Any())
            throw new ArchitectureAssetException("PACKAGE_MISSING","Required source or vector artifact is absent");
        if(input.Artifacts.Any(a=>string.Equals(a.RelativePath,checksumPath,StringComparison.OrdinalIgnoreCase)||string.Equals(a.RelativePath,archivePath,StringComparison.OrdinalIgnoreCase)))
            throw new ArchitectureAssetException("PACKAGE_RESERVED","Package inputs cannot supply their checksum or archive output");
        var check=checker.Check(input.Policy,input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)).ToArray());
        if(!check.IsValid)throw new ArchitectureAssetException("PACKAGE_POLICY","Source package policy rejected input: "+string.Join(", ",check.Issues.Select(i=>i.Code+":"+i.Path)));
        var ordered=input.Artifacts.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        var rows=ordered.Select(receipts.Describe).Select(a=>a.Sha256+"  "+a.RelativePath+"\n");
        var checksums=new Artifact(checksumPath,Encoding.UTF8.GetBytes(string.Concat(rows)),"text/plain");
        using var buffer=new MemoryStream();
        using(var archive=new ZipArchive(buffer,ZipArchiveMode.Create,true))
        {
            foreach(var artifact in ordered.Append(checksums).OrderBy(a=>a.RelativePath,StringComparer.Ordinal))
            {
                var entry=archive.CreateEntry(artifact.RelativePath,CompressionLevel.NoCompression);
                entry.LastWriteTime=new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero);entry.ExternalAttributes=0;
                using var stream=entry.Open();stream.Write(artifact.Bytes.Span);
            }
        }
        return new(new(archivePath,buffer.ToArray(),"application/zip"),checksums);
    }
}
