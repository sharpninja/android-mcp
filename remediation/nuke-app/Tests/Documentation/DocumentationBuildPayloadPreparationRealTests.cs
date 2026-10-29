using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadPreparationRealTests
{
    [Theory,MemberData(nameof(BuildPayloadPreparationCases.Cases),MemberType=typeof(BuildPayloadPreparationCases))]
    public Task SameCaptureChildRuntimeAndRecheckAssertionsBindNativePreparation(string scenario)
    {
        var e=new BuildPayloadPreparationEnvironment(scenario);var services=new BuildPayloadPreparationServices(e);
        var factory=new DocumentationBuildPayloadPreparationFactory(e.Run,e.Input.NativeConfiguration,e.Input.PrerequisiteScopes,
            services,new BuildPayloadPreparationReader(e,false),new BuildPayloadPreparationReader(e,true),services,new CanonicalJson(),services,e.Generation);
        return BuildPayloadPreparationCases.AssertContract(e,factory);
    }
}
