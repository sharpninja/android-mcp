using System.Globalization;
using System.IO.Compression;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SourceArchiveAcquirer(IByteTransport transport,IArtifactStore store)
{
    public async Task<ReadOnlyMemory<byte>> ReadMemberAsync(SourcePin source,CancellationToken cancellationToken=default)
    {
        var pin=source.Archive??throw new ArgumentException("Source requires archive pin.");_=ArtifactPaths.Segments(pin.Member);
        if(pin.ArchiveSha256 is not null)
        {
            var path=pin.ArchiveLocalPath??"archives/"+pin.ArchiveSha256+".zip";_=ArtifactPaths.Segments(path);
            ReadOnlyMemory<byte> bytes;bool downloaded=false;
            try{bytes=await store.ReadAsync(path,cancellationToken);}
            catch(FileNotFoundException){bytes=await Download();downloaded=true;}
            catch(DirectoryNotFoundException){bytes=await Download();downloaded=true;}
            if(bytes.Length>64*1024*1024)throw new TransportException("ARCHIVE_LIMIT","Source archive exceeds64MiB.");
            if(ContentHash.Sha256(bytes.Span)!=pin.ArchiveSha256||pin.ArchiveSha1 is not null&&ContentHash.AidlSha1Legacy(bytes.Span)!=pin.ArchiveSha1)throw new TransportException("ARCHIVE_HASH","Source archive hash differs; existing bytes retained.");
            if(pin.SizeBytes is not null&&bytes.Length!=pin.SizeBytes)throw new TransportException("ARCHIVE_SIZE","Source archive size differs.");
            if(downloaded)await store.WriteAsync(new(path,bytes,"application/zip"),cancellationToken);
            using var stream=new MemoryStream(bytes.ToArray(),false);using var archive=new ZipArchive(stream,ZipArchiveMode.Read);
            var matches=archive.Entries.Where(x=>x.FullName==pin.Member).ToArray();if(matches.Length!=1)throw new TransportException("ARCHIVE_MEMBER","Source archive member absent or ambiguous.");
            return MetadataArchiveReader.ReadMember(matches[0],64*1024*1024);
            async Task<ReadOnlyMemory<byte>> Download()=> (await transport.SendAsync(new(pin.Url,source.Revision,pin.ArchiveSha256,pin.SizeBytes,Retry:Policy(pin)),cancellationToken)).Bytes;
        }
        if(!MetadataArchiveReader.IsSelected(pin.Member))throw new TransportException("METADATA_SELECTION","Range acquisition is restricted to selected metadata members.");
        if(pin.Compatibility==AcquisitionCompatibilityProfile.CanonicalRestore)
        {
            if(string.IsNullOrWhiteSpace(pin.ETag))throw new TransportException("ETAG","Canonical metadata restore requires a pinned ETag.");
            var size=new SdkArchiveVersionValidator().ResolveSize(pin.RepositoryXml,pin.PlatformVersion??"",pin.PackagePath,pin.SizeBytes);pin=pin with{SizeBytes=size};
        }
        var ranges=await HttpRangeSource.CreateAsync(transport,pin,source.Revision,source.Sha256,cancellationToken);
        var metadata=new MetadataArchiveReader().Read(ranges);
        if(!metadata.Files.TryGetValue(pin.Member,out var member))throw new TransportException("ARCHIVE_MEMBER","Selected metadata member missing.");
        return member;
    }
    internal static TransportRetryPolicy Policy(SourceArchivePin pin)=>pin.Compatibility==AcquisitionCompatibilityProfile.CanonicalRestore?TransportRetryPolicy.CanonicalRestore:TransportRetryPolicy.SingleAttempt;
}

public sealed class HttpRangeSource:IRangeSource
{
    private readonly IByteTransport transport;private readonly SourceArchivePin pin;private readonly string revision;private readonly string memberSha;private readonly CancellationToken cancellationToken;
    public long Length{get;}
    private HttpRangeSource(IByteTransport transport,SourceArchivePin pin,string revision,string memberSha,long length,CancellationToken cancellationToken)
    {this.transport=transport;this.pin=pin;this.revision=revision;this.memberSha=memberSha;Length=length;this.cancellationToken=cancellationToken;}
    public static async Task<HttpRangeSource> CreateAsync(IByteTransport transport,SourceArchivePin pin,string revision,string memberSha,CancellationToken cancellationToken=default)
    {
        var length=pin.SizeBytes;
        if(length is null)
        {
            var response=await transport.SendAsync(new(pin.Url,revision,ExpectedLength:1,MaxBytes:1,Range:new(0,1),IfMatch:pin.ETag,ExpectedETag:pin.ETag,Retry:SourceArchiveAcquirer.Policy(pin),PartialMemberSha256:memberSha),cancellationToken);
            if(response.ContentRange is null||!long.TryParse(response.ContentRange.Split('/')[^1],NumberStyles.None,CultureInfo.InvariantCulture,out var measured)||measured<=0)throw new TransportException("CONTENT_RANGE","Archive size probe invalid.");
            length=measured;
        }
        if(length<=0)throw new TransportException("ARCHIVE_SIZE","Metadata archive size must be positive.");
        return new(transport,pin,revision,memberSha,length.Value,cancellationToken);
    }
    public byte[] Read(long offset,int count)
    {
        if(offset<0||count<0||count>8*1024*1024||offset>Length-count)throw new TransportException("RANGE_LIMIT","Metadata request outside bounded archive.");
        if(count==0)return [];
        var request=new PinnedRequest(pin.Url,revision,ExpectedLength:count,MaxBytes:8*1024*1024,Range:new(offset,count),IfMatch:pin.ETag,ExpectedETag:pin.ETag,Retry:SourceArchiveAcquirer.Policy(pin),ExpectedTotalLength:Length,PartialMemberSha256:memberSha);
        var response=transport.SendAsync(request,cancellationToken).GetAwaiter().GetResult();
        if(response.StatusCode!=206||response.ContentRange!=$"bytes {offset}-{offset+count-1}/{Length}"||response.Bytes.Length!=count||response.ContentEncoding!="identity"||pin.ETag is not null&&response.ETag!=pin.ETag)throw new TransportException("CONTENT_RANGE","Metadata range/version mismatch.");
        return response.Bytes.ToArray();
    }
}
