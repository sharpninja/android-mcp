using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record GlobMatchingCase(string Id, string Text, string Pattern, GlobMatchingProfile Profile, bool Expected);
public static class GlobMatchingCases
{
    // Expected behavior is pinned from legacy fnmatch._translate and glob.translate /
    // PurePosixPath.full_match TEXT, never obtained by running the legacy interpreter.
    public static IReadOnlyList<GlobMatchingCase> All { get; } =
    [
        new("PUBG-01", "p/A.aidl", "*.aidl", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-02", "p/sub/A.aidl", "p/*.aidl", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-03", "p/.hidden.aidl", "p/*.aidl", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-04", "", "*", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-05", "A", "a", GlobMatchingProfile.PublicationFilename, false),
        new("PUBG-06", "a\nb", "a?b", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-07", "a/b", "a?b", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-08", "[abc", "[abc", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-09", "b", "[a-c]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-10", "z", "[!a-c]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-11", "b", "[!a-c]", GlobMatchingProfile.PublicationFilename, false),
        new("PUBG-12", "]", "[]]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-13", "x", "[z-a]", GlobMatchingProfile.PublicationFilename, false),
        new("PUBG-14", "-", "[-a]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-15", "\\", "[\\]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-16", "^", "[^]", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-17", "abc", "a**c", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-18", "a/b/c", "**", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-19", "ab", "a", GlobMatchingProfile.PublicationFilename, false),
        new("PUBG-20", "a😀b", "a?b", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-21", "p/😀/literal.txt", "p/😀/literal.txt", GlobMatchingProfile.PublicationFilename, true),
        new("PUBG-22", "./p//literal.txt", "p/literal.txt", GlobMatchingProfile.PublicationFilename, false),
        new("POSG-01", "p/A.aidl", "*.aidl", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-02", "p/sub/A.aidl", "p/*.aidl", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-03", "p/A.aidl", "p/**/*.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-04", "p/sub/A.aidl", "p/**/*.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-05", "p/.hidden.aidl", "p/*.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-06", "p/test/T.aidl", "p/test/**", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-07", "p/A.aidl", "p/test/**", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-08", "", "*", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-09", "", "**", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-10", "p/A.aidl", "p/*/*.aidl", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-11", "p/a/b/A.aidl", "p/**/**/A.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-12", "p/A.aidl", "p/**/**/A.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-13", "a/b", "a?b", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-14", "a\\b", "a?b", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-15", "p/A.aidl", "./p//*.aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-16", "A", "a", GlobMatchingProfile.PosixFullPath, false),
        new("POSG-17", "p/b.aidl", "p/[a-c].aidl", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-18", "p/.a", "p/?a", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-19", "a\nb", "a?b", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-20", "a😀b", "a?b", GlobMatchingProfile.PosixFullPath, true),
        new("POSG-21", "./p//literal.txt", "p/literal.txt", GlobMatchingProfile.PosixFullPath, true)
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static GlobMatchingCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(GlobMatchingCase test, IGlobMatcher provider) => Assert.Equal(test.Expected, provider.Matches(test.Text, test.Pattern, test.Profile));
    public static void AssertInvalidProfile(IGlobMatcher provider) => Assert.Throws<ArgumentOutOfRangeException>(() => provider.Matches("a", "*", (GlobMatchingProfile)999));
}

public sealed class ScriptedGlobMatcher(Func<string, string, GlobMatchingProfile, bool> response) : IGlobMatcher
{
    public List<(string Text, string Pattern, GlobMatchingProfile Profile)> Calls { get; } = [];
    public bool Matches(string text, string pattern, GlobMatchingProfile profile) { Calls.Add((text, pattern, profile)); return response(text, pattern, profile); }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class GlobMatchingMockTests
{
    [Theory, MemberData(nameof(GlobMatchingCases.Cases), MemberType = typeof(GlobMatchingCases))]
    public void CorrectProfileMocksExerciseTheSameFutureMatcherAssertions(string id)
    {
        var test = GlobMatchingCases.Get(id); var fake = new ScriptedGlobMatcher((text, pattern, profile) =>
        { Assert.Equal(test.Text, text); Assert.Equal(test.Pattern, pattern); Assert.Equal(test.Profile, profile); return test.Expected; });
        GlobMatchingCases.AssertContract(test, fake); var call = Assert.Single(fake.Calls);
        Assert.Equal(test.Text, call.Text); Assert.Equal(test.Pattern, call.Pattern); Assert.Equal(test.Profile, call.Profile);
    }
    [Theory, MemberData(nameof(GlobMatchingCases.Cases), MemberType = typeof(GlobMatchingCases))]
    public void EveryOppositeOutcomeAndProviderFailureIsRejected(string id)
    {
        var test = GlobMatchingCases.Get(id);
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertContract(test, new ScriptedGlobMatcher((_, _, _) => !test.Expected))));
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertContract(test, new ScriptedGlobMatcher((_, _, _) => throw new IOException("scripted failure")))));
    }
    [Theory]
    [InlineData("PUBG-01", "POSG-01")]
    [InlineData("PUBG-02", "POSG-02")]
    [InlineData("PUBG-04", "POSG-08")]
    [InlineData("PUBG-07", "POSG-13")]
    public void ProfileConflationControlsAreIndividuallyRejected(string publication, string posix)
    {
        var a = GlobMatchingCases.Get(publication); var b = GlobMatchingCases.Get(posix);
        Assert.Equal(a.Text, b.Text); Assert.Equal(a.Pattern, b.Pattern); Assert.NotEqual(a.Expected, b.Expected);
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertContract(a, new ScriptedGlobMatcher((_, _, _) => b.Expected))));
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertContract(b, new ScriptedGlobMatcher((_, _, _) => a.Expected))));
    }
    [Fact]
    public void UnsupportedProfileFailureIsTypedAndMissingWrongFailuresAreRejected()
    {
        GlobMatchingCases.AssertInvalidProfile(new ScriptedGlobMatcher((_, _, _) => throw new ArgumentOutOfRangeException("profile")));
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertInvalidProfile(new ScriptedGlobMatcher((_, _, _) => false))));
        Assert.NotNull(Record.Exception(() => GlobMatchingCases.AssertInvalidProfile(new ScriptedGlobMatcher((_, _, _) => throw new IOException("wrong")))));
    }
}
