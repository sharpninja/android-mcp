using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureTransportRealTests
{
    [Theory,MemberData(nameof(ArchitectureTransportCases.Data),MemberType=typeof(ArchitectureTransportCases))]
    public Task SameNativeArgvCasesBindTransport(ArchitectureTransportCase test)
    {var environment=new ArchitectureTransportEnvironment(test);return ArchitectureTransportCases.AssertContract(test,environment,new ArchitectureRendererTransport(new RecordingArchitectureRunner(environment)));}
}
