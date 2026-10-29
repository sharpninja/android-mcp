using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HashMainConcurrencyObservation(int Successes, int Refused, int Acquired, int Released,
    bool OneCommittedGeneration, bool AllPinsMatch, bool ExactFactBytes, bool InputsRetained, bool SameScope, bool OutputsRetained);
public interface IHashMainConcurrencyProbe { Task<HashMainConcurrencyObservation> RunAsync(); }
public static class PublishedHashMainConcurrencyContract
{
    public static async Task AssertCase(IHashMainConcurrencyProbe probe)
    {
        var actual = await probe.RunAsync(); Assert.Equal(1, actual.Successes); Assert.Equal(1, actual.Refused); Assert.Equal(2, actual.Acquired); Assert.Equal(2, actual.Released);
        Assert.True(actual.OneCommittedGeneration); Assert.True(actual.AllPinsMatch); Assert.True(actual.ExactFactBytes); Assert.True(actual.InputsRetained); Assert.True(actual.SameScope); Assert.True(actual.OutputsRetained);
    }
}
public sealed class ScriptedHashMainConcurrencyProbe(Func<HashMainConcurrencyObservation, HashMainConcurrencyObservation>? mutate = null) : IHashMainConcurrencyProbe
{
    public Task<HashMainConcurrencyObservation> RunAsync()
    {
        var expected = new HashMainConcurrencyObservation(1, 1, 2, 2, true, true, true, true, true, true);
        return Task.FromResult(mutate is null ? expected : mutate(expected));
    }
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainConcurrencyMockTests
{
    [Fact] public Task CorrectCooperatingGenerationOutcome() => PublishedHashMainConcurrencyContract.AssertCase(new ScriptedHashMainConcurrencyProbe());
    [Fact] public async Task EveryLeaseOutcomePinRetentionAndFactAssertionDiscriminates()
    {
        Func<HashMainConcurrencyObservation, HashMainConcurrencyObservation>[] mutations =
        [value => value with { Successes = 2 }, value => value with { Refused = 0 }, value => value with { Acquired = 1 }, value => value with { Released = 1 },
            value => value with { OneCommittedGeneration = false }, value => value with { AllPinsMatch = false }, value => value with { ExactFactBytes = false },
            value => value with { InputsRetained = false }, value => value with { SameScope = false }, value => value with { OutputsRetained = false }];
        foreach (var mutation in mutations) Assert.NotNull(await Record.ExceptionAsync(() => PublishedHashMainConcurrencyContract.AssertCase(new ScriptedHashMainConcurrencyProbe(mutation))));
    }
}
