using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSourceExtractor(
    string kind, Func<SourceDocument, ExtractionResult> response) : ISourceExtractor
{
    public string Kind { get; } = kind;
    public List<SourceDocument> Received { get; } = [];
    public ExtractionResult Extract(SourceDocument source) { Received.Add(source); return response(source); }
}

public sealed record SourceMutation(int RecordIndex, string[] Path, string Operation)
{
    public override string ToString() => RecordIndex + ":" + string.Join('/', Path) + ":" + Operation;
}

public static class SourceMutationCases
{
    public static IEnumerable<object[]> Cases => SourceExtractionContractCases.All.SelectMany(For);

    public static IEnumerable<object[]> For(SourceExtractionCase test) =>
        test.ExpectedRecords.SelectMany((record, index) => Paths(record, [])
            .SelectMany(path => new[] { "omit", "wrong" }.Select(operation =>
                new object[] { test.Id, new SourceMutation(index, path, operation) })));

    private static IEnumerable<string[]> Paths(JsonNode? node, string[] prefix)
    {
        if (node is JsonObject obj)
        {
            foreach (var item in obj)
            {
                var path = prefix.Append(item.Key).ToArray();
                yield return path;
                foreach (var nested in Paths(item.Value, path)) yield return nested;
            }
        }
        else if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++)
                foreach (var nested in Paths(array[i], prefix.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray())) yield return nested;
    }

    public static ExtractionResult Apply(SourceExtractionCase test, SourceMutation mutation)
    {
        var correct = test.CorrectResult();
        JsonNode parent = correct.Records[mutation.RecordIndex];
        foreach (var segment in mutation.Path[..^1])
            parent = parent is JsonArray array ? array[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]! : parent[segment]!;
        var property = mutation.Path[^1];
        if (parent is JsonObject obj)
        {
            if (mutation.Operation == "omit") obj.Remove(property);
            else obj[property] = "__individually_wrong_mock__";
        }
        else throw new InvalidOperationException("Mutation paths terminate at asserted object properties.");
        return correct;
    }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceExtractionMockGateTests
{
    [Theory, MemberData(nameof(SourceExtractionContractCases.Cases), MemberType = typeof(SourceExtractionContractCases))]
    public void CorrectScriptedProviderPassesIdenticalProductionAssertions(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        var provider = new ScriptedSourceExtractor(test.Kind, _ => test.CorrectResult());
        SourceExtractionContractCases.AssertContract(test, provider);
        var received = Assert.Single(provider.Received);
        Assert.Equal(test.Document().RelativePath, received.RelativePath);
        Assert.Equal(test.Document().Context, received.Context);
        Assert.Equal(test.Document().Bytes.ToArray(), received.Bytes.ToArray());
    }

    [Theory, MemberData(nameof(SourceMutationCases.Cases), MemberType = typeof(SourceMutationCases))]
    public void EveryAssertedPropertyRejectsItsOwnOmissionAndWrongValue(string id, SourceMutation mutation)
    {
        var test = SourceExtractionContractCases.Get(id);
        var incorrect = SourceMutationCases.Apply(test, mutation);
        var provider = new ScriptedSourceExtractor(test.Kind, _ => incorrect);
        var rejection = Record.Exception(() => SourceExtractionContractCases.AssertContract(test, provider));
        Assert.NotNull(rejection);
        Assert.Single(provider.Received);
    }

    [Theory, MemberData(nameof(SourceExtractionContractCases.Cases), MemberType = typeof(SourceExtractionContractCases))]
    public void MissingAndDuplicatedRecordsAreRejected(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        var correct = test.CorrectResult();
        var missing = correct with { Records = correct.Records.Skip(1).ToArray() };
        var duplicated = correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() };
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => missing))));
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => duplicated))));
    }

    [Theory, MemberData(nameof(SourceExtractionContractCases.Cases), MemberType = typeof(SourceExtractionContractCases))]
    public void WrongStructuralStatusAndIssueMembershipAreRejected(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        var correct = test.CorrectResult();
        var wrongStatus = correct with { StructuralStatus = test.Status == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean };
        var unexpectedIssue = correct with { Issues = correct.Issues.Concat([new SourceIssue("unexpected", "Incorrect mock issue", null, null)]).ToArray() };
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => wrongStatus))));
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => unexpectedIssue))));
        if (correct.Issues.Count > 0)
        {
            var missingIssue = correct with { Issues = correct.Issues.Skip(1).ToArray() };
            var noReason = correct with { Issues = correct.Issues.Select(x => x with { Reason = "" }).ToArray() };
            var badRange = correct with { Issues = correct.Issues.Select(x => x with { StartByte = 0, EndByte = test.Document().Bytes.Length + 1 }).ToArray() };
            var incompleteRange = correct with { Issues = correct.Issues.Select(x => x with { StartByte = 0, EndByte = null }).ToArray() };
            foreach (var wrong in new[] { missingIssue, noReason, badRange, incompleteRange })
                Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => wrong))));
        }
    }

    [Theory, MemberData(nameof(SourceExtractionContractCases.Cases), MemberType = typeof(SourceExtractionContractCases))]
    public void InputMutationAndProviderFailureAreRejected(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        var mutating = new ScriptedSourceExtractor(test.Kind, source =>
        {
            Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var bytes));
            bytes.Array![bytes.Offset] ^= 1;
            return test.CorrectResult();
        });
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, mutating)));
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test,
            new ScriptedSourceExtractor(test.Kind, _ => throw new IOException("Scripted extraction failure")))));
    }

    [Theory, MemberData(nameof(SourceExtractionContractCases.OrderCases), MemberType = typeof(SourceExtractionContractCases))]
    public void RecordOrderAndSelectedDeclarationDetailsCannotBeLost(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        var correct = test.CorrectResult();
        var reordered = correct with { Records = correct.Records.Reverse().ToArray() };
        Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Kind, _ => reordered))));
    }

    [Fact]
    public void FixtureInventoryRetainsBoundaryCasesAndLegacyEvidence()
    {
        Assert.Equal(24, SourceExtractionContractCases.All.Count);
        Assert.Equal(20, SourceExtractionContractCases.All.Count(x => x.Kind == "aidl"));
        Assert.Equal(4, SourceExtractionContractCases.All.Count(x => x.Kind == "signature"));
        Assert.Equal(SourceExtractionContractCases.All.Count, SourceExtractionContractCases.All.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(SourceExtractionContractCases.All, x => Assert.False(string.IsNullOrWhiteSpace(x.Evidence)));
        Assert.Contains(SourceExtractionContractCases.All, x => x.Status == StructuralParseStatus.Malformed);
        Assert.Contains(SourceExtractionContractCases.All, x => x.Status == StructuralParseStatus.Unsupported);
        Assert.Contains(SourceExtractionContractCases.All, x => x.Status == StructuralParseStatus.Partial);
        Assert.Contains(SourceExtractionContractCases.All, x => x.ExpectedRecords.Any(r => r["extras"]?["declaration_depth"]?.GetValue<int>() == 2));
        Assert.Contains(SourceExtractionContractCases.All, x => x.ExpectedRecords.Any(r => r["extras"]?["interface_oneway"]?.GetValue<bool>() == true));
    }
}
