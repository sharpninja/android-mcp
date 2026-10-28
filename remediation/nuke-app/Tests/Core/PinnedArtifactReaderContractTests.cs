using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record PinnedArtifactCase(string Id,StoredArtifact Pin,byte[] Bytes,string? Error,int Reads);
public sealed class PinnedArtifactFixture
{
    public required PinnedArtifactCase Case{get;init;}
    public CancellationTokenSource Cancellation{get;}=new();
    public List<(string Path,CancellationToken Token)> Reads{get;}=[];
    public int Writes{get;private set;}
    public IArtifactStore Store=>new FixtureStore(this);
    private sealed class FixtureStore(PinnedArtifactFixture fixture):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {
            fixture.Reads.Add((relativePath,cancellationToken));
            if(fixture.Case.Id=="missing")throw new FileNotFoundException();
            if(fixture.Case.Id=="cancel-during")fixture.Cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ReadOnlyMemory<byte>>(fixture.Case.Bytes);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {fixture.Writes++;throw new InvalidOperationException("Pinned reader attempted a write");}
    }
}
public static class PinnedArtifactReaderCases
{
    public const string AbcSha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public const string EmptySha="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    public static IEnumerable<object[]> All
    {
        get
        {
            var pin=new StoredArtifact("artifacts/input.bin",AbcSha,3,"application/octet-stream");
            return new PinnedArtifactCase[]{
                new("bytes",pin,[97,98,99],null,1),new("empty",pin with{Sha256=EmptySha,Length=0},[],null,1),
                new("not-json",pin with{MediaType="application/json"},[97,98,99],null,1),
                new("stale",pin,[100,101,102],"ARTIFACT_PIN_HASH",1),
                new("hash",pin with{Sha256=new string('0',64)},[97,98,99],"ARTIFACT_PIN_HASH",1),
                new("length",pin with{Length=4},[97,98,99],"ARTIFACT_PIN_LENGTH",1),
                new("length-before-hash",pin with{Length=4,Sha256=new string('0',64)},[97,98,99],"ARTIFACT_PIN_LENGTH",1),
                new("missing",pin,[],"FileNotFoundException",1),new("cancel-before",pin,[97,98,99],"OperationCanceledException",0),
                new("cancel-during",pin,[97,98,99],"OperationCanceledException",1)
            }.Select(test=>new object[]{test});
        }
    }
    public static async Task AssertCase(PinnedArtifactCase test,Func<PinnedArtifactFixture,IPinnedArtifactReader> factory)
    {
        var fixture=new PinnedArtifactFixture{Case=test};using var cancellation=fixture.Cancellation;if(test.Id=="cancel-before")cancellation.Cancel();
        var reader=factory(fixture);ReadOnlyMemory<byte> result=default;
        var error=await Record.ExceptionAsync(async()=>{result=await reader.ReadAsync(test.Pin,cancellation.Token);});
        Assert.Equal(test.Reads,fixture.Reads.Count);foreach(var read in fixture.Reads){Assert.Equal(test.Pin.RelativePath,read.Path);Assert.Equal(cancellation.Token,read.Token);}
        Assert.Equal(0,fixture.Writes);
        if(test.Error is null){Assert.Null(error);Assert.Equal(test.Bytes,result.ToArray());}
        else if(test.Error=="FileNotFoundException")Assert.IsType<FileNotFoundException>(error);
        else if(test.Error=="OperationCanceledException")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else Assert.Equal(test.Error,Assert.IsType<PinnedArtifactException>(error).Code);
    }
}
public sealed class ScriptedPinnedArtifactReader(PinnedArtifactFixture fixture,string? mutation=null):IPinnedArtifactReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        if(fixture.Case.Id=="cancel-before"&&mutation!="cancel")cancellationToken.ThrowIfCancellationRequested();
        ReadOnlyMemory<byte> bytes=fixture.Case.Bytes;
        if(mutation!="no-read"&&fixture.Case.Id!="cancel-before")
        {
            bytes=await fixture.Store.ReadAsync(mutation=="path"?"wrong":callerPin.RelativePath,mutation=="token"?CancellationToken.None:cancellationToken);
            if(mutation=="double-read")_=await fixture.Store.ReadAsync(callerPin.RelativePath,cancellationToken);
        }
        if(mutation=="write")_=await fixture.Store.WriteAsync(new(callerPin.RelativePath,bytes,callerPin.MediaType),cancellationToken);
        if(fixture.Case.Error?.StartsWith("ARTIFACT_PIN_",StringComparison.Ordinal)==true&&mutation!="omit-error")throw new PinnedArtifactException(mutation=="code"?"wrong":fixture.Case.Error);
        if(mutation=="error-added")throw new IOException();
        return mutation=="bytes"?new byte[]{0}:bytes;
    }
}
[Trait("Gate","Mock")]
public sealed class PinnedArtifactReaderMockTests
{
    [Theory,MemberData(nameof(PinnedArtifactReaderCases.All),MemberType=typeof(PinnedArtifactReaderCases))]
    public Task CorrectOneReadExactPinsAndByteOnlyAdmission(PinnedArtifactCase test)=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new ScriptedPinnedArtifactReader(fixture));
    [Theory,MemberData(nameof(PinnedArtifactReaderCases.All),MemberType=typeof(PinnedArtifactReaderCases))]
    public async Task WrongReadPinPayloadCancellationAndEffectsRejected(PinnedArtifactCase test)
    {
        var mutations=test.Id=="cancel-before"?new[]{"cancel"}:test.Id is "missing" or "cancel-during"?new[]{"no-read","path","token"}:
            test.Error is null?new[]{"no-read","double-read","path","token","write","error-added","bytes"}:new[]{"no-read","double-read","path","token","write","omit-error","code"};
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new ScriptedPinnedArtifactReader(fixture,mutation))));
    }
}
