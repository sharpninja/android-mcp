using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class CatalogValidationPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
        [("project-root","contracts"),("pipeline-request","validation.json"),
         ("pipeline-request-sha256",new string('a',64)),("pipeline-request-length","123")];
    private static readonly string[] Steps = ["admit-pinned-workflow","validate-catalog-readonly","emit-validation-receipt"];
    private static readonly string[] Edges = Steps.Skip(1).Select((step,index)=>step+"<-"+Steps[index]).ToArray();
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var key=new OperationKey(Category.Validate,Operation.Catalog);
            var cases=new List<PlannerCase>
            {
                new("VALIDATE-CATALOG",PlannerContractCases.Request("Validate","Catalog",Required),key,Steps,[],Edges),
                new("VALIDATE-CATALOG-CASE",PlannerContractCases.Request("validate","catalog",Required),key,Steps,[],Edges)
            };
            foreach(var parameter in Required)
                cases.Add(new("MISSING-"+parameter.Key,PlannerContractCases.Request("Validate","Catalog",Required.Where(x=>x.Key!=parameter.Key).ToArray()),null,[],["MISSING_PARAMETER:"+parameter.Key]));
            foreach(var parameter in new[]{"output-root","source-manifest","database-path","report-path","validation-mode"})
                cases.Add(new("UNSUPPORTED-"+parameter,PlannerContractCases.Request("Validate","Catalog",Required.Append((parameter,"value")).ToArray()),null,[],["UNSUPPORTED_PARAMETER:"+parameter]));
            return cases.Select(test=>new object[]{test});
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogValidationPlannerMockTests
{
    [Theory,MemberData(nameof(CatalogValidationPlannerCases.Cases),MemberType=typeof(CatalogValidationPlannerCases))]
    public void CorrectPlansUseTheSharedContract(PlannerCase test)=>
        new PlannerMockGateTests().CorrectMocksPassEveryAssertion(test);

    [Theory,MemberData(nameof(CatalogValidationPlannerCases.Cases),MemberType=typeof(CatalogValidationPlannerCases))]
    public void ExistingIndependentControlsRejectWrongPlans(PlannerCase test)
    {
        var harness=new PlannerMockGateTests();
        harness.WrongEffectsAreRejectedInsidePassingHarness(test);
        harness.WrongKeysPlansAndErrorsAreRejected(test);
        harness.IndividualKeyStepAndErrorMutationsAreRejected(test);
    }
}
