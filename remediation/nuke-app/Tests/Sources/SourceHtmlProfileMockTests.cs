using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceHtmlProfileMockTests
{
    [Theory, MemberData(nameof(SourceHtmlProfileContractCases.Cases), MemberType = typeof(SourceHtmlProfileContractCases))]
    public void CorrectMocksReceiveExactInputAndSelectedProfile(string id, SourceHtmlDecodingProfile profile)
    {
        var test = SourceHtmlProfileContractCases.Get(id);
        var provider = new ScriptedProfileHtmlDecoder((text, selected) => { Assert.Equal(test.Input, text); return test.Expected(selected); });
        SourceHtmlProfileContractCases.AssertDecode(test, profile, provider);
        Assert.Equal(new HtmlProfileCall(test.Input, profile, true), provider.Received[0]);
        if (profile == SourceHtmlDecodingProfile.Text)
            Assert.Equal(new[] { new HtmlProfileCall(test.Input, profile, true), new HtmlProfileCall(test.Input, profile, false) }, provider.Received);
        else Assert.Single(provider.Received);
    }
    [Theory, MemberData(nameof(SourceHtmlProfileContractCases.Cases), MemberType = typeof(SourceHtmlProfileContractCases))]
    public void EveryExpectedOutputHasAnIndependentWrongMock(string id, SourceHtmlDecodingProfile profile)
    {
        var test = SourceHtmlProfileContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertDecode(test, profile,
            new ScriptedProfileHtmlDecoder((_, selected) => test.Expected(selected) + " wrong"))));
    }
    [Theory, MemberData(nameof(SourceHtmlProfileContractCases.DifferentProfiles), MemberType = typeof(SourceHtmlProfileContractCases))]
    public void WrongProfileRoutingCannotPassTheDiscriminatingVectors(string id, SourceHtmlDecodingProfile profile)
    {
        var test = SourceHtmlProfileContractCases.Get(id);
        var other = profile == SourceHtmlDecodingProfile.Text ? SourceHtmlDecodingProfile.Attribute : SourceHtmlDecodingProfile.Text;
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertDecode(test, profile,
            new ScriptedProfileHtmlDecoder((_, _) => test.Expected(other)))));
    }
    [Theory, MemberData(nameof(SourceHtmlProfileContractCases.LegacyCases), MemberType = typeof(SourceHtmlProfileContractCases))]
    public void TypedTextSuccessCannotHideChangedOneArgumentSemantics(string id)
    {
        var test = SourceHtmlProfileContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertDecode(test, SourceHtmlDecodingProfile.Text,
            new ScriptedProfileHtmlDecoder((_, selected) => test.Expected(selected), _ => "wrong legacy text"))));
    }
    [Fact]
    public void NullIsRejectedInBothProfilesAndTheExistingOverload()
    {
        SourceHtmlProfileContractCases.AssertNullPrecondition(new ScriptedProfileHtmlDecoder((text, _) => { ArgumentNullException.ThrowIfNull(text); return text; }));
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertNullPrecondition(new ScriptedProfileHtmlDecoder((_, _) => "omitted failure"))));
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertNullPrecondition(new ScriptedProfileHtmlDecoder((_, _) => throw new FormatException("wrong caller failure")))));
    }
    [Fact]
    public void UnknownProfileIsRejectedBeforeAnyReplacement()
    {
        SourceHtmlProfileContractCases.AssertUnknownProfile(new ScriptedProfileHtmlDecoder((text, selected) =>
        {
            if (!Enum.IsDefined(selected)) throw new ArgumentOutOfRangeException(nameof(selected));
            return text;
        }));
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertUnknownProfile(new ScriptedProfileHtmlDecoder((text, _) => text))));
        Assert.NotNull(Record.Exception(() => SourceHtmlProfileContractCases.AssertUnknownProfile(new ScriptedProfileHtmlDecoder((_, _) => throw new FormatException("wrong profile failure")))));
    }
}
