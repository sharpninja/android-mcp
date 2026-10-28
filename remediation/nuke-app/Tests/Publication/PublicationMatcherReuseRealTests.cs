using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationMatcherReuseRealTests
{
    [Theory,MemberData(nameof(PublicationMatcherReuseCases.Cases),MemberType=typeof(PublicationMatcherReuseCases))]
    public void SameScaleProfileAndReuseCasesBindNativeSharedMatcher(PublicationMatcherCase c)=>
        PublicationMatcherReuseCases.AssertCase(c,m=>new PublicationChecker(new CanonicalJson(),m),nativeMatcher:true);
}
