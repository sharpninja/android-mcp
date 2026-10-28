using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationModelPreflightRealTests
{
    [Theory,MemberData(nameof(DocumentationModelPreflightCases.Cases),MemberType=typeof(DocumentationModelPreflightCases))]
    public void NativeCompositionBindsEverySharedModelMockCase(string scenario)
    {
        var e=new ModelPreflightEnvironment(scenario);var services=new RecordingModelPreflightServices(e);
        DocumentationModelPreflightCases.AssertContract(e,new DocumentationModelPreflight(services,services,services,services,services));
    }
}
