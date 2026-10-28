using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record PlannerCase(
    string Id, OperationRequest Request, OperationKey? ExpectedKey,
    string[] ExpectedSteps, string[] ExpectedErrors, string[]? ExpectedEdges = null);

public static class PlannerContractCases
{
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("REG-01", Request("Sources", "Plan",("project-root","inputs"),("source-manifest","recipe.json"),("recipe-lock","recipe.lock.json"),("output-root","plan")), new(Category.Sources,Operation.Plan), ["source-plan"], []),
        new PlannerCase("REG-02", Request("Catalog", "Build", ("project-root","catalog-contracts"),("source-manifest","sources.json"),("output-root","new-output"),("stage-root","private-stage")), new(Category.Catalog,Operation.Build), ["validate-source-manifest","build-catalog"], [], ["build-catalog<-validate-source-manifest"]),
        new PlannerCase("REG-03", Request("Design", "Assets", ("project-root","project"),("output-root","new-output")), new(Category.Design,Operation.Assets), ["validate-design-inputs","project-ui"], [], ["project-ui<-validate-design-inputs"]),
        new PlannerCase("REG-04", Request("Validate", "Toolchain"), new(Category.Validate,Operation.Toolchain), ["verify-toolchain"], []),
        new PlannerCase("REG-05", Request("Review", "Prepare", ("snapshot","snapshot")), new(Category.Review,Operation.Prepare), ["verify-review-evidence","prepare-review"], []),
        new PlannerCase("REG-06", Request("Publish", "Check", ("git-checkout","repository"),("git-identity","git.json"),("receipt-root","receipts")), new(Category.Publish,Operation.Check), ["check-deliverables"], []),
        new PlannerCase("REG-07", Request("NotACategory", "Plan"), null, [], ["UNKNOWN_CATEGORY"]),
        new PlannerCase("REG-08", Request("Sources", "NotAnOperation"), null, [], ["UNKNOWN_OPERATION"]),
        new PlannerCase("REG-09", Request("Sources", "Sync"), null, [], ["ILLEGAL_CATEGORY_OPERATION"]),
        new PlannerCase("REG-10", Request("Catalog", "Build"), null, [], ["MISSING_PARAMETER:project-root","MISSING_PARAMETER:output-root","MISSING_PARAMETER:source-manifest","MISSING_PARAMETER:stage-root"]),
        new PlannerCase("REG-11", Request("Sources", "Plan", ("project-root","inputs"),("source-manifest","recipe.json"),("recipe-lock","recipe.lock.json"),("output-root","plan"),("unrecognized-option","x")), null, [], ["UNSUPPORTED_PARAMETER:unrecognized-option"])
    }.Select(x => new object[] { x });

    public static OperationRequest Request(string category, string operation, params (string Key,string Value)[] parameters) =>
        new(category, operation, parameters.ToDictionary(x => x.Key,x => x.Value,StringComparer.Ordinal));

    public static PlanningResult Correct(PlannerCase test) => new(
        test.ExpectedKey is null ? null : new(test.ExpectedKey, test.ExpectedSteps.Select(x => new PlannedStep(x,(test.ExpectedEdges ?? []).Where(e=>e.StartsWith(x+"<-",StringComparison.Ordinal)).Select(e=>e[(e.IndexOf("<-",StringComparison.Ordinal)+2)..]).ToArray())).ToArray()),
        test.ExpectedErrors.Select(x => new PlanError(x.Split(':')[0],x.Contains(':') ? x.Split(':')[1] : "", "Fixture failure")).ToArray());

    public static void AssertContract(PlannerCase test, IOperationPlanner provider)
    {
        var parametersBefore = test.Request.Parameters.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray();
        var result = provider.Plan(test.Request);
        Assert.Equal(parametersBefore,test.Request.Parameters.OrderBy(x=>x.Key,StringComparer.Ordinal));
        Assert.Empty(provider.ObservedEffects);
        Assert.Equal(test.ExpectedKey is not null,result.IsValid);
        Assert.Equal(test.ExpectedKey,result.Plan?.Key);
        Assert.Equal(test.ExpectedSteps,result.Plan?.Steps.Select(x => x.Id).ToArray() ?? []);
        Assert.Equal(test.ExpectedErrors.Order(StringComparer.Ordinal),result.Errors.Select(x => x.Code+(x.Parameter.Length>0 ? ":"+x.Parameter : "")).Order(StringComparer.Ordinal));
        Assert.Equal((test.ExpectedEdges ?? []).Order(StringComparer.Ordinal),(result.Plan?.Steps.SelectMany(s=>s.DependsOn.Select(d=>s.Id+"<-"+d)) ?? []).Order(StringComparer.Ordinal));
        if (result.Plan is not null)
        {
            Assert.Equal(result.Plan.Steps.Count,result.Plan.Steps.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count());
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in result.Plan.Steps) { Assert.All(step.DependsOn,x=>Assert.Contains(x,visited)); visited.Add(step.Id); }
        }
    }
}

public sealed class ScriptedPlanner(PlanningResult result, params string[] effects) : IOperationPlanner
{
    public IReadOnlyList<string> ObservedEffects => effects;
    public OperationRequest? Received { get; private set; }
    public PlanningResult Plan(OperationRequest request) { Received=request;return result; }
}

[Trait("Gate","Mock")]
public sealed class PlannerMockGateTests
{
    [Theory,MemberData(nameof(PlannerContractCases.Cases),MemberType=typeof(PlannerContractCases))]
    public void CorrectMocksPassEveryAssertion(PlannerCase test)
    {
        var provider = new ScriptedPlanner(PlannerContractCases.Correct(test));
        PlannerContractCases.AssertContract(test,provider);
        Assert.Same(test.Request,provider.Received);
    }

    [Theory,MemberData(nameof(PlannerContractCases.Cases),MemberType=typeof(PlannerContractCases))]
    public void WrongEffectsAreRejectedInsidePassingHarness(PlannerCase test)
    {
        var provider = new ScriptedPlanner(PlannerContractCases.Correct(test),"unexpected-file-write");
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,provider)));
    }

    [Theory,MemberData(nameof(PlannerContractCases.Cases),MemberType=typeof(PlannerContractCases))]
    public void WrongKeysPlansAndErrorsAreRejected(PlannerCase test)
    {
        var wrong = test.ExpectedKey is null
            ? new PlanningResult(new(new(Category.Publish,Operation.Sync),[new("unexpected-push",[])]),[])
            : new PlanningResult(null,[new("UNEXPECTED_ERROR","","wrong")]);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrong))));
    }

    [Theory,MemberData(nameof(PlannerContractCases.Cases),MemberType=typeof(PlannerContractCases))]
    public void IndividualKeyStepAndErrorMutationsAreRejected(PlannerCase test)
    {
        var correct = PlannerContractCases.Correct(test);
        if (correct.Plan is not null)
        {
            var wrongKey = correct with { Plan=correct.Plan with { Key=new(Category.Publish,Operation.Sync) } };
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrongKey))));
            var wrongSteps = correct with { Plan=correct.Plan with { Steps=[] } };
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrongSteps))));
            if ((test.ExpectedEdges ?? []).Length>0)
            {
                var missingEdges = correct with { Plan=correct.Plan with { Steps=correct.Plan.Steps.Select(s=>s with { DependsOn=[] }).ToArray() } };
                Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(missingEdges))));
            }
        }
        else
        {
            var wrongErrors = correct with { Errors=[new("DIFFERENT_ERROR","","wrong")] };
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrongErrors))));
        }
    }

    [Fact]
    public void DuplicateAndOutOfOrderStepsAreRejected()
    {
        var test = new PlannerCase("REG-12",PlannerContractCases.Request("Design","Assets"),new(Category.Design,Operation.Assets),["project-ui","project-ui"],[]);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)))));
        test = test with { ExpectedSteps=["project-ui","validate-design-inputs"] };
        var wrong = new PlanningResult(new(test.ExpectedKey!,[new("project-ui",["validate-design-inputs"]),new("validate-design-inputs",[])]),[]);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrong))));
    }
}
