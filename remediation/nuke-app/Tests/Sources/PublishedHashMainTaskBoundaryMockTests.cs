using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HashTaskBoundaryObservation(int Exit, bool TaskRejected, bool NoWorkflowReadError, bool NoOutput, bool InputsRetained);
public interface IHashTaskBoundaryProbe { Task<HashTaskBoundaryObservation> RunAsync(string task); }
public static class PublishedHashMainTaskBoundaryContract
{
    public static IEnumerable<object[]> Cases => new[] { "87", "-1" }.Select(task => new object[] { task });
    public static async Task AssertCase(string task, IHashTaskBoundaryProbe probe)
    { var actual = await probe.RunAsync(task); Assert.NotEqual(0, actual.Exit); Assert.True(actual.TaskRejected); Assert.True(actual.NoWorkflowReadError); Assert.True(actual.NoOutput); Assert.True(actual.InputsRetained); }
}
public sealed class ScriptedHashTaskBoundaryProbe(Func<HashTaskBoundaryObservation, HashTaskBoundaryObservation>? mutate = null) : IHashTaskBoundaryProbe
{
    public Task<HashTaskBoundaryObservation> RunAsync(string task)
    { Assert.Contains(task, new[] { "87", "-1" }); var result = new HashTaskBoundaryObservation(1, true, true, true, true); return Task.FromResult(mutate is null ? result : mutate(result)); }
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004")]
public sealed class PublishedHashMainTaskBoundaryMockTests
{
    [Theory, MemberData(nameof(PublishedHashMainTaskBoundaryContract.Cases), MemberType = typeof(PublishedHashMainTaskBoundaryContract))]
    public Task UnknownNumericEnumCannotFallBackToDeclarations(string task) => PublishedHashMainTaskBoundaryContract.AssertCase(task, new ScriptedHashTaskBoundaryProbe());
    [Theory, MemberData(nameof(PublishedHashMainTaskBoundaryContract.Cases), MemberType = typeof(PublishedHashMainTaskBoundaryContract))]
    public async Task EveryRejectionBeforeInputAndRetentionAssertionDiscriminates(string task)
    {
        Func<HashTaskBoundaryObservation, HashTaskBoundaryObservation>[] mutations = [value => value with { Exit = 0 }, value => value with { TaskRejected = false }, value => value with { NoWorkflowReadError = false }, value => value with { NoOutput = false }, value => value with { InputsRetained = false }];
        foreach (var mutate in mutations) Assert.NotNull(await Record.ExceptionAsync(() => PublishedHashMainTaskBoundaryContract.AssertCase(task, new ScriptedHashTaskBoundaryProbe(mutate))));
    }
}
