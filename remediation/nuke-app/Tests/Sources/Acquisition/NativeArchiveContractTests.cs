using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class MemoryAcquisitionStore:IArtifactStore
{
    public Dictionary<string,Artifact> Files {get;}=new(StringComparer.Ordinal);
    public int Writes {get;private set;}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Writes++;
        if(Files.TryGetValue(artifact.RelativePath,out var old)&&!old.Bytes.Span.SequenceEqual(artifact.Bytes.Span))throw new IOException("Preserve original");
        Files[artifact.RelativePath]=artifact;
        return Task.FromResult(new StoredArtifact(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType));
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)=>Files.TryGetValue(relativePath,out var value)?Task.FromResult(value.Bytes):throw new FileNotFoundException(relativePath);
}
public static class NativeArchiveCases
{
    public static byte[] Fixture(string path="repo/lib/src/a.c",bool link=false)
    {
        using var result=new MemoryStream();
        using(var gz=new GZipStream(result,CompressionLevel.SmallestSize,true))
        using(var tar=new TarWriter(gz,true))
        {
            var entry=new PaxTarEntry(link?TarEntryType.SymbolicLink:TarEntryType.RegularFile,path);
            if(link)entry.LinkName="/etc/passwd";else entry.DataStream=new MemoryStream(Encoding.ASCII.GetBytes("abc"));
            tar.WriteEntry(entry);
        }
        return result.ToArray();
    }
    public static async Task AssertCase(INativeArchiveExtractor extractor,byte[] fixture,MemoryAcquisitionStore store)
    {
        var result=await extractor.ExtractTarGZipAsync(fixture,store,"native");
        Assert.Equal(ContentHash.Sha256(fixture),result.ArchiveSha256);var file=Assert.Single(result.Files);
        Assert.Equal("native/lib/src/a.c",file.RelativePath);Assert.Equal(AcquisitionCases.AbcSha,file.Sha256);Assert.Equal(3,file.Length);
        Assert.Equal(Encoding.ASCII.GetBytes("abc"),(await store.ReadAsync(file.RelativePath)).ToArray());Assert.Single(store.Files);
    }
}
public sealed class ScriptedNativeExtractor(string? mutation=null):INativeArchiveExtractor
{
    public async Task<NativeArchiveEvidence> ExtractTarGZipAsync(ReadOnlyMemory<byte> archive,IArtifactStore store,string prefix,CancellationToken cancellationToken=default)
    {
        var artifact=await store.WriteAsync(new("native/lib/src/a.c",mutation=="bytes"?new byte[]{0}:Encoding.ASCII.GetBytes("abc"),"application/octet-stream"),cancellationToken);
        artifact=mutation switch{"path"=>artifact with{RelativePath="wrong"},"hash"=>artifact with{Sha256="wrong"},"size"=>artifact with{Length=4},_=>artifact};
        return new(mutation=="archive"?"wrong":ContentHash.Sha256(archive.Span),mutation=="missing"?[]:[artifact]);
    }
}
[Trait("Gate","Mock")]
public sealed class NativeArchiveMockTests
{
    [Fact]public Task CorrectMock()=>NativeArchiveCases.AssertCase(new ScriptedNativeExtractor(),NativeArchiveCases.Fixture(),new());
    [Theory][InlineData("path")][InlineData("hash")][InlineData("size")][InlineData("bytes")][InlineData("archive")][InlineData("missing")]
    public async Task IndividualResponseMutationsRejected(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>NativeArchiveCases.AssertCase(new ScriptedNativeExtractor(mutation),NativeArchiveCases.Fixture(),new())));
}
