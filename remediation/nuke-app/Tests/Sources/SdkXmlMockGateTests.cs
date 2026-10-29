using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SdkXmlFailureCase(string Id, string Xml, string? Target, Type ErrorType, string MessageFragment)
{
    public SourceDocument Document() => new("fixtures/" + Id + ".xml", Encoding.UTF8.GetBytes(Xml),
        new("fixture-sdk", "fixture-pin", "sdk-snapshot", Target));
}

public static class SdkXmlFailureCases
{
    public static IReadOnlyList<SdkXmlFailureCase> All { get; } =
    [
        new("SDK-ERR01", "<api version=\"4\"/>", null, typeof(ArgumentException), "platform"),
        new("SDK-ERR02", "<api version=\"4\"/>", "", typeof(ArgumentException), "platform"),
        new("SDK-ERR03", "<api version=\"4\"/>", "not-a-version", typeof(ArgumentException), "platform"),
        new("SDK-ERR04", "<api version=\"3\"/>", "37.2", typeof(FormatException), "format"),
        new("SDK-ERR05", "<other version=\"4\"/>", "37.2", typeof(FormatException), "format"),
        new("SDK-ERR06", "<api version=\"4\"><class", "37.2", typeof(FormatException), "XML"),
        new("SDK-ERR07", "", "37.2", typeof(FormatException), "XML"),
        new("SDK-ERR08", "<api version=\"4\" version=\"4\"/>", "37.2", typeof(FormatException), "XML"),
        new("SDK-ERR09", "<!DOCTYPE api SYSTEM \"https://fixture.invalid/forbidden\"><api version=\"4\"/>", "37.2", typeof(FormatException), "XML"),
        new("SDK-ERR10", "<!DOCTYPE api [<!ENTITY x \"expanded\">]><api version=\"4\"><class name=\"&x;\"/></api>", "37.2", typeof(FormatException), "XML")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static SdkXmlFailureCase Get(string id) => All.Single(x => x.Id == id);

    public static void AssertContract(SdkXmlFailureCase test, ISourceExtractor provider)
    {
        var source = test.Document(); var original = source.Bytes.ToArray();
        Assert.Equal("sdk-api-index", provider.Kind);
        var error = Assert.Throws(test.ErrorType, () => provider.Extract(source));
        Assert.Contains(test.MessageFragment, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, source.Bytes.ToArray());
    }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SdkXmlMockGateTests
{
    [Theory, MemberData(nameof(SdkXmlContractCases.Cases), MemberType = typeof(SdkXmlContractCases))]
    public void CorrectSdkRowsPassSameFutureRealAssertions(string id)
    {
        var test = SdkXmlContractCases.Get(id);
        var provider = new ScriptedSourceExtractor(test.Kind, _ => test.CorrectResult());
        SdkXmlContractCases.AssertContract(test, provider);
        var received = Assert.Single(provider.Received);
        Assert.Equal(test.Document().Context, received.Context);
        Assert.Equal(test.Document().RelativePath, received.RelativePath);
    }

    [Theory, MemberData(nameof(SdkXmlContractCases.Mutations), MemberType = typeof(SdkXmlContractCases))]
    public void SdkPropertiesIndividuallyRejectOmissionsAndWrongValues(string id, SourceMutation mutation)
    {
        var test = SdkXmlContractCases.Get(id);
        var provider = new ScriptedSourceExtractor(test.Kind, _ => SourceMutationCases.Apply(test, mutation));
        Assert.NotNull(Record.Exception(() => SdkXmlContractCases.AssertContract(test, provider)));
        Assert.Single(provider.Received);
    }

    [Theory, MemberData(nameof(SdkXmlContractCases.Cases), MemberType = typeof(SdkXmlContractCases))]
    public void SdkNoDropOrderStatusAndLiteralAbsenceAssertionsDiscriminate(string id)
    {
        var test = SdkXmlContractCases.Get(id); var correct = test.CorrectResult();
        var incorrect = new List<ExtractionResult>
        {
            correct with { StructuralStatus = correct.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean },
            correct with { Issues = correct.Issues.Concat([new SourceIssue("invented", "Invented SDK issue", null, null)]).ToArray() }
        };
        if (correct.Records.Count > 0)
        {
            incorrect.Add(correct with { Records = correct.Records.Skip(1).ToArray() });
            incorrect.Add(correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() });
            if (correct.Records.Count > 1) incorrect.Add(correct with { Records = correct.Records.Reverse().ToArray() });
            foreach (var key in new[] { "api_index_attributes", "api_lifecycle", "container_api_lifecycle" })
            {
                var added = test.CorrectResult();
                added.Records[0]["extras"]![key]!["invented-attribute"] = "not-present-in-source";
                incorrect.Add(added);
            }
        }
        else incorrect.Add(correct with { Records = [new JsonObject { ["kind"] = "invented" }] });
        if (correct.Issues.Count > 0) incorrect.Add(correct with { Issues = [] });
        foreach (var wrong in incorrect)
            Assert.NotNull(Record.Exception(() => SdkXmlContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => wrong))));
    }

    [Theory, MemberData(nameof(SdkXmlFailureCases.Cases), MemberType = typeof(SdkXmlFailureCases))]
    public void CorrectTypedFailureMocksPassBeforeXmlImplementation(string id)
    {
        var test = SdkXmlFailureCases.Get(id);
        var provider = new ScriptedSourceExtractor("sdk-api-index", _ => throw Create(test.ErrorType, test.MessageFragment + " fixture rejection"));
        SdkXmlFailureCases.AssertContract(test, provider);
        Assert.Single(provider.Received);
    }

    [Theory, MemberData(nameof(SdkXmlFailureCases.Cases), MemberType = typeof(SdkXmlFailureCases))]
    public void MissingWrongTypeAndWrongReasonFailuresAreRejected(string id)
    {
        var test = SdkXmlFailureCases.Get(id);
        var noFailure = new ScriptedSourceExtractor("sdk-api-index", _ => new([], [], StructuralParseStatus.Clean));
        var wrongType = new ScriptedSourceExtractor("sdk-api-index", _ => throw new IOException("Unexpected I/O failure"));
        var wrongReason = new ScriptedSourceExtractor("sdk-api-index", _ => throw Create(test.ErrorType, "unrelated"));
        foreach (var wrong in new[] { noFailure, wrongType, wrongReason })
            Assert.NotNull(Record.Exception(() => SdkXmlFailureCases.AssertContract(test, wrong)));
    }

    [Fact]
    public void SdkFixtureInventoryRetainsFullBoundaryDenominators()
    {
        Assert.Equal(20, SdkXmlContractCases.All.Count);
        Assert.Equal(10, SdkXmlFailureCases.All.Count);
        Assert.Equal(5, SdkXmlContractCases.Get("SDK-01").ExpectedRecords.Count);
        Assert.Equal(3, SdkXmlContractCases.Get("SDK-01").ExpectedRecords.Count(x => x["extras"]!["lifecycle_status"]!.GetValue<string>() == "current-at-target"));
        Assert.Equal(2, SdkXmlContractCases.Get("SDK-01").ExpectedRecords.Count(x => x["extras"]!["lifecycle_status"]!.GetValue<string>() == "not-current-at-target"));
        Assert.Empty(SdkXmlContractCases.Get("SDK-13").ExpectedRecords);
        Assert.All(SdkXmlContractCases.All, x => Assert.False(string.IsNullOrWhiteSpace(x.Evidence)));
    }

    private static Exception Create(Type type, string message) => type == typeof(ArgumentException) ? new ArgumentException(message) : new FormatException(message);
}
