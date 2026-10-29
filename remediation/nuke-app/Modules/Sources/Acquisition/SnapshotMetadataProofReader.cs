using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Input admission only. Pin IO, XML package/size rules, archives and HTTP stay
// in their existing single authorities. One receipt field is interpreted here.
public sealed class SnapshotMetadataProofReader(IPinnedArtifactReader inputs,IReadOnlyList<StoredArtifact> pins,
    ISdkArchivePackageVersionValidator versions):ISnapshotMetadataProofReader
{
    public const string RepositoryXmlPath="repository2-3.xml";
    private readonly Dictionary<StoredArtifact,ReadOnlyMemory<byte>> bytes=[];
    private readonly Dictionary<StoredArtifact,ReceiptTag> tags=[];
    public async Task<SnapshotMetadataProofAdmission> BindAsync(SourcePin source,RehydrationCanonicalMetadataReference reference,
        CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(source.Compatibility==AcquisitionCompatibilityProfile.GenericRehydrate)return new(source,null);
        if(!reference.ReceiptPathPresent||reference.ReceiptPathKind!=JsonValueKind.String||string.IsNullOrEmpty(reference.ReceiptPath))Fail("METADATA_REFERENCE");
        var receiptPin=Find(reference.ReceiptPath!);var tag=await Tag(receiptPin,cancellationToken);
        var platform=reference.PlatformVersion;
        if(string.IsNullOrEmpty(platform))Fail("SDK_PLATFORM");
        var xmlPin=Find(RepositoryXmlPath);var xml=await Read(xmlPin,cancellationToken);
        var archive=source.Archive??throw new ArgumentException("Canonical metadata requires an archive pin.");
        var package=new SdkArchivePackageChoice(reference.PackagePathPresent,reference.PackagePathKind==JsonValueKind.String,reference.PackagePath);
        var size=versions.ResolvePackageSize(xml,platform!,package,archive.SizeBytes);
        if(tag.Kind!=JsonValueKind.String||string.IsNullOrWhiteSpace(tag.Value))Fail("ETAG");
        // The original bounded transport does not retrieve/claim container
        // identity, even if an extra caller field declares a whole hash.
        var admitted=archive with{ETag=tag.Value,RepositoryXml=xml,PlatformVersion=platform,
            PackagePath=reference.PackagePathPresent?reference.PackagePath:null,SizeBytes=size,
            ArchiveSha256=null,ArchiveSha1=null};
        return new(source with{Archive=admitted},new(source.LocalPath,source.Sha256,receiptPin,xmlPin,DeclaredArchiveSha256:archive.ArchiveSha256));
    }
    private StoredArtifact Find(string path)
    {
        _=ArtifactPaths.Segments(path);
        var candidates=pins.Where(pin=>pin.RelativePath==path).ToArray();
        if(candidates.Length!=1)Fail("METADATA_PIN_REQUIRED");
        return candidates[0];
    }
    private async Task<ReadOnlyMemory<byte>> Read(StoredArtifact pin,CancellationToken cancellationToken)
    {
        if(bytes.TryGetValue(pin,out var cached))return cached;
        var read=await inputs.ReadAsync(pin,cancellationToken);
        ReadOnlyMemory<byte> retained=read.ToArray();bytes.Add(pin,retained);return retained;
    }
    private async Task<ReceiptTag> Tag(StoredArtifact pin,CancellationToken cancellationToken)
    {
        if(tags.TryGetValue(pin,out var cached))return cached;
        var input=await Read(pin,cancellationToken);
        try
        {
            using var document=JsonDocument.Parse(input);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)Fail("METADATA_RECEIPT_JSON");
            if(!document.RootElement.TryGetProperty("etag",out var value))Fail("ETAG");
            var tag=new ReceiptTag(value.ValueKind,value.ValueKind==JsonValueKind.String?value.GetString():null);
            tags.Add(pin,tag);return tag;
        }
        catch(JsonException){throw new TransportException("METADATA_RECEIPT_JSON","Metadata receipt JSON is malformed.");}
    }
    private sealed record ReceiptTag(JsonValueKind Kind,string? Value);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code)=>throw new TransportException(code,"Canonical metadata input admission failed; originals retained.");
}
