using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record PrerequisiteObservation(string Status, string UnitProjects, string IntegrationProjects, int GitCalls);
public sealed record PrerequisiteCase(string Name, string Status, string UnitProjects = "", string IntegrationProjects = "", int GitCalls = 4);
public interface IPrerequisiteCaseSystem { Task<PrerequisiteObservation> ExecuteAsync(string name); }
public static class ReviewPrerequisiteCases
{
    public static readonly PrerequisiteCase[] All =
    [
        new("complete-pending-hv", "verified", "tests/unit.csproj", "tests/integration.csproj"),
        new("omitted-unit-project", "PROJECT_METADATA_UNRESOLVED"),
        new("missing-evaluation", "PROJECT_METADATA_UNRESOLVED"),
        new("dynamic-unknown-kind", "PROJECT_METADATA_UNRESOLVED"),
        new("missing-test-metadata", "PROJECT_METADATA_UNRESOLVED"),
        new("stale-project-definition", "EVIDENCE_HASH_MISMATCH"),
        new("wrong-evaluation-head", "HEAD_MISMATCH"),
        new("wrong-live-git-head", "HEAD_MISMATCH", GitCalls: 1),
        new("missing-fr-ac", "REQUIREMENTS_INCOMPLETE"),
        new("missing-tr-mapping", "REQUIREMENTS_INCOMPLETE"),
        new("missing-test-mapping", "REQUIREMENTS_INCOMPLETE"),
        new("missing-ac-coverage", "REQUIREMENTS_INCOMPLETE"),
        new("forged-caller-complete", "REQUIREMENTS_INCOMPLETE"),
        new("omitted-scope-integration", "INTEGRATION_SCOPE_INCOMPLETE"),
        new("missing-checklist-entry", "PREFLIGHT_INCOMPLETE"),
        new("stale-checklist-evidence", "EVIDENCE_HASH_MISMATCH")
    ];
    public static IEnumerable<object[]> Data => All.Select(c => new object[] { c.Name });
    public static void AssertContract(string name, PrerequisiteObservation actual)
    {
        var expected = All.Single(x => x.Name == name);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.UnitProjects, actual.UnitProjects);
        Assert.Equal(expected.IntegrationProjects, actual.IntegrationProjects);
        Assert.Equal(expected.GitCalls, actual.GitCalls);
    }
}
public sealed class ScriptedPrerequisites : IPrerequisiteCaseSystem
{
    public Task<PrerequisiteObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "complete-pending-hv" => new PrerequisiteObservation("verified", "tests/unit.csproj", "tests/integration.csproj", 4),
        "omitted-unit-project" or "missing-evaluation" or "dynamic-unknown-kind" or "missing-test-metadata" => new("PROJECT_METADATA_UNRESOLVED", "", "", 4),
        "stale-project-definition" or "stale-checklist-evidence" => new("EVIDENCE_HASH_MISMATCH", "", "", 4),
        "wrong-evaluation-head" => new("HEAD_MISMATCH", "", "", 4),
        "wrong-live-git-head" => new("HEAD_MISMATCH", "", "", 1),
        "missing-fr-ac" or "missing-tr-mapping" or "missing-test-mapping" or "missing-ac-coverage" or "forged-caller-complete" => new("REQUIREMENTS_INCOMPLETE", "", "", 4),
        "omitted-scope-integration" => new("INTEGRATION_SCOPE_INCOMPLETE", "", "", 4),
        "missing-checklist-entry" => new("PREFLIGHT_INCOMPLETE", "", "", 4),
        _ => throw new InvalidOperationException("Unknown prerequisite fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewPrerequisiteMockGate
{
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task CorrectMock(string name) => ReviewPrerequisiteCases.AssertContract(name, await new ScriptedPrerequisites().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task IndividualStatusControl(string name)
    {
        var actual = await new ScriptedPrerequisites().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewPrerequisiteCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task IndividualUnitInventoryControl(string name)
    {
        var actual = await new ScriptedPrerequisites().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewPrerequisiteCases.AssertContract(name, actual with { UnitProjects = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task IndividualIntegrationInventoryControl(string name)
    {
        var actual = await new ScriptedPrerequisites().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewPrerequisiteCases.AssertContract(name, actual with { IntegrationProjects = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task IndividualInteractionControl(string name)
    {
        var actual = await new ScriptedPrerequisites().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewPrerequisiteCases.AssertContract(name, actual with { GitCalls = actual.GitCalls + 1 })));
    }
}
