using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceBinaryDispatcherMockTests
{
    // Each case scripts its chosen dependency call, not the dispatch algorithm.
    private static ISourcesExtractionDispatcher Script(SourceBinaryDispatcherCase test, ISourceExtractor provider, ExtractionResult? output = null, string? wrong = null) =>
        new ScriptedSourcesExtractionDispatcher([test.Registered], (source, kind, scope) =>
        {
            Assert.Equal(test.Requested, kind); Assert.Null(scope);
            if ((test.Called && wrong != "omit-call") || wrong == "invent-call")
            {
                if (test.Fault is not null && (!test.Infrastructure || wrong == "swallow-infrastructure")) _ = Record.Exception(() => provider.Extract(source));
                else _ = provider.Extract(source);
            }
            if (wrong == "mutate-input") SourceBinaryDispatcherContractCases.MutateInput(source);
            return output ?? test.OriginalCase().Expected();
        });
    [Theory, MemberData(nameof(SourceBinaryDispatcherContractCases.Cases), MemberType = typeof(SourceBinaryDispatcherContractCases))]
    public void CorrectMocksAdmitOnlyExplicitRegisteredBinaryProvidersBeforeTextDecode(string id)
    { var test = SourceBinaryDispatcherContractCases.Get(id); SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider)); }
    [Theory, MemberData(nameof(SourceBinaryDispatcherContractCases.Mutations), MemberType = typeof(SourceBinaryDispatcherContractCases))]
    public void EachMetadataRawByteAndIdentityMutationIsRejected(string id, SourceMutation mutation)
    {
        var test = SourceBinaryDispatcherContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test,
            (provider, _) => Script(test, provider, test.OriginalCase().Expected() with { Metadata = wrong }))));
    }
    [Theory, MemberData(nameof(SourceBinaryDispatcherContractCases.Cases), MemberType = typeof(SourceBinaryDispatcherContractCases))]
    public void MissingInventedProviderCallsInputMutationAndSwallowedInfrastructureAreRejected(string id)
    {
        var test = SourceBinaryDispatcherContractCases.Get(id);
        if (!test.Infrastructure)
        {
            Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, wrong: test.Called ? "omit-call" : "invent-call"))));
            Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, wrong: "mutate-input"))));
            var expected = test.OriginalCase().Expected();
            var wrongStatus = expected with { StructuralStatus = expected.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Unsupported : StructuralParseStatus.Clean };
            Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, wrongStatus))));
            var wrongRecords = expected with { Records = [new System.Text.Json.Nodes.JsonObject { ["invented"] = "binding" }] };
            Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, wrongRecords))));
            if (expected.Issues.Count > 0)
                Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, expected with { Issues = [] }))));
        }
        else Assert.NotNull(Record.Exception(() => SourceBinaryDispatcherContractCases.AssertContract(test, (provider, _) => Script(test, provider, wrong: "swallow-infrastructure"))));
    }
}
