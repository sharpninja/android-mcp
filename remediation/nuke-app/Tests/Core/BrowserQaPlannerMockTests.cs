using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class BrowserQaPlannerCases
{
    private static readonly (string Key, string Value)[] Required =
    [("project-root", "ui-generation"), ("output-root", "new-browser-evidence"),
        ("browser-identity", "browser.json"), ("ui-manifest-sha256", new string('a',64)),
        ("expected-architecture-diagrams", "0")];
    private static readonly string[] Steps = ["validate-browser-generations", "run-native-browser-qa", "seal-browser-evidence"];
    private static readonly string[] Edges = ["run-native-browser-qa<-validate-browser-generations", "seal-browser-evidence<-run-native-browser-qa"];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("BROWSER-PLAN-01-explicit-ui-only", PlannerContractCases.Request("Design","BrowserQa",Required), new(Category.Design,Operation.BrowserQa), Steps, [], Edges),
        new PlannerCase("BROWSER-PLAN-02-missing-required", PlannerContractCases.Request("Design","BrowserQa"), null, [], Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("BROWSER-PLAN-03-full-generation-pair", PlannerContractCases.Request("Design","BrowserQa", Required.Select(x=>x.Key=="expected-architecture-diagrams"?(x.Key,"21"):x)
            .Concat(new[]{("architecture-root","architecture-generation"),("architecture-manifest-sha256",new string('b',64)),("viewport-width","1440"),("viewport-height","960")}).ToArray()), new(Category.Design,Operation.BrowserQa), Steps, [], Edges),
        new PlannerCase("BROWSER-PLAN-04-no-implicit-denominator", PlannerContractCases.Request("Design","BrowserQa",Required.Where(x=>x.Key!="expected-architecture-diagrams").ToArray()), null, [], ["MISSING_PARAMETER:expected-architecture-diagrams"]),
        new PlannerCase("BROWSER-PLAN-05-no-provider-option", PlannerContractCases.Request("Design","BrowserQa",Required.Concat(new[]{("disable-sandbox","true")}).ToArray()), null, [], ["UNSUPPORTED_PARAMETER:disable-sandbox"]),
        new PlannerCase("BROWSER-PLAN-06-no-empty-identity", PlannerContractCases.Request("Design","BrowserQa",Required.Select(x=>x.Key=="browser-identity"?(x.Key,""):x).ToArray()), null, [], ["MISSING_PARAMETER:browser-identity"])
    }.Select(test=>new object[]{test});
}

[Trait("Gate","Mock"), Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaPlannerMockTests
{
    [Theory, MemberData(nameof(BrowserQaPlannerCases.Cases), MemberType=typeof(BrowserQaPlannerCases))]
    public void CorrectPlanningPreservesExplicitScope(PlannerCase test) =>
        PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory, MemberData(nameof(BrowserQaPlannerCases.Cases), MemberType=typeof(BrowserQaPlannerCases))]
    public void WrongPlanningOrEarlyBrowserEffectsAreRejected(PlannerCase test)
    {
        var result=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(result,"browser-before-input-verification"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(result with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
    }
}
