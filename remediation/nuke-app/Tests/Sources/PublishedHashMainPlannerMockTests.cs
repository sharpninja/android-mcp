using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class PublishedHashMainPlannerCases
{
    private static readonly string[] Steps = ["admit-pinned-workflow", "admit-task-inputs", "verify-task-source-pins", "capture-runtime-identity", "extract-source-generation", "seal-extraction"];
    private static readonly string[] Edges = ["admit-task-inputs<-admit-pinned-workflow", "verify-task-source-pins<-admit-task-inputs", "capture-runtime-identity<-verify-task-source-pins", "extract-source-generation<-capture-runtime-identity", "seal-extraction<-extract-source-generation"];
    public static IEnumerable<object[]> Cases => new[] { "Declarations", "InterfaceHashes" }.Select(task => new object[] { new PlannerCase("HASH-TASK-" + task,
        PlannerContractCases.Request("Sources", "Extract", [("project-root", "inputs"), ("pipeline-request", "hash.json"), ("pipeline-request-sha256", new string('a', 64)), ("pipeline-request-length", "42"), ("extract-task", task)]), new(Category.Sources, Operation.Extract), Steps, [], Edges) });
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-003")]
public sealed class PublishedHashMainPlannerMockTests
{
    [Theory, MemberData(nameof(PublishedHashMainPlannerCases.Cases), MemberType = typeof(PublishedHashMainPlannerCases))]
    public void ApprovedTaskSelectionUsesTheExistingOperation(PlannerCase test)
    {
        PlannerContractCases.AssertContract(test, new ScriptedPlanner(PlannerContractCases.Correct(test)));
        var wrong = PlannerContractCases.Correct(test) with { Plan = null, Errors = [new("UNSUPPORTED_PARAMETER", "extract-task", "wrong")] };
        Assert.NotNull(Record.Exception(() => PlannerContractCases.AssertContract(test, new ScriptedPlanner(wrong))));
        Assert.NotNull(Record.Exception(() => PlannerContractCases.AssertContract(test, new ScriptedPlanner(PlannerContractCases.Correct(test), "effect-before-pin"))));
    }
}
