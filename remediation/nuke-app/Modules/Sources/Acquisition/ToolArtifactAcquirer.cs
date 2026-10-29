using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Pinned tool bytes are data. Tool execution/admission and lock generation belong to the caller.</summary>
public sealed class ToolArtifactAcquirer(IByteTransport transport,IArtifactStore store,IArtifactLeaseProvider leases,ICanonicalJson canonical):IToolArtifactAcquirer
{
    public async Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);var pin=request.Pin;
        await using var lease=await leases.AcquireAsync("tool-artifact/"+request.CachePath,cancellationToken);
        var cached=await OptionalRead(request.CachePath,cancellationToken);var reused=cached is not null;
        if(cached is not null)Verify(cached.Value.Span,pin);
        if(cached is null&&request.Access==ToolArtifactAccess.Offline)throw new TransportException("TOOL_OFFLINE_MISS","Offline mode requires the exact cached tool artifact.");
        Artifact? newProof=null;StoredArtifact? proofReceipt=null;
        if(pin.PublishedChecksumUrl is not null)
        {
            if(request.Access==ToolArtifactAccess.Online)
            {
                var response=await transport.SendAsync(new(pin.PublishedChecksumUrl,pin.Version,MaxBytes:4096,
                    ToolTransfer:new(pin.Sha256,pin.SizeBytes,pin.Bounds.MaxDownloadBytes,ToolTransferKind.PublishedChecksum,pin.AllowedRedirects)),cancellationToken);
                CheckPublished(response.Bytes.Span,pin.Sha256);
                var proof=new ToolChecksumProof(1,pin.Url.AbsoluteUri,pin.Sha256,pin.SizeBytes,pin.PublishedChecksumUrl.AbsoluteUri,ContentHash.Sha256(response.Bytes.Span),Convert.ToBase64String(response.Bytes.Span));
                var bytes=canonical.Serialize(JsonSerializer.SerializeToElement(proof));var digest=ContentHash.Sha256(bytes);
                newProof=new("tool-checksums/"+digest+".json",bytes,"application/json");proofReceipt=Describe(newProof);
            }
            else
            {
                if(request.OfflineChecksumProofPath is null)throw new TransportException("TOOL_CHECKSUM_PROOF","Offline mode requires retained published checksum proof.");
                var bytes=await OptionalRead(request.OfflineChecksumProofPath,cancellationToken);
                if(bytes is null)throw new TransportException("TOOL_CHECKSUM_PROOF","Retained checksum proof is absent.");
                ValidateProof(bytes.Value,pin);proofReceipt=Describe(new(request.OfflineChecksumProofPath,bytes.Value,"application/json"));
            }
        }
        var artifactUrl=pin.Url;ReadOnlyMemory<byte> body;
        if(cached is not null)body=cached.Value;
        else
        {
            var response=await transport.SendAsync(new(pin.Url,pin.Version,pin.Sha256,pin.SizeBytes,pin.SizeBytes,
                ToolTransfer:new(pin.Sha256,pin.SizeBytes,pin.Bounds.MaxDownloadBytes,AllowedRedirects:pin.AllowedRedirects)),cancellationToken);
            body=response.Bytes;artifactUrl=response.Url;Verify(body.Span,pin);
        }
        var files=pin.Format==ToolArtifactFormat.Zip?new ToolZipExtractor().Preflight(body,pin,request.ExtractionPrefix!,cancellationToken):[];
        var archive=new Artifact(request.CachePath,body,pin.Format==ToolArtifactFormat.Zip?"application/zip":"application/octet-stream");
        var planned=new List<Artifact>{archive};planned.AddRange(files);if(newProof is not null)planned.Add(newProof);
        ValidateTargets(planned);
        var missing=new HashSet<string>(StringComparer.Ordinal);
        foreach(var artifact in planned)
        {
            cancellationToken.ThrowIfCancellationRequested();var old=await OptionalRead(artifact.RelativePath,cancellationToken);
            if(old is null){missing.Add(artifact.RelativePath);continue;}
            if(!old.Value.Span.SequenceEqual(artifact.Bytes.Span))throw new IOException("Tool destination conflicts; original bytes retained: "+artifact.RelativePath);
        }
        foreach(var artifact in planned)if(missing.Contains(artifact.RelativePath))await store.WriteAsync(artifact,cancellationToken);
        return new(pin,Describe(archive),files.Select(Describe).ToArray(),proofReceipt,reused?"reused":"downloaded",request.Access,artifactUrl);
    }
    private async Task<ReadOnlyMemory<byte>?> OptionalRead(string path,CancellationToken cancellationToken)
    {try{return await store.ReadAsync(path,cancellationToken);}catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}}
    private static StoredArtifact Describe(Artifact value)=>new(value.RelativePath,ContentHash.Sha256(value.Bytes.Span),value.Bytes.Length,value.MediaType);
    private static void Verify(ReadOnlySpan<byte> bytes,ToolArtifactPin pin)
    {
        if(ContentHash.Sha256(bytes)!=pin.Sha256)throw new TransportException("TOOL_HASH","Tool artifact hash mismatch; original bytes retained.");
        if(bytes.Length!=pin.SizeBytes)throw new TransportException("TOOL_SIZE","Tool artifact differs from its exact byte count.");
    }
    private static void CheckPublished(ReadOnlySpan<byte> bytes,string expected)
    {
        if(bytes.Length>4096)throw new TransportException("TOOL_CHECKSUM","Published checksum response exceeds4KiB.");
        foreach(var value in bytes)if(value>127)throw new TransportException("TOOL_CHECKSUM","Published checksum response is not ASCII.");
        var first=Encoding.ASCII.GetString(bytes).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if(first!=expected)throw new TransportException("TOOL_CHECKSUM","Published checksum differs from committed tool pin.");
    }
    private void ValidateProof(ReadOnlyMemory<byte> bytes,ToolArtifactPin pin)
    {
        try
        {
            if(bytes.Length>32768||!canonical.Serialize(bytes).AsSpan().SequenceEqual(bytes.Span))throw new FormatException("Noncanonical or oversized proof.");
            var proof=JsonSerializer.Deserialize<ToolChecksumProof>(bytes.Span)??throw new FormatException("Empty proof.");
            if(proof.Version!=1||proof.ArtifactUrl!=pin.Url.AbsoluteUri||proof.ArtifactSha256!=pin.Sha256||proof.ArtifactSize!=pin.SizeBytes||proof.ChecksumUrl!=pin.PublishedChecksumUrl!.AbsoluteUri||string.IsNullOrEmpty(proof.ResponseBase64)||proof.ResponseBase64.Length>5464)throw new FormatException("Proof identity differs.");
            var raw=Convert.FromBase64String(proof.ResponseBase64);if(ContentHash.Sha256(raw)!=proof.ResponseSha256)throw new FormatException("Proof response hash differs.");
            CheckPublished(raw,pin.Sha256);
        }
        catch(Exception error) when(error is FormatException or JsonException or TransportException or ArgumentException)
        {throw new TransportException("TOOL_CHECKSUM_PROOF","Retained checksum proof is invalid or bound to another artifact.");}
    }
    private static void Validate(ToolArtifactRequest request)
    {
        var pin=request.Pin;_=ArtifactPaths.Segments(request.CachePath);if(request.ExtractionPrefix is not null)_=ArtifactPaths.Segments(request.ExtractionPrefix);if(request.OfflineChecksumProofPath is not null)_=ArtifactPaths.Segments(request.OfflineChecksumProofPath);
        if(!pin.Url.IsAbsoluteUri||pin.Url.Scheme!=Uri.UriSchemeHttps||pin.Url.UserInfo.Length!=0||pin.PublishedChecksumUrl is not null&&(!pin.PublishedChecksumUrl.IsAbsoluteUri||pin.PublishedChecksumUrl.Scheme!=Uri.UriSchemeHttps||pin.PublishedChecksumUrl.UserInfo.Length!=0)||pin.AllowedRedirects?.Any(x=>!x.IsAbsoluteUri||x.Scheme!=Uri.UriSchemeHttps||x.UserInfo.Length!=0)==true)throw new TransportException("URL","Tool artifact and admitted endpoints require HTTPS without credentials.");
        if(string.IsNullOrWhiteSpace(pin.Id)||string.IsNullOrWhiteSpace(pin.Version)||!Regex.IsMatch(pin.Sha256,"^[a-f0-9]{64}$")||!Enum.IsDefined(pin.Format)||!Enum.IsDefined(request.Access))throw new TransportException("TOOL_PIN","Explicit tool identity, format, access mode and SHA256 are required.");
        if(pin.SizeBytes<0||pin.Bounds.MaxDownloadBytes<pin.SizeBytes||pin.Bounds.MaxDownloadBytes>512L*1024*1024||pin.Format==ToolArtifactFormat.Zip&&(pin.Bounds.MaxExpandedBytes is <=0 or >512L*1024*1024||pin.Bounds.MaxMemberBytes is <=0 or >128L*1024*1024||pin.Bounds.MaxEntries is <=0 or >10000))throw new TransportException("TOOL_BOUNDS","Tool artifact has invalid or unsupported explicit bounds.");
        if(pin.Format==ToolArtifactFormat.Zip&&request.ExtractionPrefix is null||pin.Format==ToolArtifactFormat.Stored&&request.ExtractionPrefix is not null)throw new ArgumentException("ZIP requires an extraction prefix; stored tool bytes do not accept one.");
    }
    private static void ValidateTargets(IReadOnlyList<Artifact> files)
    {
        var all=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in files)if(!all.Add(file.RelativePath))throw new TransportException("TOOL_ZIP_COLLISION","Tool artifact roles have duplicate or case-colliding targets.");
        foreach(var file in files){var parts=file.RelativePath.Split('/');for(var count=1;count<parts.Length;count++)if(all.Contains(string.Join('/',parts.Take(count))))throw new TransportException("TOOL_ZIP_COLLISION","Tool target is also a parent directory.");}
    }
}
