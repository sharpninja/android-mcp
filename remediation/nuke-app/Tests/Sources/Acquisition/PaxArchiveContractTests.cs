using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public static class PaxArchiveCases
{
    public static byte[] Fixture(IReadOnlyDictionary<string,string>? attributes=null)
    {
        using var raw=new MemoryStream();using(var tar=new TarWriter(raw,true))
        {
            var global=new PaxGlobalExtendedAttributesTarEntry(attributes??new Dictionary<string,string>{{"comment",TransportCases.Revision}}){Name="pax_global_header"};tar.WriteEntry(global);
            var entry=new PaxTarEntry(TarEntryType.RegularFile,"repo/lib/src/a.c"){DataStream=new MemoryStream(Encoding.ASCII.GetBytes("abc"))};tar.WriteEntry(entry);
        }
        // TarWriter generates a process-specific global header name, ignoring Name.
        // Replace only the test fixture's first TAR name/checksum with the exact
        // retained GitHub header. Production never patches downloaded archives.
        var bytes=raw.ToArray();Array.Clear(bytes,0,100);Encoding.ASCII.GetBytes("pax_global_header").CopyTo(bytes,0);
        Array.Fill(bytes,(byte)' ',148,8);var checksum=bytes.AsSpan(0,512).ToArray().Sum(x=>(int)x);
        Encoding.ASCII.GetBytes(Convert.ToString(checksum,8).PadLeft(6,'0')+"\0 ").CopyTo(bytes,148);
        using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.Optimal,true))gzip.Write(bytes);return output.ToArray();
    }
    public static async Task AssertCase(INativeArchiveExtractor provider)
    {
        var fixture=Fixture();var store=new MemoryAcquisitionStore();var result=await provider.ExtractTarGZipAsync(fixture,store,"native");
        Assert.Equal(ContentHash.Sha256(fixture),result.ArchiveSha256);var file=Assert.Single(result.Files);Assert.Equal("native/lib/src/a.c",file.RelativePath);Assert.Equal(AcquisitionCases.AbcSha,file.Sha256);
        var metadata=Assert.Single(result.Metadata!);Assert.Equal("pax_global_header",metadata.Name);Assert.Equal("GlobalExtendedAttributes",metadata.EntryType);Assert.Equal(TransportCases.Revision,Assert.Single(metadata.Attributes).Value);Assert.True(metadata.Attributes.ContainsKey("comment"));
        Assert.Single(store.Files);Assert.Equal(Encoding.ASCII.GetBytes("abc"),store.Files[file.RelativePath].Bytes.ToArray());
    }
}

public sealed record PaxOverrideCase(string Key,string Value);
public interface IPaxOverrideProbe { Task ExecuteAsync(PaxOverrideCase test,byte[] fixture);int Writes{get;} }
public static class PaxOverrideCases
{
    public static IEnumerable<object[]> All=>new[]{new PaxOverrideCase("path","../../escape"),new PaxOverrideCase("path","/escape"),new PaxOverrideCase("linkpath","/etc/passwd")}.Select(x=>new object[]{x});
    public static async Task AssertCase(PaxOverrideCase test,IPaxOverrideProbe provider)
    {
        var fixture=PaxArchiveCases.Fixture(new Dictionary<string,string>{{test.Key,test.Value}});
        var error=await Record.ExceptionAsync(()=>provider.ExecuteAsync(test,fixture));Assert.IsType<TransportException>(error);Assert.Equal("ARCHIVE_METADATA_PATH",((TransportException)error!).Code);Assert.Equal(0,provider.Writes);
    }
}
public sealed class ScriptedPaxOverride(string? mutation=null):IPaxOverrideProbe
{
    public int Writes=>mutation=="write"?1:0;
    public Task ExecuteAsync(PaxOverrideCase test,byte[] fixture)
    {Assert.NotEmpty(fixture);if(mutation=="error")return Task.CompletedTask;throw new TransportException(mutation=="code"?"wrong":"ARCHIVE_METADATA_PATH","Scripted unsafe override rejection");}
}
[Trait("Gate","Mock")]
public sealed class PaxOverrideMockTests
{
    [Theory,MemberData(nameof(PaxOverrideCases.All),MemberType=typeof(PaxOverrideCases))]
    public Task CorrectUnsafeOverrideRejections(PaxOverrideCase test)=>PaxOverrideCases.AssertCase(test,new ScriptedPaxOverride());
    [Theory,MemberData(nameof(PaxOverrideCases.All),MemberType=typeof(PaxOverrideCases))]
    public async Task MissingErrorWrongCodeAndWritesRejected(PaxOverrideCase test)
    {foreach(var mutation in new[]{"error","code","write"})Assert.NotNull(await Record.ExceptionAsync(()=>PaxOverrideCases.AssertCase(test,new ScriptedPaxOverride(mutation))));}
}
public sealed class ScriptedPaxExtractor(string? mutation=null):INativeArchiveExtractor
{
    public async Task<NativeArchiveEvidence> ExtractTarGZipAsync(ReadOnlyMemory<byte> archive,IArtifactStore store,string prefix,CancellationToken cancellationToken=default)
    {
        var file=await store.WriteAsync(new("native/lib/src/a.c",Encoding.ASCII.GetBytes("abc"),"application/octet-stream"),cancellationToken);
        var metadata=new ArchiveMetadataEvidence(mutation=="name"?"wrong":"pax_global_header",mutation=="type"?"RegularFile":"GlobalExtendedAttributes",new Dictionary<string,string>{{mutation=="key"?"wrong":"comment",mutation=="value"?"wrong":TransportCases.Revision}});
        return new(mutation=="hash"?"wrong":ContentHash.Sha256(archive.Span),[file],mutation=="missing"?[]:[metadata]);
    }
}
[Trait("Gate","Mock")]
public sealed class PaxArchiveMockTests
{
    [Fact]public Task ExactGitHubGlobalPaxMetadataRetained()=>PaxArchiveCases.AssertCase(new ScriptedPaxExtractor());
    [Theory][InlineData("name")][InlineData("type")][InlineData("key")][InlineData("value")][InlineData("hash")][InlineData("missing")]
    public async Task EveryMetadataPropertyRejectedWhenWrong(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>PaxArchiveCases.AssertCase(new ScriptedPaxExtractor(mutation))));
}
