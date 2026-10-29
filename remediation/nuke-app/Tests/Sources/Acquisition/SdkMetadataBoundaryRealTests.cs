using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed class NativeMetadataRangeBoundary:IMetadataRangeBoundaryProbe
{
    public MetadataRangeObservation Run(MetadataRangeCase c)
    {
        var bytes=SdkMetadataRangeCases.Data(c.Length);var before=ContentHash.Sha256(bytes);var source=new RangeSource(bytes,c.Mode);using var stream=MetadataArchiveReader.OpenRangeStream(source);
        var chunks=new List<string>();var errors=new List<string>();var original=true;
        foreach(var action in c.Actions)
        {
            try
            {
                if(action.Kind=="seek")stream.Seek(action.Value,(SeekOrigin)action.Origin);
                else
                {
                    // The old read(-1) call maps to a remaining-length buffer;
                    // the existing native stream still enforces its8MiB bound.
                    var count=action.Value<0?(int)Math.Min(Math.Max(0,stream.Length-stream.Position),SdkMetadataRangeCases.Limit+1L):action.Value;
                    var buffer=new byte[count];var read=stream.Read(buffer,0,count);chunks.Add(SdkMetadataRangeCases.Chunk(buffer.AsSpan(0,read)));
                }
            }
            catch(Exception error){errors.Add(error is TransportException typed?typed.Code:error is IOException?"IO":error.GetType().Name);if(c.Mode=="io")original&=ReferenceEquals(source.Error,error);if(!action.Continue)break;}
        }
        return new(stream.Position,stream.Length,stream.CanRead,stream.CanSeek,stream.CanWrite,chunks.ToArray(),source.Reads.ToArray(),errors.ToArray(),before==ContentHash.Sha256(bytes),original);
    }
    private sealed class RangeSource(byte[] bytes,string mode):IRangeSource
    {
        public long Length=>bytes.Length;public List<ByteRange> Reads{get;}=[];public IOException Error{get;}=new("Original callback fixture failure.");
        public byte[] Read(long offset,int count){Reads.Add(new(offset,count));if(mode=="io")throw Error;return bytes.AsSpan((int)offset,mode=="short"?Math.Max(0,count-1):count).ToArray();}
    }
}
public sealed class NativeMetadataLeaf:IMetadataLeafProbe{public bool Selected(string path)=>MetadataArchiveReader.IsSelected(path);}
public sealed class NativeMetadataArchiveBoundary:IMetadataArchiveBoundaryProbe
{
    public MetadataArchiveBoundaryObservation Run(MetadataArchiveBoundaryCase c)
    {
        var (bytes,binaryStart,binaryLength)=Fixture(c);var hash=ContentHash.Sha256(bytes);var source=new RangeSource(bytes,c.Shape=="short");MetadataArchiveEvidence? result=null;string? error=null;
        try{result=new MetadataArchiveReader().Read(source);}
        catch(Exception problem){var chain=new List<Exception>();for(var current=problem;current is not null;current=current.InnerException)chain.Add(current);error=chain.OfType<TransportException>().FirstOrDefault()?.Code??(chain.Any(p=>p is ArgumentException)?"PATH":problem.GetType().Name);}
        var reads=source.Reads;var bounds=reads.All(p=>p.Offset>=0&&p.Length>=0&&p.Length<=SdkMetadataRangeCases.Limit&&p.Offset+p.Length<=bytes.Length);
        if(c.Shape=="all")bounds&=reads.Sum(p=>(long)p.Length)<10000;
        var touched=binaryLength>0&&reads.Any(p=>p.Length>0&&p.Offset<binaryStart+binaryLength&&p.Offset+p.Length>binaryStart);
        return new(error,result?.Files.Keys.ToArray()??[],result?.Files.Select(p=>p.Key+":"+SdkMetadataRangeCases.Chunk(p.Value)).ToArray()??[],
            result?.Entries.Select(p=>$"{p.Path}:{p.SizeBytes}:{p.CompressedBytes}:{p.Selected}").ToArray()??[],hash==ContentHash.Sha256(bytes),bounds,touched,result?.CompleteArchiveIdentity==true||result?.WholeArchiveSha256 is not null,
            $"Binary={binaryStart}+{binaryLength}; reads="+string.Join(",",reads.Select(p=>$"{p.Offset}+{p.Length}")));
    }
    private static (byte[] Bytes,long BinaryStart,int BinaryLength) Fixture(MetadataArchiveBoundaryCase c)
    {
        using var stream=new MemoryStream();long binaryStart=0;var binaryLength=0;
        using(var archive=new ZipArchive(stream,ZipArchiveMode.Create,true))
        {
            void Add(string path,byte[] bytes,bool binary=false)
            {
                var entry=archive.CreateEntry(path,CompressionLevel.NoCompression);entry.LastWriteTime=new(2000,1,1,0,0,0,TimeSpan.Zero);using var output=entry.Open();
                if(binary){binaryLength=bytes.Length;output.WriteByte(bytes[0]);binaryStart=stream.Position-1;output.Write(bytes.AsSpan(1));}else output.Write(bytes);
            }
            if(c.Shape=="all"){
                Add("android.jar",Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("UNSELECTED_BINARY",10000))),true);
                foreach(var leaf in SdkMetadataLeafCases.Leaves)Add("nested/"+leaf,"abc"u8.ToArray());Add("directory/",[]);
            }
            else if(c.Shape=="directory")Add("folder/source.properties/",[]);
            else if(c.Shape!="empty"){
                Add(c.Shape=="unsafe-selected"?"../api-versions.xml":c.Shape=="unsafe-unselected"?"../android.jar":"data/api-versions.xml",c.Shape=="zero"?[]:"abc"u8.ToArray());
                if(c.Shape=="duplicate")Add("data/api-versions.xml","abc"u8.ToArray());
            }
        }
        var data=stream.ToArray();
        if(binaryLength>0)Assert.Equal((byte)'U',data[binaryStart]);
        if(c.Shape is "crc" or "uncompressed" or "compressed"){
            // Known fixture mutation, not a ZIP parser: only one central entry.
            var offset=data.AsSpan().IndexOf(new byte[]{0x50,0x4b,0x01,0x02});Assert.True(offset>=0);
            var field=c.Shape=="crc"?16:c.Shape=="compressed"?20:24;
            var value=c.Shape=="crc"?1u:c.Shape=="compressed"?8u*1024*1024+1:32u*1024*1024+1;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset+field,4),value);
        }
        return(data,binaryStart,binaryLength);
    }
    private sealed class RangeSource(byte[] bytes,bool shortRead):IRangeSource
    {
        public long Length=>bytes.Length;public List<ByteRange> Reads{get;}=[];
        public byte[] Read(long offset,int count){Reads.Add(new(offset,count));return bytes.AsSpan((int)offset,shortRead?Math.Max(0,count-1):count).ToArray();}
    }
}
[Trait("Gate","Real")]
public sealed class SdkMetadataBoundaryRealTests
{
    [Theory,MemberData(nameof(SdkMetadataRangeCases.Cases),MemberType=typeof(SdkMetadataRangeCases))]public void SameRangeMethodsUseExistingStream(MetadataRangeCase c)=>SdkMetadataRangeCases.AssertCase(c,new NativeMetadataRangeBoundary());
    [Theory,MemberData(nameof(SdkMetadataLeafCases.Cases),MemberType=typeof(SdkMetadataLeafCases))]public void SameLeafCasesUseExistingSelection(MetadataLeafCase c)=>SdkMetadataLeafCases.AssertCase(c,new NativeMetadataLeaf());
    [Theory,MemberData(nameof(SdkMetadataArchiveCases.Cases),MemberType=typeof(SdkMetadataArchiveCases))]public void SameArchiveCasesUseExistingReader(MetadataArchiveBoundaryCase c)=>SdkMetadataArchiveCases.AssertCase(c,new NativeMetadataArchiveBoundary());
}
