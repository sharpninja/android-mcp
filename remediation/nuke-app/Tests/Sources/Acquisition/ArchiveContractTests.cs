using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class FixtureRangeSource(byte[] bytes):IRangeSource
{
    public long Length=>bytes.Length;
    public List<(long Offset,int Count)> Reads {get;}=[];
    public byte[] Read(long offset,int count){Reads.Add((offset,count));return bytes.AsSpan(checked((int)offset),count).ToArray();}
}
public static class ArchiveCases
{
    public static byte[] ZipFixture()
    {
        using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
        foreach(var (path,bytes) in new[]{("android.jar",Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("UNSELECTED_BINARY",10000)))),("data/api-versions.xml",Encoding.ASCII.GetBytes("abc")),("source.properties",Encoding.ASCII.GetBytes("abc"))})
        {using var output=zip.CreateEntry(path,CompressionLevel.NoCompression).Open();output.Write(bytes);}
        return stream.ToArray();
    }
    public static async Task AssertCase(IMetadataArchiveReader provider,FixtureRangeSource source)
    {
        var result=provider.Read(source);Assert.Equal(3,result.Entries.Count);Assert.Equal(2,result.Files.Count);
        Assert.Equal(new[]{"android.jar","data/api-versions.xml","source.properties"},result.Entries.Select(x=>x.Path));
        Assert.False(result.Entries[0].Selected);Assert.Equal(170000,result.Entries[0].SizeBytes);
        foreach(var entry in result.Entries.Skip(1)){Assert.True(entry.Selected);Assert.Equal(3,entry.SizeBytes);Assert.Equal(3,entry.CompressedBytes);Assert.Equal("352441c2",entry.Crc32);Assert.Equal(Encoding.ASCII.GetBytes("abc"),result.Files[entry.Path]);}
        Assert.False(result.Files.ContainsKey("android.jar"));Assert.Null(result.WholeArchiveSha256);Assert.False(result.CompleteArchiveIdentity);
        Assert.NotEmpty(source.Reads);Assert.All(source.Reads,x=>Assert.InRange(x.Count,0,8*1024*1024));
        Assert.True(source.Reads.Sum(x=>(long)x.Count)<10000); // no unselected payload read
        await Task.CompletedTask;
    }
}
public sealed class ScriptedMetadataReader(string? mutation=null):IMetadataArchiveReader
{
    public MetadataArchiveEvidence Read(IRangeSource source)
    {
        source.Read(0,mutation=="reads"?10000:1);
        var entries=new[]{new ArchiveEntryEvidence("android.jar",170000,170000,"00000000",false),new ArchiveEntryEvidence("data/api-versions.xml",3,3,"352441c2",true),new ArchiveEntryEvidence("source.properties",3,3,"352441c2",true)};
        if(mutation=="path")entries[1]=entries[1] with{Path="wrong"};
        if(mutation=="size")entries[1]=entries[1] with{SizeBytes=4};
        if(mutation=="compressed")entries[1]=entries[1] with{CompressedBytes=4};
        if(mutation=="crc")entries[1]=entries[1] with{Crc32="wrong"};
        if(mutation=="selected")entries[1]=entries[1] with{Selected=false};
        var files=new Dictionary<string,byte[]>{{"data/api-versions.xml",Encoding.ASCII.GetBytes("abc")},{"source.properties",Encoding.ASCII.GetBytes("abc")}};
        if(mutation=="bytes")files["source.properties"]=[0];if(mutation=="binary")files["android.jar"]=[0];
        return new(files,mutation=="entries"?entries[..2]:entries,mutation=="hash"?"invented":null,mutation=="complete");
    }
}
[Trait("Gate","Mock")]
public sealed class ArchiveMockTests
{
    [Fact]public Task CorrectInventorySelectionAndBoundedInteractions()=>ArchiveCases.AssertCase(new ScriptedMetadataReader(),new(ArchiveCases.ZipFixture()));
    [Theory][InlineData("path")][InlineData("size")][InlineData("compressed")][InlineData("crc")][InlineData("selected")][InlineData("bytes")][InlineData("binary")][InlineData("entries")][InlineData("hash")][InlineData("complete")][InlineData("reads")]
    public async Task EveryPropertyAndInteractionRejectsWrongResponse(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>ArchiveCases.AssertCase(new ScriptedMetadataReader(mutation),new(ArchiveCases.ZipFixture()))));
}
