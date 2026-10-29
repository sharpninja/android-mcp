using System.Formats.Tar;
using System.IO.Compression;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Read pinned codeload source archives as data; all validation precedes artifact publication.</summary>
public sealed class NativeArchiveExtractor:INativeArchiveExtractor
{
    public async Task<NativeArchiveEvidence> ExtractTarGZipAsync(ReadOnlyMemory<byte> archive,IArtifactStore store,
        string destinationPrefix,CancellationToken cancellationToken=default)
    {
        _=ArtifactPaths.Segments(destinationPrefix);
        if(archive.Length>64*1024*1024)throw new TransportException("ARCHIVE_LIMIT","Source archive exceeds64MiB.");
        using var raw=new MemoryStream(archive.ToArray(),false);
        using var gzip=new GZipStream(raw,CompressionMode.Decompress);
        using var tar=new TarReader(gzip);
        var staged=new List<Artifact>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var metadata=new List<ArchiveMetadataEvidence>();string? commonRoot=null;long total=0;
        while(true)
        {
            cancellationToken.ThrowIfCancellationRequested();var entry=tar.GetNextEntry();if(entry is null)break;
            if(entry is PaxGlobalExtendedAttributesTarEntry global)
            {
                if(global.GlobalExtendedAttributes.ContainsKey("path")||global.GlobalExtendedAttributes.ContainsKey("linkpath"))throw new TransportException("ARCHIVE_METADATA_PATH","Global PAX path/link overrides are not accepted.");
                if(entry.Length>32*1024*1024||total+entry.Length>64*1024*1024)throw new TransportException("ARCHIVE_LIMIT","Expanded archive metadata exceeds bounded size.");
                total+=entry.Length;metadata.Add(new(entry.Name,entry.EntryType.ToString(),global.GlobalExtendedAttributes.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)));continue;
            }
            var name=entry.Name.TrimEnd('/');var parts=ArtifactPaths.Segments(name);
            commonRoot??=parts[0];if(parts[0]!=commonRoot)throw new TransportException("ARCHIVE_ROOT","Archive contains multiple source roots.");
            if(entry.EntryType==TarEntryType.Directory)continue;
            if(entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))throw new TransportException("ARCHIVE_LINK","Source archive contains a link or special file.");
            if(parts.Length<2)throw new TransportException("ARCHIVE_ROOT","Source files require one pinned archive root.");
            var target=destinationPrefix+"/"+string.Join('/',parts.Skip(1));_=ArtifactPaths.Segments(target);
            if(!paths.Add(target))throw new TransportException("ARCHIVE_DUPLICATE","Source archive contains duplicate portable targets.");
            if(entry.Length>32*1024*1024||entry.Length<0||total+entry.Length>64*1024*1024)throw new TransportException("ARCHIVE_LIMIT","Expanded source archive exceeds bounded size.");
            var bytes=entry.DataStream is null?[]:ReadBounded(entry.DataStream,checked((int)entry.Length),cancellationToken);
            total+=bytes.Length;staged.Add(new(target,bytes,"application/octet-stream"));
        }
        // No partial entry set is published until every member passes path/type/size checks.
        var files=new List<StoredArtifact>();foreach(var artifact in staged)files.Add(await store.WriteAsync(artifact,cancellationToken));
        return new(ContentHash.Sha256(archive.Span),files,metadata);
    }
    internal static byte[] ReadBounded(Stream input,int expected,CancellationToken cancellationToken=default)
    {
        using var output=new MemoryStream();var buffer=new byte[64*1024];
        while(true){cancellationToken.ThrowIfCancellationRequested();var count=input.Read(buffer);if(count==0)break;if(output.Length+count>expected)throw new TransportException("MEMBER_LENGTH","Archive member expanded beyond its declared size.");output.Write(buffer,0,count);}
        if(output.Length!=expected)throw new TransportException("MEMBER_LENGTH","Archive member length mismatch.");return output.ToArray();
    }
}
