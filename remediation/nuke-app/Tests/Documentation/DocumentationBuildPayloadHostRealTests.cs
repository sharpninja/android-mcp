using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadHostRealTests
{
    [Theory,MemberData(nameof(BuildPayloadHostCases.Cases),MemberType=typeof(BuildPayloadHostCases))]
    public Task SamePinnedConfigurationAssertionsBindNativeReader(BuildPayloadHostCase test)
    {var trace=new List<string>();return BuildPayloadHostCases.AssertContract(test,new DocumentationBuildPayloadHostReader(new BuildPayloadHostPinnedReader(test,trace),new StrictJsonInspector()).ReadAsync,trace);}
}
