using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ArtifactCase(string Id,string Path,byte[] Bytes,string? Error);
public static class ArtifactStoreCases
{
    public const string AbcSha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public const string EmptySha="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    public static IEnumerable<object[]> Cases => new [] {
        new ArtifactCase("ART-01","docs/result.txt",Encoding.UTF8.GetBytes("abc"),null),
        new ArtifactCase("ART-02","empty.bin",[],null),
        new ArtifactCase("ART-03","../escape",[],"ArgumentException"),
        new ArtifactCase("ART-04","/absolute",[],"ArgumentException"),
        new ArtifactCase("ART-05","a/../escape",[],"ArgumentException"),
        new ArtifactCase("ART-06","a\\escape",[],"ArgumentException"),
        new ArtifactCase("ART-07","C:/drive",[],"ArgumentException"),
        new ArtifactCase("ART-08","",[],"ArgumentException"),
        new ArtifactCase("ART-09","a//b",[],"ArgumentException")
    }.Select(x=>new object[]{x});
    public static async Task AssertContract(ArtifactCase test,IArtifactStore store)
    {
        var input=new Artifact(test.Path,test.Bytes,"application/octet-stream");
        if(test.Error is not null)
        {
            var error=await Record.ExceptionAsync(()=>store.WriteAsync(input));
            Assert.NotNull(error); Assert.Equal(test.Error,error!.GetType().Name); return;
        }
        var result=await store.WriteAsync(input);
        Assert.Equal(test.Path,result.RelativePath);
        Assert.Equal(test.Bytes.Length,result.Length);
        Assert.Equal(input.MediaType,result.MediaType);
        Assert.Equal(test.Bytes.Length==0?EmptySha:AbcSha,result.Sha256);
        Assert.Equal(test.Bytes,(await store.ReadAsync(test.Path)).ToArray());
        Assert.Equal(result,await store.WriteAsync(input)); // identical retry is idempotent
        var conflict=await Record.ExceptionAsync(()=>store.WriteAsync(input with {Bytes=Encoding.UTF8.GetBytes("different")}));
        Assert.IsType<IOException>(conflict);
        Assert.Equal(test.Bytes,(await store.ReadAsync(test.Path)).ToArray()); // retained original
    }
}

public sealed class ScriptedArtifactStore(ArtifactCase fixture,string? mutation=null) : IArtifactStore
{
    private int writes;
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        if(fixture.Error is not null && mutation!="error") throw new ArgumentException("Invalid portable artifact path.");
        writes++;
        if(writes>2 && mutation!="conflict") throw new IOException("Retained original conflicts.");
        return Task.FromResult(new StoredArtifact(mutation=="path"?"wrong":fixture.Path,
            mutation=="hash"?"wrong":fixture.Bytes.Length==0?ArtifactStoreCases.EmptySha:ArtifactStoreCases.AbcSha,
            mutation=="length"?-1:fixture.Bytes.Length,
            mutation=="media"?"wrong":"application/octet-stream"));
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default) =>
        Task.FromResult<ReadOnlyMemory<byte>>(mutation=="bytes" ? new byte[]{255}:fixture.Bytes);
}
[Trait("Gate","Mock")]
public sealed class ArtifactStoreMockTests
{
    [Theory,MemberData(nameof(ArtifactStoreCases.Cases),MemberType=typeof(ArtifactStoreCases))]
    public Task CorrectMock(ArtifactCase test)=>ArtifactStoreCases.AssertContract(test,new ScriptedArtifactStore(test));
    [Theory,MemberData(nameof(ArtifactStoreCases.Cases),MemberType=typeof(ArtifactStoreCases))]
    public async Task WrongIndividualResponsesRejected(ArtifactCase test)
    {
        var mutations=test.Error is null ? new[]{"path","hash","length","media","bytes","conflict"} : new[]{"error"};
        foreach(var mutation in mutations)
            Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactStoreCases.AssertContract(test,new ScriptedArtifactStore(test,mutation))));
    }
}
