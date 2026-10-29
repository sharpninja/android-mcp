using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StreamingReaderProviderRealTests
{
    [Theory,MemberData(nameof(StreamingReaderProviderCases.Cases),MemberType=typeof(StreamingReaderProviderCases))]
    public async Task SameUnconfiguredProviderCasesBindStore(string operation)
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-no-reader-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);var path=Path.Combine(root,"payload.bin");
        var bytes=StreamingReaderCases.Abc.ToArray();await File.WriteAllBytesAsync(path,bytes);
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        await StreamingReaderProviderCases.AssertCase(operation,store);
        Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
    }
}
