using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSourceTypeFacts(Func<string, IReadOnlyDictionary<string, string>, string?> xml, Func<string, IReadOnlySet<string>> formals, Func<JsonObject, StructuralTypeHeader> headers) : IAnnotationTargetSourceFacts, IStructuralTypeHeaderSourceFacts
{
    public List<string> Calls { get; } = [];
    public string? XmlJvmKey(string itemName, IReadOnlyDictionary<string, string> owners) { Calls.Add("xml"); return xml(itemName, owners); }
    public IReadOnlySet<string> FormalNames(string raw) { Calls.Add("formals"); return formals(raw); }
    public StructuralTypeHeader StructuralHeader(JsonObject document) { Calls.Add("header"); return headers(document); }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceTypeFactsMockGateTests
{
    private static ScriptedSourceTypeFacts Xml(Func<string, IReadOnlyDictionary<string, string>, string?> response) => new(response, _ => throw new IOException("wrong concern"), _ => throw new IOException("wrong concern"));
    private static ScriptedSourceTypeFacts Formals(Func<string, IReadOnlySet<string>> response) => new((_, _) => throw new IOException("wrong concern"), response, _ => throw new IOException("wrong concern"));
    private static ScriptedSourceTypeFacts Header(Func<JsonObject, StructuralTypeHeader> response) => new((_, _) => throw new IOException("wrong concern"), _ => throw new IOException("wrong concern"), response);
    private static StructuralTypeHeader Correct(StructuralHeaderCase test) => new(test.Parameters?.DeepClone(), test.Supertypes.Select(x => (JsonObject)x.DeepClone()).ToArray());

    [Theory, MemberData(nameof(SourceTypeFactsContractCases.XmlCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void ExactLegacyXmlKeyMocksAndOppositeOutcomesProveActualBoundary(string id)
    {
        var test = SourceTypeFactsContractCases.Xml.Single(x => x.Id == id); var fake = Xml((item, owners) =>
        { Assert.Equal(test.Item, item); Assert.Same(SourceTypeFactsContractCases.Owners, owners); return test.Expected; });
        SourceTypeFactsContractCases.AssertXml(test, fake); Assert.Equal("xml", Assert.Single(fake.Calls));
        foreach (var wrong in test.Expected is null ? new[] { "invented" } : new[] { "wrong", null })
            Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertXml(test, Xml((_, _) => wrong))));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertXml(test, Xml((_, _) => throw new IOException("wrong failure")))));
    }

    [Theory, MemberData(nameof(SourceTypeFactsContractCases.FormalCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void FormalNameMocksRetainExactLegacyAnnotationFailureAndSetAssertions(string id)
    {
        var test = SourceTypeFactsContractCases.Formals.Single(x => x.Id == id); var fake = Formals(raw =>
        { Assert.Equal(test.Raw, raw); if (test.MalformedAnnotation) throw new FormatException("Unclosed annotation"); return new HashSet<string>(test.Names, StringComparer.Ordinal); });
        SourceTypeFactsContractCases.AssertFormals(test, fake); Assert.Equal("formals", Assert.Single(fake.Calls));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertFormals(test, Formals(_ => throw new IOException("wrong type")))));
        if (test.MalformedAnnotation)
        {
            Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertFormals(test, Formals(_ => new HashSet<string>()))));
            Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertFormals(test, Formals(_ => throw new FormatException("wrong reason")))));
        }
        else
        {
            Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertFormals(test, Formals(_ => new HashSet<string>(test.Names.Append("invented"))))));
            foreach (var name in test.Names) Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertFormals(test, Formals(_ => new HashSet<string>(test.Names.Where(x => x != name))))));
        }
    }

    [Theory, MemberData(nameof(SourceTypeFactsContractCases.HeaderCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void HeaderMocksProveExactSortedDedupeParametersAndNoInputMutation(string id)
    {
        var test = SourceTypeFactsContractCases.Headers.Single(x => x.Id == id); var fake = Header(_ => Correct(test));
        SourceTypeFactsContractCases.AssertHeader(test, fake); Assert.Equal("header", Assert.Single(fake.Calls));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => Correct(test) with { TypeParameters = JsonValue.Create("wrong") }))));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => Correct(test) with { Supertypes = test.Supertypes.Concat([new JsonObject { ["relation"] = "invented", ["text"] = "invented" }]).ToArray() }))));
        if (test.Supertypes.Length > 0) Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => Correct(test) with { Supertypes = [] }))));
        if (test.Supertypes.Length > 1) Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => Correct(test) with { Supertypes = test.Supertypes.Reverse().ToArray() }))));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(document => { document["wrong"] = true; return Correct(test); }))));
        Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => throw new IOException("scripted failure")))));
    }

    [Theory, MemberData(nameof(SourceTypeFactsContractCases.HeaderFieldControls), MemberType = typeof(SourceTypeFactsContractCases))]
    public void EverySupertypePropertyRejectsIndividualMissingAndWrongMocks(string id, int index, string property)
    {
        var test = SourceTypeFactsContractCases.Headers.Single(x => x.Id == id);
        foreach (var omit in new[] { true, false })
        {
            var wrong = Correct(test); if (omit) wrong.Supertypes[index].Remove(property); else wrong.Supertypes[index][property] = "wrong";
            Assert.NotNull(Record.Exception(() => SourceTypeFactsContractCases.AssertHeader(test, Header(_ => wrong))));
        }
    }
}
