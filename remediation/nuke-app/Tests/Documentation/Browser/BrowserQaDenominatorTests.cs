using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal static class BrowserQaDenominatorCases
{
    internal static BrowserQaRequest Input(string root,int expected,ToolIdentity? browser=null)
    {var request=BrowserQaCases.CreateInput(root,BrowserQaCases.Get("QA-good"),browser) with{ExpectedArchitectureDiagrams=expected};File.Delete(Path.Combine(request.ProjectRoot,"docs/diagrams/fixture.svg"));return request;}
    internal static async Task AssertContract(int expected,IBrowserQaOperation provider,IArtifactStore store,BrowserQaRequest request)
    {
        var result=await provider.RunAsync(request);Assert.Equal(expected==0,result.Passed);Assert.Equal(expected==0?Array.Empty<string>():new[]{"diagram-count"},result.Failures);
        var report=JsonNode.Parse((await store.ReadAsync(result.Receipt.RelativePath)).Span)!;Assert.Equal(expected,report["expected_diagrams"]!.GetValue<int>());Assert.Equal(0,report["diagrams"]!.GetValue<int>());
    }
}
internal sealed class ScriptedQaDenominator(int expected,IArtifactStore store,string? mutation=null):IBrowserQaOperation
{
    public async Task<BrowserQaResult> RunAsync(BrowserQaRequest request,CancellationToken cancellationToken=default)
    {
        var report=CdpCases.Obj(("expected_diagrams",expected),("diagrams",mutation=="actual"?21:0));if(mutation=="missing")report.Remove("expected_diagrams");
        var receipt=await store.WriteAsync(new("browser-qa.json",JsonSerializer.SerializeToUtf8Bytes(report),"application/json"),cancellationToken);
        return new(receipt,mutation=="verdict"?expected!=0:expected==0,3,2,4,[],expected==0?[]:["diagram-count"]);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaDenominatorMockTests
{
    [Theory,InlineData(21),InlineData(0)]
    public async Task Full21AndExplicitUi0CannotBeSilentlyInterchanged(int expected)
    {
        foreach(var mutation in new string?[]{null,"verdict","actual","missing"})
        {
            var root=Path.Combine(Path.GetTempPath(),"androidmcp-qa-denominator",Guid.NewGuid().ToString("N"));var request=BrowserQaDenominatorCases.Input(root,expected);var store=new FileArtifactStore(request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
            var error=await Record.ExceptionAsync(()=>BrowserQaDenominatorCases.AssertContract(expected,new ScriptedQaDenominator(expected,store,mutation),store,request));if(mutation is null)Assert.Null(error);else Assert.NotNull(error);
        }
    }
}
