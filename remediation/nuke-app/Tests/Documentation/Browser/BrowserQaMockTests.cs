using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaMockTests
{
    public static IEnumerable<object[]> Cases=>BrowserQaCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task CorrectQaReportsAndCapturesRetainEveryRequiredAssertion(string id)
    {
        var test=BrowserQaCases.Get(id);var root=Path.Combine(Path.GetTempPath(),"androidmcp-browser-qa-mock",Guid.NewGuid().ToString("N"));var request=BrowserQaCases.CreateInput(root,test);var store=new FileArtifactStore(request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var provider=new ScriptedBrowserQa(test,store);await BrowserQaCases.AssertContract(test,provider,store,request);Assert.Same(request,provider.Received);
    }
    [Theory,MemberData(nameof(Cases))]
    public async Task IndependentQaVerdictDenominatorReportAndCaptureControlsAreRejected(string id)
    {
        var test=BrowserQaCases.Get(id);
        foreach(var mutation in new[]{"verdict","slides","images","svg","receipt-field","navigation","image-bytes","capture-omission","failure-list","input-mutation","launch","shutdown"})
        {
            var root=Path.Combine(Path.GetTempPath(),"androidmcp-browser-qa-negative",Guid.NewGuid().ToString("N"));var request=BrowserQaCases.CreateInput(root,test);var store=new FileArtifactStore(request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
            Assert.NotNull(await Record.ExceptionAsync(()=>BrowserQaCases.AssertContract(test,new ScriptedBrowserQa(test,store,mutation),store,request)));
        }
    }
}
