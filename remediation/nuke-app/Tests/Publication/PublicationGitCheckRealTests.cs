using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationGitCheckRealTests
{
    [Theory,MemberData(nameof(PublicationGitCheckCases.Cases),MemberType=typeof(PublicationGitCheckCases))]
    public Task SameReadOnlyCasesBindNativeOrchestration(GitCheckCase c)=>
        PublicationGitCheckCases.AssertCase(c,h=>new PublicationGitCheck(h,h,h,h));
}
