using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SourceAcquirer(IByteTransport transport,IArtifactStore store,SourceArchiveAcquirer? archiveSession=null)
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
            using var local=archiveSession is null?new SourceArchiveAcquirer(transport,store):null;
            bytes=await (archiveSession??local!).ReadMemberAsync(pin,cancellationToken);
        }
        else if(pin.RequiredLocalSnapshot is not null)
        {
            // Missing immutable bundled snapshots never fall back to their mutable HTTP URL.
            bytes=await store.ReadAsync(pin.RequiredLocalSnapshot,cancellationToken);
        }
        else
        {
            var url=pin.Url;
            var gitiles=pin.Encoding is SourceWireEncoding.GitilesText or SourceWireEncoding.SnapshotRestoreGitiles or SourceWireEncoding.SnapshotFetchGitiles;
            if(gitiles&&!url.Query.TrimStart('?').Split('&').Contains("format=TEXT"))url=new Uri(url.AbsoluteUri+(url.Query.Length==0?"?":"&")+"format=TEXT");
            var response=await transport.SendAsync(new(url,pin.Revision,
                gitiles?null:pin.Sha256,
                gitiles?null:pin.SizeBytes,
                Retry:pin.Compatibility==AcquisitionCompatibilityProfile.CanonicalRestore?TransportRetryPolicy.CanonicalRestore:TransportRetryPolicy.SingleAttempt),cancellationToken);
            bytes=gitiles?DecodeGitiles(response.Bytes.Span,pin.Encoding):response.Bytes;
        }
        Verify(bytes.Span,pin);
        var artifact=await store.WriteAsync(new(pin.LocalPath,bytes,"application/octet-stream"),cancellationToken);
        return new(artifact,"downloaded",pin.Url,pin.Revision,pin.GitBlob);
    }
    public static byte[] DecodeGitiles(ReadOnlySpan<byte> bytes)=>DecodeGitiles(bytes,SourceWireEncoding.GitilesText);
    public static byte[] DecodeGitiles(ReadOnlySpan<byte> bytes,SourceWireEncoding encoding)
    {
        string text;
        if(encoding==SourceWireEncoding.GitilesText)
        {
            // Preserve the established generic whitespace-normalized contract.
            var chars=new StringBuilder();
            foreach(var value in bytes){if(value is 9 or 10 or 11 or 12 or 13 or 32)continue;if(value>127)throw new TransportException("BASE64","Gitiles TEXT is not ASCII base64.");chars.Append((char)value);}
            text=chars.ToString();
            if(!Regex.IsMatch(text,"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$"))throw new TransportException("BASE64","Invalid Gitiles TEXT base64.");
        }
        else if(encoding is SourceWireEncoding.SnapshotRestoreGitiles or SourceWireEncoding.SnapshotFetchGitiles)
            text=NormalizeSnapshotBase64(bytes,encoding==SourceWireEncoding.SnapshotFetchGitiles);
        else throw new TransportException("BASE64","Explicit Gitiles decoding policy required.");
        // All policies use this sole byte decoder. The compatibility adapter
        // only admits/normalizes alphabet and padding; it never decodes bits.
        try{return Convert.FromBase64String(text);}catch(FormatException){throw new TransportException("BASE64","Invalid Gitiles TEXT base64.");}
    }
    private static string NormalizeSnapshotBase64(ReadOnlySpan<byte> bytes,bool permissive)
    {
        var chars=new StringBuilder();var position=0;var pads=0;var paddingStarted=false;
        for(var index=0;index<bytes.Length;index++)
        {
            var value=bytes[index];
            if(value=='=')
            {
                if(!permissive&&position==0)Invalid();
                paddingStarted=true;
                if(position>=2&&position+ ++pads>=4)
                {
                    if(!permissive&&index+1!=bytes.Length)Invalid();
                    chars.Append('=',4-position);return chars.ToString();
                }
                continue;
            }
            var alphabet=value is >=(byte)'A' and <=(byte)'Z' or >=(byte)'a' and <=(byte)'z' or >=(byte)'0' and <=(byte)'9' or (byte)'+' or (byte)'/';
            if(!alphabet){if(!permissive)Invalid();continue;}
            if(!permissive&&paddingStarted)Invalid();
            pads=0;chars.Append((char)value);position=(position+1)%4;
        }
        if(position!=0)Invalid();
        return chars.ToString();
        static void Invalid()=>throw new TransportException("BASE64","Invalid Snapshot Gitiles base64.");
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
