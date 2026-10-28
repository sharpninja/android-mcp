using System.Text;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public interface ICacheWireProbe { Task<byte[]> ReadDecodedPayloadAsync(); }
public static class CacheWireCases
{
    public static readonly byte[] Expected=Encoding.UTF8.GetBytes("{\"value\":1}\n");
    public static async Task AssertCase(ICacheWireProbe provider)=>Assert.Equal(Expected,await provider.ReadDecodedPayloadAsync());
}
public sealed class ScriptedCacheWire(string? mutation=null):ICacheWireProbe
{
    public Task<byte[]> ReadDecodedPayloadAsync()=>Task.FromResult(Encoding.UTF8.GetBytes(mutation switch{"array"=>"[{\"value\":1}]","newline"=>"{\"value\":1}","value"=>"{\"value\":2}\n","space"=>"{\"value\": 1}\n",_=>"{\"value\":1}\n"}));
}
[Trait("Gate","Mock")]
public sealed class CacheWireMockTests
{
    [Fact]public Task CorrectCanonicalJsonLines()=>CacheWireCases.AssertCase(new ScriptedCacheWire());
    [Theory][InlineData("array")][InlineData("newline")][InlineData("value")][InlineData("space")]
    public async Task WireMutationsRejected(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>CacheWireCases.AssertCase(new ScriptedCacheWire(mutation))));
}
