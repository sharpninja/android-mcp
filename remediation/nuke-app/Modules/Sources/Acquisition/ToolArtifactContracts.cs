using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;
public enum ToolArtifactFormat { Stored, Zip }
public enum ToolArtifactAccess { Online, Offline }
public sealed record ToolArtifactBounds(long MaxDownloadBytes,long MaxExpandedBytes,long MaxMemberBytes,int MaxEntries);
public sealed record ToolArtifactPin(string Id,string Version,Uri Url,string Sha256,long SizeBytes,
    ToolArtifactFormat Format,ToolArtifactBounds Bounds,Uri? PublishedChecksumUrl=null,IReadOnlyList<Uri>? AllowedRedirects=null);
public sealed record ToolArtifactRequest(ToolArtifactPin Pin,ToolArtifactAccess Access,string CachePath,
    string? ExtractionPrefix=null,string? OfflineChecksumProofPath=null);
public sealed record ToolArtifactReceipt(ToolArtifactPin Pin,StoredArtifact Artifact,IReadOnlyList<StoredArtifact> Files,
    StoredArtifact? ChecksumProof,string Status,ToolArtifactAccess Access,Uri ArtifactUrl);
public interface IToolArtifactAcquirer
{
    Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken cancellationToken=default);
}
public sealed record ToolChecksumProof(int Version,string ArtifactUrl,string ArtifactSha256,long ArtifactSize,
    string ChecksumUrl,string ResponseSha256,string ResponseBase64);
