using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed class RepositoryCallerEnvironment
{
    public string Scenario{get;}
    public List<string> Trace{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public IOException Failure{get;}=new("original malformed/read failure");
    public StringWriter Output{get;}=new();
    public RepositoryDocumentationCheckResult Result=>new(2,3,4,5,Scenario=="ERRORS"?["Missing link in docs/one.md: lost","Remote presentation asset: img: //remote"]:[]);
    public RepositoryCallerEnvironment(string scenario){Scenario=scenario;if(scenario=="CANCELLED")Cancellation.Cancel();}
    public IRepositoryDocumentationCheck Checker=>new Recording(this);
    private sealed class Recording(RepositoryCallerEnvironment e):IRepositoryDocumentationCheck
    {
        public Task<RepositoryDocumentationCheckResult> CheckAsync(CancellationToken cancellationToken=default)
        {Assert.Equal(e.Cancellation.Token,cancellationToken);e.Trace.Add("check");cancellationToken.ThrowIfCancellationRequested();if(e.Scenario=="THROW")throw e.Failure;return Task.FromResult(e.Result);}
    }
}
public static class RepositoryCallerCases
{
    public static IEnumerable<object[]> Cases=>new[]{"GOOD","ERRORS","THROW","CANCELLED"}.Select(value=>new object[]{value});
    public static async Task AssertContract(RepositoryCallerEnvironment e,Func<TextWriter,CancellationToken,Task<int>> run)
    {
        int? exit=null;var failure=await Record.ExceptionAsync(async()=>exit=await run(e.Output,e.Cancellation.Token));
        Assert.Equal(new[]{"check"},e.Trace);
        if(e.Scenario is "THROW" or "CANCELLED"){
            if(e.Scenario=="THROW")Assert.Same(e.Failure,failure);else Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Null(exit);Assert.Equal("",e.Output.ToString());return;}
        Assert.Null(failure);Assert.Equal(e.Scenario=="ERRORS"?1:0,exit);
        var text=e.Output.ToString();Assert.EndsWith(Environment.NewLine,text,StringComparison.Ordinal);Assert.Contains("\n  \"checksums_checked\": 2",text,StringComparison.Ordinal);
        var json=JsonNode.Parse(text)!.AsObject();Assert.Equal(new[]{"checksums_checked","markdown_links_checked","rendered_svg_count","editable_ui_copies","errors"},json.Select(value=>value.Key));
        Assert.Equal(2,json["checksums_checked"]!.GetValue<int>());Assert.Equal(3,json["markdown_links_checked"]!.GetValue<int>());Assert.Equal(4,json["rendered_svg_count"]!.GetValue<int>());Assert.Equal(5,json["editable_ui_copies"]!.GetValue<int>());
        Assert.Equal(e.Result.Errors,json["errors"]!.AsArray().Select(value=>value!.GetValue<string>()));
    }
    public static async Task<int> Mock(RepositoryCallerEnvironment e,TextWriter writer,CancellationToken token,string mutation="")
    {
        RepositoryDocumentationCheckResult result;
        try{result=await e.Checker.CheckAsync(mutation=="token"?CancellationToken.None:token);}
        catch(Exception) when(mutation=="swallow"){return 0;}
        if(mutation=="duplicate")await e.Checker.CheckAsync(token);
        var json=new JsonObject{["checksums_checked"]=2,["markdown_links_checked"]=3,["rendered_svg_count"]=4,["editable_ui_copies"]=5,["errors"]=new JsonArray(result.Errors.Select(value=>JsonValue.Create(value)).ToArray())};
        if(mutation=="field-set")json["ExitCode"]=0;if(mutation=="errors")json["errors"]=new JsonArray();
        if(new[]{"checksums_checked","markdown_links_checked","rendered_svg_count","editable_ui_copies"}.Contains(mutation,StringComparer.Ordinal))json[mutation]=0;
        var text=json.ToJsonString(new(){WriteIndented=mutation!="indent"});if(mutation!="missing-output")await writer.WriteAsync(text+(mutation=="newline"?"":Environment.NewLine));
        return mutation=="exit"?1-result.ExitCode:result.ExitCode;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationCallerMockTests
{
    [Theory,MemberData(nameof(RepositoryCallerCases.Cases),MemberType=typeof(RepositoryCallerCases))]
    public Task OriginalMainJsonExitAndFailureEffectsAreAsserted(string scenario){var e=new RepositoryCallerEnvironment(scenario);return RepositoryCallerCases.AssertContract(e,(writer,token)=>RepositoryCallerCases.Mock(e,writer,token));}
    [Theory,InlineData("exit"),InlineData("duplicate"),InlineData("token"),InlineData("missing-output"),InlineData("field-set"),InlineData("errors"),InlineData("indent"),InlineData("newline")]
    [InlineData("checksums_checked"),InlineData("markdown_links_checked"),InlineData("rendered_svg_count"),InlineData("editable_ui_copies")]
    public async Task EachPrintedFieldExitAndCallEffectRejectsWrongMock(string mutation)
    {var e=new RepositoryCallerEnvironment("ERRORS");Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryCallerCases.AssertContract(e,(writer,token)=>RepositoryCallerCases.Mock(e,writer,token,mutation))));}
    [Theory,InlineData("THROW"),InlineData("CANCELLED")]
    public async Task FailureCannotBePrintedAsSuccess(string scenario)
    {var e=new RepositoryCallerEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryCallerCases.AssertContract(e,(writer,token)=>RepositoryCallerCases.Mock(e,writer,token,"swallow"))));}
}
