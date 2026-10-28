using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererRedirectProfileRealTests
{
    [Theory,MemberData(nameof(RendererRedirectCases.Data),MemberType=typeof(RendererRedirectCases))]
    public void SameRedirectReaderCasesBindNativeReader(RendererProfileCase test)=>RendererRedirectCases.AssertReader(test,new DocumentationRendererProfileReader());
}
[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererRedirectBootstrapRealTests
{
    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public Task SameDeclaredRedirectCasesBindNativeBootstrap(bool redirected,bool empty)
    {var e=new RedirectBootstrapEnvironment(redirected,empty);return RedirectBootstrapAssertions.AssertContract(e,new DocumentationToolBootstrap(new RecordingRedirectToolAcquirer(e)));}
}
