using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class CatalogSourcesPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","inputs"),("pipeline-request","catalog-workflow.json"),
        ("pipeline-request-sha256",new string('a',64)),("pipeline-request-length","1234")];
    private static readonly string[] Steps = ["admit-pinned-workflow","admit-locked-recipe","verify-source-inventory",
        "capture-runtime-identity","project-effective-inventory","build-catalog","seal-catalog"];
    private static readonly string[] Edges = Steps.Skip(1).Select((step,index)=>step+"<-"+Steps[index]).ToArray();
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var key=new OperationKey(Category.Catalog,Operation.BuildFromSources);
            var cases=new List<PlannerCase>
            {
                new("CAT-SOURCES-01",PlannerContractCases.Request("Catalog","BuildFromSources",Required),key,Steps,[],Edges),
                new("CAT-SOURCES-02",PlannerContractCases.Request("catalog","buildfromsources",Required),key,Steps,[],Edges),
                new("CAT-SOURCES-03",PlannerContractCases.Request("Catalog","BuildFromSources"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray())
            };
            foreach(var parameter in Required)
                cases.Add(new("CAT-SOURCES-MISSING-"+parameter.Key,PlannerContractCases.Request("Catalog","BuildFromSources",Required.Where(x=>x.Key!=parameter.Key).ToArray()),null,[],["MISSING_PARAMETER:"+parameter.Key]));
            cases.Add(new("CAT-SOURCES-EMPTY-LENGTH",PlannerContractCases.Request("Catalog","BuildFromSources",Required.Select(x=>x.Key=="pipeline-request-length"?(x.Key," "):x).ToArray()),null,[],["MISSING_PARAMETER:pipeline-request-length"]));
            foreach(var parameter in new[]{"source-manifest","producer-id","projection","output-root"})
                cases.Add(new("CAT-SOURCES-UNSUPPORTED-"+parameter,PlannerContractCases.Request("Catalog","BuildFromSources",Required.Append((parameter,"caller-value")).ToArray()),null,[],["UNSUPPORTED_PARAMETER:"+parameter]));
            cases.Add(new("CAT-SOURCES-OTHER-CATEGORY",PlannerContractCases.Request("Sources","BuildFromSources",Required),null,[],["ILLEGAL_CATEGORY_OPERATION"]));
            return cases.Select(test=>new object[]{test});
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogSourcesPlannerMockTests
{
    [Theory,MemberData(nameof(CatalogSourcesPlannerCases.Cases),MemberType=typeof(CatalogSourcesPlannerCases))]
    public void CorrectTypedCatalogPlanUsesSharedContract(PlannerCase test)=>
        new PlannerMockGateTests().CorrectMocksPassEveryAssertion(test);

    [Theory,MemberData(nameof(CatalogSourcesPlannerCases.Cases),MemberType=typeof(CatalogSourcesPlannerCases))]
    public void SharedControlsAndEachDependencyRejectWrongPlans(PlannerCase test)
    {
        var shared=new PlannerMockGateTests();
        shared.WrongEffectsAreRejectedInsidePassingHarness(test);
        shared.WrongKeysPlansAndErrorsAreRejected(test);
        shared.IndividualKeyStepAndErrorMutationsAreRejected(test);
        var expected=PlannerContractCases.Correct(test);
        if(expected.Plan is null)return;
        foreach(var step in expected.Plan.Steps.Where(x=>x.DependsOn.Count>0))
        {
            var changed=expected.Plan.Steps.Select(x=>x==step?x with{DependsOn=[]}:x).ToArray();
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,
                new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=changed}}))));
        }
    }
}
