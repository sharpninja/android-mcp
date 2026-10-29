using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HtmlProfileDecodeCase(string Id, string Input, string Text, string Attribute)
{
    public string Expected(SourceHtmlDecodingProfile profile) => profile switch
    {
        SourceHtmlDecodingProfile.Text => Text,
        SourceHtmlDecodingProfile.Attribute => Attribute,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
public sealed record HtmlProfileCall(string Text, SourceHtmlDecodingProfile Profile, bool ExplicitProfile);

public static class SourceHtmlProfileContractCases
{
    // Only policy discriminators. All existing31 text vectors remain unchanged regressions.
    public static readonly HtmlProfileDecodeCase[] All =
    [
        new("PROFILE-PREFIX", "&notit;", "\u00acit;", "&notit;"),
        new("PROFILE-EQUALS", "&copy=", "\u00a9=", "&copy="),
        new("PROFILE-NONEXACT", "&ampx", "&x", "&ampx"),
        new("PROFILE-EXACT-SEMICOLON", "&copy;=", "\u00a9=", "\u00a9="),
        new("PROFILE-EXACT-LEGACY", "&copy", "\u00a9", "\u00a9"),
        new("PROFILE-NUMERIC-EQUALS", "&#65=", "A=", "A=")
    ];
    public static IEnumerable<object[]> Cases() => All.SelectMany(test =>
        new[] { SourceHtmlDecodingProfile.Text, SourceHtmlDecodingProfile.Attribute }.Select(profile => new object[] { test.Id, profile }));
    public static IEnumerable<object[]> LegacyCases() => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> DifferentProfiles() => Cases().Where(row => Get((string)row[0]).Text != Get((string)row[0]).Attribute);
    public static HtmlProfileDecodeCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertDecode(HtmlProfileDecodeCase test, SourceHtmlDecodingProfile profile, IProfiledSourceHtmlDecoder provider)
    {
        Assert.Equal(test.Expected(profile), provider.Decode(test.Input, profile));
        if (profile == SourceHtmlDecodingProfile.Text) Assert.Equal(test.Text, provider.Decode(test.Input));
    }
    public static void AssertNullPrecondition(IProfiledSourceHtmlDecoder provider)
    {
        Assert.Throws<ArgumentNullException>(() => provider.Decode(null!));
        Assert.Throws<ArgumentNullException>(() => provider.Decode(null!, SourceHtmlDecodingProfile.Text));
        Assert.Throws<ArgumentNullException>(() => provider.Decode(null!, SourceHtmlDecodingProfile.Attribute));
    }
    public static void AssertUnknownProfile(IProfiledSourceHtmlDecoder provider)
    {
        foreach (var text in new[] { "", "&copy=" })
            Assert.Throws<ArgumentOutOfRangeException>(() => provider.Decode(text, (SourceHtmlDecodingProfile)777));
    }
}

public sealed class ScriptedProfileHtmlDecoder(Func<string, SourceHtmlDecodingProfile, string> handler,
    Func<string, string>? legacyHandler = null) : IProfiledSourceHtmlDecoder
{
    public List<HtmlProfileCall> Received { get; } = [];
    public string Decode(string text)
    {
        Received.Add(new(text, SourceHtmlDecodingProfile.Text, false));
        return legacyHandler is null ? handler(text, SourceHtmlDecodingProfile.Text) : legacyHandler(text);
    }
    public string Decode(string text, SourceHtmlDecodingProfile profile)
    {
        Received.Add(new(text, profile, true));
        return handler(text, profile);
    }
}
