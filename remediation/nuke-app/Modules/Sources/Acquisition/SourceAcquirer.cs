using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SourceAcquirer(IByteTransport transport,IArtifactStore store)
{
    public async Task<AcquiredSource> AcquireAsync(SourcePin pin,CancellationToken cancellationToken=default)
    {
        ValidatePin(pin);
        try
        {
            var existing=await store.ReadAsync(pin.LocalPath,cancellationToken);Verify(existing.Span,pin);
            return new(new(pin.LocalPath,pin.Sha256,existing.Length,"application/octet-stream"),"reused",pin.Url,pin.Revision,pin.GitBlob);
        }
        catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        ReadOnlyMemory<byte> bytes;
        if(pin.Archive is not null)
        {
            bytes=await new SourceArchiveAcquirer(transport,store).ReadMemberAsync(pin,cancellationToken);
        }
        else if(pin.RequiredLocalSnapshot is not null)
        {
            // Missing immutable bundled snapshots never fall back to their mutable HTTP URL.
            bytes=await store.ReadAsync(pin.RequiredLocalSnapshot,cancellationToken);
        }
        else
        {
            var url=pin.Url;
            if(pin.Encoding==SourceWireEncoding.GitilesText&&!url.Query.TrimStart('?').Split('&').Contains("format=TEXT"))url=new Uri(url.AbsoluteUri+(url.Query.Length==0?"?":"&")+"format=TEXT");
            var response=await transport.SendAsync(new(url,pin.Revision,
                pin.Encoding==SourceWireEncoding.Raw?pin.Sha256:null,
                pin.Encoding==SourceWireEncoding.Raw?pin.SizeBytes:null,
                Retry:pin.Compatibility==AcquisitionCompatibilityProfile.CanonicalRestore?TransportRetryPolicy.CanonicalRestore:TransportRetryPolicy.SingleAttempt),cancellationToken);
            bytes=pin.Encoding==SourceWireEncoding.GitilesText?DecodeGitiles(response.Bytes.Span):response.Bytes;
        }
        Verify(bytes.Span,pin);
        var artifact=await store.WriteAsync(new(pin.LocalPath,bytes,"application/octet-stream"),cancellationToken);
        return new(artifact,"downloaded",pin.Url,pin.Revision,pin.GitBlob);
    }
    public static byte[] DecodeGitiles(ReadOnlySpan<byte> bytes)
    {
        var chars=new StringBuilder();
        foreach(var value in bytes){if(value is 9 or 10 or 11 or 12 or 13 or 32)continue;if(value>127)throw new TransportException("BASE64","Gitiles TEXT is not ASCII base64.");chars.Append((char)value);}
        var text=chars.ToString();
        if(!Regex.IsMatch(text,"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$"))throw new TransportException("BASE64","Invalid Gitiles TEXT base64.");
        try{return Convert.FromBase64String(text);}catch(FormatException){throw new TransportException("BASE64","Invalid Gitiles TEXT base64.");}
    }
    public static void Verify(ReadOnlySpan<byte> bytes,SourcePin pin)
    {
        if(ContentHash.Sha256(bytes)!=pin.Sha256)throw new TransportException("SOURCE_HASH","Source SHA256 differs; original bytes retained.");
        if(pin.SizeBytes is not null&&bytes.Length!=pin.SizeBytes)throw new TransportException("SOURCE_SIZE","Source size differs.");
        if(pin.GitBlob is not null&&ContentHash.GitBlobSha1Legacy(bytes)!=pin.GitBlob)throw new TransportException("SOURCE_GIT_BLOB","Source Git content address differs.");
    }
    internal static void ValidatePin(SourcePin pin)
    {
        _=ArtifactPaths.Segments(pin.LocalPath);if(pin.RequiredLocalSnapshot is not null)_=ArtifactPaths.Segments(pin.RequiredLocalSnapshot);
        if(pin.Archive is not null){_=ArtifactPaths.Segments(pin.Archive.Member);if(pin.Archive.ArchiveLocalPath is not null)_=ArtifactPaths.Segments(pin.Archive.ArchiveLocalPath);}
        if(!Regex.IsMatch(pin.Sha256,"^[a-f0-9]{64}$")||pin.SizeBytes<0||pin.GitBlob is not null&&!Regex.IsMatch(pin.GitBlob,"^[a-f0-9]{40}$"))throw new TransportException("PIN","Invalid source content pin.");
    }
}
