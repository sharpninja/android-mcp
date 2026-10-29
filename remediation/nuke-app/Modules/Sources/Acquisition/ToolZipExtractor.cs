using System.IO.Compression;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Tool-specific ZIP admission over the common ZIP payload/CRC reader.</summary>
public sealed class ToolZipExtractor
{
    public IReadOnlyList<Artifact> Preflight(ReadOnlyMemory<byte> bytes,ToolArtifactPin pin,string prefix,CancellationToken cancellationToken=default)
    {
        _=ArtifactPaths.Segments(prefix);
        using var input=new MemoryStream(bytes.ToArray(),false);using var archive=new ZipArchive(input,ZipArchiveMode.Read);
        if(archive.Entries.Count>pin.Bounds.MaxEntries)throw new TransportException("TOOL_ZIP_LIMIT","Tool ZIP entry count exceeds its admitted bound.");
        var entries=new List<(ZipArchiveEntry Entry,string Path,bool Directory)>();var kinds=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);var spellings=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);long expanded=0;
        foreach(var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();var directory=entry.FullName.EndsWith('/');var path=directory?entry.FullName[..^1]:entry.FullName;var parts=ArtifactPaths.Segments(path);
            if(!kinds.TryAdd(path,directory))throw new TransportException("TOOL_ZIP_COLLISION","Duplicate or case-colliding ZIP target.");
            for(var count=1;count<=parts.Length;count++)
            {var component=string.Join('/',parts.Take(count));if(spellings.TryGetValue(component,out var old)&&old!=component)throw new TransportException("TOOL_ZIP_COLLISION","ZIP path components have conflicting case.");spellings[component]=component;}
            var mode=(unchecked((uint)entry.ExternalAttributes)>>16)&0xf000u;
            if((entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0||mode is not (0 or 0x8000 or 0x4000)||directory&&mode==0x8000||!directory&&mode==0x4000||directory&&entry.Length!=0)
                throw new TransportException("TOOL_ZIP_SPECIAL","Tool ZIP contains a link, special file or inconsistent directory entry.");
            if(!directory)
            {
                if(entry.Length<0||entry.Length>pin.Bounds.MaxMemberBytes||expanded>pin.Bounds.MaxExpandedBytes-entry.Length)throw new TransportException("TOOL_ZIP_LIMIT","Tool ZIP expansion exceeds admitted bounds.");
                expanded+=entry.Length;
            }
            entries.Add((entry,path,directory));
        }
        foreach(var item in entries)
        {
            var parts=item.Path.Split('/');for(var count=1;count<parts.Length;count++)if(kinds.TryGetValue(string.Join('/',parts.Take(count)),out var directory)&&!directory)throw new TransportException("TOOL_ZIP_COLLISION","Tool ZIP file is also a parent directory.");
        }
        var artifacts=new List<Artifact>();foreach(var item in entries.Where(x=>!x.Directory))
        {
            cancellationToken.ThrowIfCancellationRequested();var data=MetadataArchiveReader.ReadMember(item.Entry,pin.Bounds.MaxDownloadBytes,pin.Bounds.MaxMemberBytes,cancellationToken);
            artifacts.Add(new(prefix+"/"+item.Path,data,"application/octet-stream"));
        }
        return artifacts;
    }
}
