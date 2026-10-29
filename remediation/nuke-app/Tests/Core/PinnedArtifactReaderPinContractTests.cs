using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class PinnedArtifactMetadataCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            var pin=new StoredArtifact("artifacts/input.bin",PinnedArtifactReaderCases.AbcSha,3,"application/octet-stream");
            return new PinnedArtifactCase[]{
                new("null-pin",null!,[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("negative-length",pin with{Length=-1},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("empty-digest",pin with{Sha256=""},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("null-digest",pin with{Sha256=null!},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("short-digest",pin with{Sha256=new string('a',63)},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("long-digest",pin with{Sha256=new string('a',65)},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("non-hex-digest",pin with{Sha256=new string('g',64)},[97,98,99],"ARTIFACT_PIN_METADATA",0),
                new("non-canonical-digest",pin with{Sha256=pin.Sha256.ToUpperInvariant()},[97,98,99],"ARTIFACT_PIN_METADATA",0)
            }.Select(test=>new object[]{test});
        }
    }
}
public sealed class ScriptedMalformedPinnedReader(PinnedArtifactFixture fixture,string? mutation=null):IPinnedArtifactReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        if(mutation=="read")_=await fixture.Store.ReadAsync("artifacts/input.bin",cancellationToken);
        if(mutation=="write")_=await fixture.Store.WriteAsync(new("artifacts/input.bin",fixture.Case.Bytes,"application/octet-stream"),cancellationToken);
        if(mutation=="omit-error")return fixture.Case.Bytes;
        throw new PinnedArtifactException(mutation=="code"?"wrong":"ARTIFACT_PIN_METADATA");
    }
}
[Trait("Gate","Mock")]
public sealed class PinnedArtifactReaderPinMockTests
{
    [Theory,MemberData(nameof(PinnedArtifactMetadataCases.All),MemberType=typeof(PinnedArtifactMetadataCases))]
    public Task MalformedCallerPinsFailBeforeStoreIo(PinnedArtifactCase test)=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new ScriptedMalformedPinnedReader(fixture));
    [Theory,MemberData(nameof(PinnedArtifactMetadataCases.All),MemberType=typeof(PinnedArtifactMetadataCases))]
    public async Task IgnoredMalformedPinsReadsWritesAndWrongDiagnosticRejected(PinnedArtifactCase test)
    {
        foreach(var mutation in new[]{"read","write","omit-error","code"})
            Assert.NotNull(await Record.ExceptionAsync(()=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new ScriptedMalformedPinnedReader(fixture,mutation))));
    }
}
