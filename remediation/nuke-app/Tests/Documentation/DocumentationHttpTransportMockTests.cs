using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationHttpObservation(string? Error,bool CallerCancellation,string Body,IReadOnlyList<string> UserAgents,
    int Sends,IReadOnlyList<TimeSpan> Budgets,int ActiveTimers,TimeSpan Elapsed);
public static class DocumentationHttpCases
{
    public const string Agent="Android-MCP-local-documentation-build";
    private static readonly string[] OriginalNames=["DEFAULT","DEFAULT-CUSTOM","GOOD","PROFILE-CUSTOM","PER-READ","TIMEOUT-SEND","TIMEOUT-STREAM","TIMEOUT-READ","CANCEL-SEND","CANCEL-READ","NO-RETRY","HASH","BOUND","UNSUPPORTED"];
    public static readonly string[] Names=OriginalNames.Concat(new[]{"RESTORE-","FETCH-"}.SelectMany(prefix=>OriginalNames.Where(id=>id is not("DEFAULT" or "DEFAULT-CUSTOM" or "UNSUPPORTED")).Select(id=>prefix+id))).ToArray();
    public static bool Source(string id)=>id.StartsWith("RESTORE-",StringComparison.Ordinal)||id.StartsWith("FETCH-",StringComparison.Ordinal);
    public static string Scenario(string id)=>Source(id)?id[(id.StartsWith("FETCH-",StringComparison.Ordinal)?6:8)..]:id;
    public static TimeSpan Budget(string id)=>TimeSpan.FromSeconds(id.StartsWith("FETCH-",StringComparison.Ordinal)?45:60);
    public static DocumentationHttpObservation Correct(string id)
    {
        var scenario=Scenario(id);var budget=Budget(id);
        var error=scenario switch{"TIMEOUT-SEND" or "TIMEOUT-STREAM" or "TIMEOUT-READ"=>"TIMEOUT","CANCEL-SEND" or "CANCEL-READ"=>"cancelled","NO-RETRY"=>"HTTP_STATUS","HASH"=>"BODY_HASH","BOUND"=>"BODY_LIMIT","UNSUPPORTED"=>"profile",_=>null};
        var count=scenario switch{"DEFAULT" or "DEFAULT-CUSTOM" or "UNSUPPORTED"=>0,"TIMEOUT-SEND" or "CANCEL-SEND" or "NO-RETRY"=>1,"TIMEOUT-STREAM"=>2,"TIMEOUT-READ" or "CANCEL-READ" or "BOUND"=>3,"PER-READ"=>6,_=>4};
        var agent=Source(id)?scenario.EndsWith("CUSTOM",StringComparison.Ordinal)?"Custom-Agent":"":scenario=="DEFAULT"?"":scenario=="DEFAULT-CUSTOM"?"Custom-Agent":Agent;
        return new(error,scenario.StartsWith("CANCEL-",StringComparison.Ordinal),error is null?"abc":"",scenario=="UNSUPPORTED"?[]:[agent],scenario=="UNSUPPORTED"?0:1,
            Enumerable.Repeat(budget,count).ToArray(),0,scenario=="PER-READ"?TimeSpan.FromSeconds(budget.TotalSeconds*5-30):scenario.StartsWith("TIMEOUT-",StringComparison.Ordinal)?budget:TimeSpan.Zero);
    }
    public static async Task AssertContract(string id,Func<string,Task<DocumentationHttpObservation>> probe)
    {
        var expected=Correct(id);var actual=await probe(id);Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.CallerCancellation,actual.CallerCancellation);Assert.Equal(expected.Body,actual.Body);
        Assert.Equal(expected.UserAgents,actual.UserAgents);Assert.Equal(expected.Sends,actual.Sends);Assert.Equal(expected.Budgets,actual.Budgets);Assert.Equal(0,actual.ActiveTimers);Assert.Equal(expected.Elapsed,actual.Elapsed);
    }
    public static IEnumerable<object[]> Cases=>Names.Select(value=>new object[]{value});
    public static IEnumerable<object[]> Faults=>Names.SelectMany(id=>new[]{"error","cancellation","body","agent","sends","budgets","leak","elapsed"}.Select(mutation=>new object[]{id,mutation}));
    public static DocumentationHttpObservation Wrong(string id,string mutation)
    {
        var value=Correct(id);return mutation switch{
            "error"=>value with{Error="wrong"},"cancellation"=>value with{CallerCancellation=!value.CallerCancellation},"body"=>value with{Body="wrong"},"agent"=>value with{UserAgents=["wrong"]},
            "sends"=>value with{Sends=99},"budgets"=>value with{Budgets=[TimeSpan.FromSeconds(100)]},"leak"=>value with{ActiveTimers=1},_=>value with{Elapsed=value.Elapsed+TimeSpan.FromSeconds(1)}};
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationHttpTransportMockTests
{
    [Theory,MemberData(nameof(DocumentationHttpCases.Cases),MemberType=typeof(DocumentationHttpCases))]
    public Task CorrectExactAgentAndPerBlockingOperationMocksPass(string id)=>DocumentationHttpCases.AssertContract(id,value=>Task.FromResult(DocumentationHttpCases.Correct(value)));
    [Theory,MemberData(nameof(DocumentationHttpCases.Faults),MemberType=typeof(DocumentationHttpCases))]
    public async Task EveryHeaderTimeoutCancellationDefaultAndDisposalObservationRejectsItsFault(string id,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationHttpCases.AssertContract(id,value=>Task.FromResult(DocumentationHttpCases.Wrong(value,mutation)))));
}
