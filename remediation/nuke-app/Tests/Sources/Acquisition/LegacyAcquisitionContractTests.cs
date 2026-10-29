using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// Exact original literals and assertions from the read-only canonical and
// release011 bundled_snapshot/restore_snapshot/sdk_zip_metadata tests.
public sealed record LegacyAcquisitionCase(string Id,string? ErrorType,string? ErrorCode,IReadOnlyDictionary<string,byte[]> Files);
public sealed record LegacyAcquisitionObservation(string? ErrorType,string? ErrorCode,IReadOnlyDictionary<string,byte[]> Files,
    int Network,int Writes,bool OriginalRetained,long RangeBytes,int InventoryEntries);
public interface ILegacyAcquisitionProbe { Task<LegacyAcquisitionObservation> RunAsync(LegacyAcquisitionCase test); }
public static class LegacyAcquisitionCases
{
    // Independently measured with native BCL SHA256 and framed SHA1 over the literal.
    public const string SnapshotSha="5084ebc7deedbebc7de96191e26a5f25c1e0f6993e241700343a94f1dca3a650";
    public const string SnapshotGit="7c10311e60fb786ecf372bd26f789af4a179fdd9";
    public static readonly byte[] Snapshot=Encoding.ASCII.GetBytes("exact old HTTP response");
    public static readonly byte[] Java=Encoding.UTF8.GetBytes("package a; class C{}");
    public static readonly byte[] Xml=Encoding.UTF8.GetBytes("<api version=\"3\"/>");
    public static readonly byte[] Properties=Encoding.UTF8.GetBytes("AndroidVersion.ApiLevel=37\nAndroidVersion.ApiMinor=2\n");
    public static IEnumerable<object[]> All=>new[]{
        new LegacyAcquisitionCase("exact-snapshot-no-network",null,null,new Dictionary<string,byte[]>{{"restored/reference.html",Snapshot}}),
        new LegacyAcquisitionCase("missing-snapshot-no-fallback","FileNotFoundException",null,new Dictionary<string,byte[]>()),
        new LegacyAcquisitionCase("changed-snapshot-retained","TransportException","SOURCE_HASH",new Dictionary<string,byte[]>()),
        new LegacyAcquisitionCase("snapshot-escape","ArgumentException",null,new Dictionary<string,byte[]>()),
        new LegacyAcquisitionCase("cached-source-zip",null,null,new Dictionary<string,byte[]>{{"src/a/C.java",Java}}),
        new LegacyAcquisitionCase("tampered-source-zip-retained","TransportException","ARCHIVE_HASH",new Dictionary<string,byte[]>()),
        new LegacyAcquisitionCase("metadata-original-literals",null,null,new Dictionary<string,byte[]>{{"data/api-versions.xml",Xml},{"source.properties",Properties}})
    }.Select(x=>new object[]{x});
    public static byte[] Zip(bool metadata)
    {
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            void Add(string path,byte[] bytes){using var stream=zip.CreateEntry(path,CompressionLevel.NoCompression).Open();stream.Write(bytes);}
            if(metadata){Add("android.jar",Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("UNSELECTED_BINARY",10000))));Add("data/api-versions.xml",Xml);Add("source.properties",Properties);}
            else Add("src/a/C.java",Java);
        }
        return output.ToArray();
    }
    public static async Task AssertCase(LegacyAcquisitionCase test,ILegacyAcquisitionProbe probe)
    {
        var actual=await probe.RunAsync(test);Assert.Equal(test.ErrorType,actual.ErrorType);Assert.Equal(test.ErrorCode,actual.ErrorCode);
        Assert.Equal(0,actual.Network);Assert.Equal(test.Id=="exact-snapshot-no-network"?1:0,actual.Writes);Assert.True(actual.OriginalRetained);
        Assert.Equal(test.Files.Keys.Order(),actual.Files.Keys.Order());foreach(var item in test.Files)Assert.Equal(item.Value,actual.Files[item.Key]);
        if(test.Id=="metadata-original-literals"){Assert.Equal(3,actual.InventoryEntries);Assert.InRange(actual.RangeBytes,1,9999);Assert.DoesNotContain("android.jar",actual.Files.Keys);}
        else {Assert.Equal(0,actual.InventoryEntries);Assert.Equal(0,actual.RangeBytes);}
    }
}
public sealed class ScriptedLegacyAcquisitionProbe(string? mutation=null):ILegacyAcquisitionProbe
{
    public Task<LegacyAcquisitionObservation> RunAsync(LegacyAcquisitionCase test)
    {
        var files=test.Files.ToDictionary(x=>x.Key,x=>x.Value);
        if(mutation=="files")files["wrong"]=[0];if(mutation=="bytes"&&files.Count>0)files[files.Keys.First()]=[0];
        return Task.FromResult(new LegacyAcquisitionObservation(mutation=="type"?"wrong":test.ErrorType,mutation=="code"?"wrong":test.ErrorCode,files,
            mutation=="network"?1:0,(test.Id=="exact-snapshot-no-network"?1:0)+(mutation=="writes"?1:0),mutation!="original",
            mutation=="range"?10000:test.Id=="metadata-original-literals"?1000:0,mutation=="inventory"?-1:test.Id=="metadata-original-literals"?3:0));
    }
}
[Trait("Gate","Mock")]
public sealed class LegacyAcquisitionMockTests
{
    [Theory,MemberData(nameof(LegacyAcquisitionCases.All),MemberType=typeof(LegacyAcquisitionCases))]
    public Task CorrectOriginalFixtures(LegacyAcquisitionCase test)=>LegacyAcquisitionCases.AssertCase(test,new ScriptedLegacyAcquisitionProbe());
    [Theory,MemberData(nameof(LegacyAcquisitionCases.All),MemberType=typeof(LegacyAcquisitionCases))]
    public async Task EveryOriginalAssertionDiscriminates(LegacyAcquisitionCase test)
    {
        foreach(var mutation in (test.Files.Count>0?new[]{"type","code","files","bytes","network","writes","original","range","inventory"}:new[]{"type","code","files","network","writes","original","range","inventory"}))
            Assert.NotNull(await Record.ExceptionAsync(()=>LegacyAcquisitionCases.AssertCase(test,new ScriptedLegacyAcquisitionProbe(mutation))));
    }
}
