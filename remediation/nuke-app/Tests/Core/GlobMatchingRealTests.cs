using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class GlobMatchingRealTests
{
    [Theory, MemberData(nameof(GlobMatchingCases.Cases), MemberType = typeof(GlobMatchingCases))]
    public void NativeMatcherUsesTheSameExplicitProfileVectors(string id) => GlobMatchingCases.AssertContract(GlobMatchingCases.Get(id), new GlobMatcher());
    [Fact]
    public void NativeMatcherRejectsTheSameUndefinedProfile() => GlobMatchingCases.AssertInvalidProfile(new GlobMatcher());
}
