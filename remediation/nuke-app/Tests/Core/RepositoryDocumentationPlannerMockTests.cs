using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// This is the category/operation replacement for the original checker's
// --root entrypoint. Content predicates belong to the existing checker cases.
public static class RepositoryDocumentationPlannerCases
{
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("REPO-DOC-PLAN-01",PlannerContractCases.Request("Validate","Design",("project-root","repository")),
            new(Category.Validate,Operation.Design),["check-repository-documentation"],[]),
        new PlannerCase("REPO-DOC-PLAN-02",PlannerContractCases.Request("Validate","Design"),
            null,[],["MISSING_PARAMETER:project-root"]),
        new PlannerCase("REPO-DOC-PLAN-03",PlannerContractCases.Request("Validate","Design",("project-root"," ")),
            null,[],["MISSING_PARAMETER:project-root"]),
        new PlannerCase("REPO-DOC-PLAN-04",PlannerContractCases.Request("Validate","Design",("project-root","repository"),("output-root","generated")),
            null,[],["UNSUPPORTED_PARAMETER:output-root"])
    }.Select(test=>new object[]{test});
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationPlannerMockTests
{
    [Theory,MemberData(nameof(RepositoryDocumentationPlannerCases.Cases),MemberType=typeof(RepositoryDocumentationPlannerCases))]
    public void CorrectReadOnlyPlansPass(PlannerCase test) =>
        PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));

    [Theory,MemberData(nameof(RepositoryDocumentationPlannerCases.Cases),MemberType=typeof(RepositoryDocumentationPlannerCases))]
    public void WrongRouteOrMutationIsRejected(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"write-before-validation"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct with {Plan=null,Errors=[new("WRONG","","wrong")]}))));
    }
}
