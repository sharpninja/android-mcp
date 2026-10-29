using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class StreamingReaderProviderCases
{
    public static IEnumerable<object[]> Cases=>new[]{new object[]{"inspect"},new object[]{"open"}};
    public static async Task AssertCase(string operation,IStreamingArtifactReader reader)
    {
        var error=await Record.ExceptionAsync(async()=>{
            if(operation=="inspect")_ =await reader.InspectAsync("payload.bin");
            else{await using var unexpected=await reader.OpenReadAsync("payload.bin");}
        });
        Assert.IsType<PlatformNotSupportedException>(error);
    }
}

public sealed class ScriptedMissingReaderProvider(string? mutation=null):IStreamingArtifactReader
{
    private Exception Error=>mutation=="wrong-error"?new IOException("wrong-error"):
        new PlatformNotSupportedException("An explicit native reader opener is required.");
    public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)=>mutation=="fallback"
        ?Task.FromResult<ArtifactReadSnapshot?>(null):Task.FromException<ArtifactReadSnapshot?>(Error);
    public Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)=>mutation=="fallback"
        ?Task.FromResult<IArtifactReadSession>(new UnexpectedSession()):Task.FromException<IArtifactReadSession>(Error);
    private sealed class UnexpectedSession:IArtifactReadSession
    {
        public Stream Content=>Stream.Null;
        public ArtifactReadSnapshot Opened {get;}=new("payload.bin",StreamingReaderCases.Identity);
        public Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken token=default)=>Task.FromResult(Opened);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StreamingReaderProviderMockTests
{
    [Theory,MemberData(nameof(StreamingReaderProviderCases.Cases),MemberType=typeof(StreamingReaderProviderCases))]
    public Task CorrectUnconfiguredProviderFailsClosed(string operation)=>StreamingReaderProviderCases.AssertCase(operation,new ScriptedMissingReaderProvider());
    [Theory,MemberData(nameof(StreamingReaderProviderCases.Cases),MemberType=typeof(StreamingReaderProviderCases))]
    public async Task ImplicitFallbackAndWrongErrorAreRejected(string operation)
    {
        foreach(var mutation in new[]{"fallback","wrong-error"})
            Assert.NotNull(await Record.ExceptionAsync(()=>StreamingReaderProviderCases.AssertCase(operation,new ScriptedMissingReaderProvider(mutation))));
    }
}
