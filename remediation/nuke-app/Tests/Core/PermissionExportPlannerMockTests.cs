using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class PermissionExportPlannerCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            (string Key,string Value)[] required=[("project-root","contracts"),("source-catalog","sealed-release"),("output-root","exported-permissions")];
            string[] steps=["admit-readonly-export","export-permissions","emit-coverage"];
            var edges=steps.Skip(1).Select((step,index)=>step+"<-"+steps[index]).ToArray();
            var key=new OperationKey(Category.Catalog,Operation.Export);
            var cases=new List<PlannerCase>{new("PERMISSION-EXPORT",PlannerContractCases.Request("Catalog","Export",required),key,steps,[],edges),
                new("PERMISSION-EXPORT-CASE",PlannerContractCases.Request("catalog","export",required),key,steps,[],edges)};
            foreach(var parameter in required)cases.Add(new("MISSING-"+parameter.Key,PlannerContractCases.Request("Catalog","Export",required.Where(value=>value.Key!=parameter.Key).ToArray()),null,[],["MISSING_PARAMETER:"+parameter.Key]));
            foreach(var parameter in new[]{"stage-root","source-manifest","pipeline-request","export-kind"})
                cases.Add(new("UNSUPPORTED-"+parameter,PlannerContractCases.Request("Catalog","Export",required.Append((parameter,"unexpected")).ToArray()),null,[],["UNSUPPORTED_PARAMETER:"+parameter]));
            return cases.Select(test=>new object[]{test});
        }
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class PermissionExportPlannerMockTests
{
    [Theory,MemberData(nameof(PermissionExportPlannerCases.Cases),MemberType=typeof(PermissionExportPlannerCases))]
    public void CorrectPlansUseExistingSharedAssertions(PlannerCase test)=>new PlannerMockGateTests().CorrectMocksPassEveryAssertion(test);
    [Theory,MemberData(nameof(PermissionExportPlannerCases.Cases),MemberType=typeof(PermissionExportPlannerCases))]
    public void IndependentPlanAndEffectControlsRejectWrongResponses(PlannerCase test)
    {
        var harness=new PlannerMockGateTests();harness.WrongEffectsAreRejectedInsidePassingHarness(test);
        harness.WrongKeysPlansAndErrorsAreRejected(test);harness.IndividualKeyStepAndErrorMutationsAreRejected(test);
    }
}
