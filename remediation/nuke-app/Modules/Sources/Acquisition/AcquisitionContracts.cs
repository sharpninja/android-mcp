using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public enum SourceWireEncoding { Raw, GitilesText, SnapshotRestoreGitiles, SnapshotFetchGitiles }
public sealed record SourcePin(string LocalPath,Uri Url,string Revision,string Sha256,long? SizeBytes=null,
    string? GitBlob=null,SourceWireEncoding Encoding=SourceWireEncoding.Raw,string? RequiredLocalSnapshot=null,
    SourceArchivePin? Archive=null,AcquisitionCompatibilityProfile Compatibility=AcquisitionCompatibilityProfile.CanonicalRestore);
public sealed record SourceArchivePin(Uri Url,string Member,string? ArchiveSha256=null,string? ArchiveSha1=null,
    long? SizeBytes=null,string? ETag=null,string? ArchiveLocalPath=null,
    AcquisitionCompatibilityProfile Compatibility=AcquisitionCompatibilityProfile.CanonicalRestore,
    ReadOnlyMemory<byte> RepositoryXml=default,string? PlatformVersion=null,string? PackagePath=null);
public sealed record AcquiredSource(StoredArtifact Artifact,string Status,Uri Url,string Revision,string? GitBlob);
public interface IRangeSource
{
    long Length {get;}
    byte[] Read(long offset,int count);
}
public sealed record ArchiveEntryEvidence(string Path,long SizeBytes,long CompressedBytes,string Crc32,bool Selected);
public sealed record MetadataArchiveEvidence(IReadOnlyDictionary<string,byte[]> Files,
    IReadOnlyList<ArchiveEntryEvidence> Entries,string? WholeArchiveSha256,bool CompleteArchiveIdentity);
public interface IMetadataArchiveReader
{
    MetadataArchiveEvidence Read(IRangeSource source);
}
public sealed record ArchiveMetadataEvidence(string Name,string EntryType,IReadOnlyDictionary<string,string> Attributes);
public sealed record NativeArchiveEvidence(string ArchiveSha256,IReadOnlyList<StoredArtifact> Files,IReadOnlyList<ArchiveMetadataEvidence>? Metadata=null);
public interface INativeArchiveExtractor
{
    Task<NativeArchiveEvidence> ExtractTarGZipAsync(ReadOnlyMemory<byte> archive,IArtifactStore store,
        string destinationPrefix, CancellationToken cancellationToken=default);
}
