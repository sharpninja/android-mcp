using System.IO.Compression;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class MetadataArchiveReader:IMetadataArchiveReader
{
    private static readonly HashSet<string> SelectedLeaves=new(StringComparer.Ordinal)
    {"source.properties","sdk.properties","package.xml","api-versions.xml","api-versions-versioned.xml","annotations.zip","public.xml","attrs_manifest.xml","optional.json","AndroidManifest.xml"};
    public static bool IsSelected(string path)=>SelectedLeaves.Contains(path.Split('/')[^1]);
    public MetadataArchiveEvidence Read(IRangeSource source)
    {
        using var stream=new BoundedRangeStream(source);using var archive=new ZipArchive(stream,ZipArchiveMode.Read);
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);var entries=new List<ArchiveEntryEvidence>();
        foreach(var entry in archive.Entries)
        {
            if(entry.FullName.EndsWith('/'))continue;_=ArtifactPaths.Segments(entry.FullName);
            var selected=IsSelected(entry.FullName);entries.Add(new(entry.FullName,entry.Length,entry.CompressedLength,entry.Crc32.ToString("x8"),selected));
            if(!selected)continue;
            if(entry.Length>32*1024*1024||entry.CompressedLength>8*1024*1024)throw new TransportException("METADATA_LIMIT","Metadata ZIP member exceeds bounded size.");
            var bytes=ReadMember(entry,8*1024*1024);
            if(!files.TryAdd(entry.FullName,bytes))throw new TransportException("ARCHIVE_DUPLICATE","Duplicate selected ZIP member.");
        }
        return new(files,entries,null,false);
    }
    internal static byte[] ReadMember(ZipArchiveEntry entry,long compressedLimit,long uncompressedLimit=32*1024*1024,CancellationToken cancellationToken=default)
    {
        if(entry.Length>uncompressedLimit||entry.CompressedLength>compressedLimit)throw new TransportException("METADATA_LIMIT","ZIP member exceeds bounded size.");
        using var input=entry.Open();var bytes=NativeArchiveExtractor.ReadBounded(input,checked((int)entry.Length),cancellationToken);
        if(Crc32(bytes)!=entry.Crc32)throw new TransportException("ZIP_CRC","ZIP member CRC mismatch.");return bytes;
    }
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc=uint.MaxValue;foreach(var value in bytes){crc^=value;for(var bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}return ~crc;
    }
    private sealed class BoundedRangeStream(IRangeSource source):Stream
    {
        private long position;
        public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
        public override long Length=>source.Length;public override long Position{get=>position;set=>Seek(value,SeekOrigin.Begin);}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            var count=checked((int)Math.Min(buffer.Length,Length-position));if(count==0)return 0;
            if(count>8*1024*1024)throw new TransportException("RANGE_LIMIT","Metadata read exceeds8MiB.");
            var bytes=source.Read(position,count);if(bytes.Length!=count)throw new TransportException("BODY_LENGTH","Short metadata range.");
            bytes.CopyTo(buffer);position+=count;return count;
        }
        public override long Seek(long offset,SeekOrigin origin)
        {var next=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>position,SeekOrigin.End=>Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);if(next<0||next>Length)throw new TransportException("ZIP_SEEK","Metadata seek outside archive.");return position=next;}
        public override void Flush(){}public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
