using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSourcesExtractionDispatcher(IReadOnlyList<string> kinds, Func<SourceDocument, string?, string?, ExtractionResult> response) : ISourcesExtractionDispatcher
{
    public IReadOnlyList<string> SupportedKinds { get; } = kinds;
    public ExtractionResult Extract(SourceDocument source, string? kind, string? scope = null) => response(source, kind, scope);
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceDispatcherMockGateTests
{
    // Scripted behavior is fixed by each independent case. This is deliberately
    // not a source-kind resolver, decoder, identity producer or exception policy.
    private static ISourcesExtractionDispatcher Script(SourceDispatcherCase test, IReadOnlyList<ISourceExtractor> providers,
        Func<ExtractionResult>? output = null, string? wrongInteraction = null, bool wrongKinds = false) => new ScriptedSourcesExtractionDispatcher(
        wrongKinds ? ["wrong-kind"] : providers.Select(x => x.Kind).Order(StringComparer.Ordinal).ToArray(), (source, kind, scope) =>
        {
            Assert.NotNull(source); Assert.Equal(test.Kind, kind); Assert.Equal(test.Scope, scope);
            if (wrongInteraction == "mutate-input")
            { Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var input)); input.Array![input.Offset] ^= 1; }
            var selected = wrongInteraction == "wrong-provider" ? providers.FirstOrDefault(x => x.Kind != test.ProviderKind) : providers.SingleOrDefault(x => x.Kind == test.ProviderKind);
            if (wrongInteraction == "omit-provider") selected = null;
            if (selected is not null)
            {
                if (test.ProviderFault is not null && !test.InfrastructureFault)
                    Assert.Same(test.ProviderFault, Record.Exception(() => selected.Extract(source)));
                else if (wrongInteraction == "swallow-infrastructure") _ = Record.Exception(() => selected.Extract(source));
                else _ = selected.Extract(source);
            }
            return output?.Invoke() ?? test.Expected();
        });

    [Theory, MemberData(nameof(SourceDispatcherContractCases.Cases), MemberType = typeof(SourceDispatcherContractCases))]
    public void CorrectMocksUseExactProviderBoundariesAndFutureDispatcherAssertions(string id)
    {
        var test = SourceDispatcherContractCases.Get(id);
        SourceDispatcherContractCases.AssertContract(test, (providers, profile) =>
        { Assert.Equal(test.Profile, profile); return Script(test, providers); });
    }

    [Theory, MemberData(nameof(SourceDispatcherContractCases.MetadataMutations), MemberType = typeof(SourceDispatcherContractCases))]
    public void EveryIdentityContextRawAndProviderMetadataPropertyRejectsItsOwnMissingAndWrongValue(string id, SourceMutation mutation)
    {
        var test = SourceDispatcherContractCases.Get(id); var wrong = SourceMutationCases.Apply(SourceDispatcherContractCases.MetadataMutationCase(test), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test,
            (providers, _) => Script(test, providers, () => test.Expected() with { Metadata = wrong }))));
    }

    [Theory, MemberData(nameof(SourceDispatcherContractCases.RecordMutations), MemberType = typeof(SourceDispatcherContractCases))]
    public void EveryNativeRecordPropertyRejectsItsOwnMissingAndWrongValue(string id, SourceMutation mutation)
    {
        var test = SourceDispatcherContractCases.Get(id); var wrong = SourceMutationCases.Apply(SourceDispatcherContractCases.RecordMutationCase(test), mutation).Records;
        Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test,
            (providers, _) => Script(test, providers, () => test.Expected() with { Records = wrong }))));
    }

    [Theory, MemberData(nameof(SourceDispatcherContractCases.ResultCases), MemberType = typeof(SourceDispatcherContractCases))]
    public void WrongStatusDroppedOrInventedRecordsAndIssueControlsAreRejected(string id)
    {
        var test = SourceDispatcherContractCases.Get(id); var expected = test.Expected();
        var wrongStatus = expected with { StructuralStatus = expected.StructuralStatus == StructuralParseStatus.Unsupported ? StructuralParseStatus.Clean : StructuralParseStatus.Unsupported };
        var extraIssue = expected with { Issues = expected.Issues.Concat([new SourceIssue("invented", "not original", null, null)]).ToArray() };
        var extraRecord = expected with { Records = expected.Records.Concat([new System.Text.Json.Nodes.JsonObject { ["invented"] = true }]).ToArray() };
        foreach (var wrong in new[] { wrongStatus, extraIssue, extraRecord })
            Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, () => wrong))));
        if (expected.Records.Count > 0)
            Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, () => expected with { Records = [] }))));
        if (expected.Issues.Count > 0)
        {
            foreach (var wrong in new[] { expected with { Issues = [] }, expected with { Issues = expected.Issues.Select(x => x with { Reason = "" }).ToArray() }, expected with { Issues = expected.Issues.Select(x => x with { Code = "wrong" }).ToArray() } })
                Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, () => wrong))));
        }
    }

    [Theory, MemberData(nameof(SourceDispatcherContractCases.Cases), MemberType = typeof(SourceDispatcherContractCases))]
    public void WrongProviderExtensionGuessMissingCallsInputMutationAndKindListAreRejected(string id)
    {
        var test = SourceDispatcherContractCases.Get(id);
        foreach (var interaction in new[] { "wrong-provider", "mutate-input" })
            Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, wrongInteraction: interaction))));
        if (test.ProviderKind is not null)
            Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, wrongInteraction: "omit-provider"))));
        Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, wrongKinds: true))));
        if (test.InfrastructureFault)
            Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertContract(test, (providers, _) => Script(test, providers, wrongInteraction: "swallow-infrastructure"))));
    }

    [Theory]
    [InlineData("REG-duplicate")]
    [InlineData("REG-empty")]
    [InlineData("REG-null-profile")]
    public void RegistrationAndProfilePreconditionMocksRejectMissingAndIncorrectFailures(string id)
    {
        SourceDispatcherContractCases.AssertRegistrationFailure(id, (_, _) => throw (id == "REG-null-profile" ? new ArgumentNullException("profile") : new ArgumentException("Invalid registered kind")));
        Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertRegistrationFailure(id,
            (_, _) => new ScriptedSourcesExtractionDispatcher([], (_, _, _) => throw new IOException("not called")))));
        Assert.NotNull(Record.Exception(() => SourceDispatcherContractCases.AssertRegistrationFailure(id, (_, _) => throw new IOException("wrong exception"))));
    }
}
