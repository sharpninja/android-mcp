using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpRealTests
{
    [Theory,MemberData(nameof(CdpCases.Cases),MemberType=typeof(CdpCases))]
    public async Task IdenticalProtocolCasesBindToRealCorrelationAndRetainCompleteWireBytes(string id)
    {
        var test=CdpCases.Get(id);var root=ProcessSessionFixturePaths.NewRoot("cdp-protocol-tests");var store=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        await using var wire=new ScriptedCdpWire(test);await using var client=new CdpClient(wire,store,new SystemClock());await CdpCases.AssertContract(test,client,wire);
        var records=new List<JsonObject>();foreach(var artifact in client.Transcript)records.Add(JsonNode.Parse((await store.ReadAsync(artifact.RelativePath)).Span)!.AsObject());
        var sent=records.Where(x=>x["direction"]!.GetValue<string>()=="sent").OrderBy(x=>x["sequence"]!.GetValue<long>()).Select(x=>Convert.FromBase64String(x["body_base64"]!.GetValue<string>())).ToArray();
        var received=records.Where(x=>x["direction"]!.GetValue<string>()=="received").OrderBy(x=>x["sequence"]!.GetValue<long>()).Select(x=>Convert.FromBase64String(x["body_base64"]!.GetValue<string>())).ToArray();
        Assert.Equal(wire.Sent.Count,sent.Length);for(var i=0;i<sent.Length;i++)Assert.Equal(wire.Sent[i],sent[i]);
        var expected=wire.Received.Where(x=>x is not null).ToArray();Assert.Equal(expected.Length,received.Length);for(var i=0;i<received.Length;i++)Assert.Equal(expected[i],received[i]);
    }
}
