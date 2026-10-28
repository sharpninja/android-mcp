using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaCliMockTests
{
    public static IEnumerable<object[]> Cases=>BrowserQaCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task RegisteredCliRetainsSameLegacyQaAssertions(string id)
    {
        var test=BrowserQaCases.Get(id);var call=BrowserQaCliCases.Input(Path.Combine(Path.GetTempPath(),"androidmcp-browser-cli",Guid.NewGuid().ToString("N")),test);
        var output=new FileArtifactStore(call.Request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        await BrowserQaCliCases.AssertContract(test,new ScriptedBrowserQaCli(test,output),call,output);
    }
    [Theory,MemberData(nameof(Cases))]
    public async Task EveryCliTransportArgumentAndExistingQaMutationRejected(string id)
    {
        var test=BrowserQaCases.Get(id);
        foreach(var wrong in Enumerable.Range(0,26))
        {
            var call=BrowserQaCliCases.Input(Path.Combine(Path.GetTempPath(),"androidmcp-browser-cli-negative",Guid.NewGuid().ToString("N")),test);
            var output=new FileArtifactStore(call.Request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
            Assert.NotNull(await Record.ExceptionAsync(()=>BrowserQaCliCases.AssertContract(test,new ScriptedBrowserQaCli(test,output,wrong),call,output)));
        }
        foreach(var mutation in new[]{"receipt-field","navigation","launch","shutdown","image-bytes","input-mutation","verdict","slides","images","svg","capture-omission","failure-list"})
        {
            var call=BrowserQaCliCases.Input(Path.Combine(Path.GetTempPath(),"androidmcp-browser-cli-qa-negative",Guid.NewGuid().ToString("N")),test);
            var output=new FileArtifactStore(call.Request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
            Assert.NotNull(await Record.ExceptionAsync(()=>BrowserQaCliCases.AssertContract(test,new ScriptedBrowserQaCli(test,output,wrongQa:mutation),call,output)));
        }
    }
}
