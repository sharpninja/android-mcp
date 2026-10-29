using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// Exact three original test_design023_guardrails.py scenarios. The old Node
// subprocess is replaced by the existing typed validator boundary, not another
// validator or an interpreter. Only ModelContractException maps to guard failure.
public sealed record LegacyGuardCase(string Method,IReadOnlyList<string> Diagnostics)
{
    public override string ToString()=>Method;
}
public static class LegacyGuardOriginalCases
{
    public static readonly LegacyGuardCase[] All=[
        new("test_current_cross_artifact_model",[]),
        new("test_missing_dispatch_edge_and_revoke_bypass_fail",["Missing dispatch caller","Revocation bypasses"]),
        new("test_placeholder_state_contract_and_missing_route_fail",["Placeholder action","Template states","Missing sealed stage route"])
    ];
    public static IEnumerable<object[]> Data=>All.Select(value=>new object[]{value});
    public static ModelContractInput Input(LegacyGuardCase test)
    {
        var environment=new ModelContractEnvironment();
        if(test==All[1])
        {
            environment.CodeText(text=>
            {
                const string old="InvocationPlanner ..> AdmissionGate : beginDispatch";
                Assert.Contains(old,text,StringComparison.Ordinal);
                return text.Replace(old,"InvocationPlanner ..> BackendAdapter : old dispatch",StringComparison.Ordinal);
            });
            environment.DslText(text=>
            {
                const string old="ownerCommands -> bridge.app.policy \"Enter exact protected commit";
                Assert.Contains(old,text,StringComparison.Ordinal);
                return text.Replace(old,"ownerCommands -> bridge.app.catalog \"Enter exact protected commit",StringComparison.Ordinal);
            });
        }
        else if(test==All[2])
        {
            var screen=environment.Blueprint["screens"]![0]!.AsObject();
            foreach(var state in screen["state_specs"]!.AsObject())
            {state.Value!["effect"]="Read-only generic";state.Value["recovery"]="Retry generic";}
            screen["control_specs"]!.AsObject().First().Value!["behavior"]="Exact named action; protected mutations open SCR09; reads never change authority";
            Assert.True(environment.Api["paths"]!.AsObject().Remove("/v1/catalog/import-stages"));
        }
        else Assert.Equal(All[0],test);
        return environment.Input();
    }
    public static void AssertOriginal(LegacyGuardCase test,ModelContractInput input,IModelContractValidator provider)
    {
        var before=JsonSerializer.Serialize(input);
        ModelContractException? failure=null;
        try{provider.Validate(input);}catch(ModelContractException error){failure=error;}
        Assert.Equal(before,JsonSerializer.Serialize(input));
        if(test.Diagnostics.Count==0)Assert.Null(failure);
        else
        {
            Assert.NotNull(failure);
            foreach(var diagnostic in test.Diagnostics)Assert.Contains(diagnostic,failure.Message,StringComparison.Ordinal);
        }
    }
}
public sealed class ScriptedLegacyGuardValidator(LegacyGuardCase test,ModelContractInput expected,string? omitted=null,bool wrongOutcome=false):IModelContractValidator
{
    public ModelContractValidationResult Validate(ModelContractInput input)
    {
        Assert.Same(expected,input);
        if(wrongOutcome)
        {
            if(test.Diagnostics.Count==0)throw new ModelContractException(["Wrong guard failure"]);
            return new(true,ModelContractCases.Scope,11,76);
        }
        if(test.Diagnostics.Count>0)throw new ModelContractException(test.Diagnostics.Where(value=>value!=omitted).ToArray());
        return new(true,ModelContractCases.Scope,11,76);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyGuardOriginalMethodMockTests
{
    [Theory,MemberData(nameof(LegacyGuardOriginalCases.Data),MemberType=typeof(LegacyGuardOriginalCases))]
    public void CorrectMockBindsExactOriginalScenario(LegacyGuardCase test)
    {var input=LegacyGuardOriginalCases.Input(test);LegacyGuardOriginalCases.AssertOriginal(test,input,new ScriptedLegacyGuardValidator(test,input));}
    [Theory,MemberData(nameof(LegacyGuardOriginalCases.Data),MemberType=typeof(LegacyGuardOriginalCases))]
    public void WrongOutcomeIsRejected(LegacyGuardCase test)
    {
        var input=LegacyGuardOriginalCases.Input(test);
        Assert.NotNull(Record.Exception(()=>LegacyGuardOriginalCases.AssertOriginal(test,input,new ScriptedLegacyGuardValidator(test,input,wrongOutcome:true))));
    }
    public static IEnumerable<object[]> Diagnostics=>LegacyGuardOriginalCases.All.SelectMany(test=>test.Diagnostics.Select(value=>new object[]{test,value}));
    [Theory,MemberData(nameof(Diagnostics))]
    public void EveryOriginalDiagnosticHasItsOwnMissingControl(LegacyGuardCase test,string diagnostic)
    {
        var input=LegacyGuardOriginalCases.Input(test);
        Assert.NotNull(Record.Exception(()=>LegacyGuardOriginalCases.AssertOriginal(test,input,new ScriptedLegacyGuardValidator(test,input,omitted:diagnostic))));
    }
}
