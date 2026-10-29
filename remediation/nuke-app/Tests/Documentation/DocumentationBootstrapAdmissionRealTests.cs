using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapAdmissionRealTests
{
    [Theory,MemberData(nameof(LatePropertiesCases.Cases),MemberType=typeof(LatePropertiesCases))]
    public Task SameOriginalPropertiesReadBoundaryBindsNativeEffects(string scenario)
    {var e=new LatePropertiesEnvironment(scenario);return LatePropertiesCases.AssertContract(e,new DocumentationBootstrapFileEffects(e.Effects,e.Effects,e.Validator,e.Read));}
    [Theory,MemberData(nameof(AdmittedToolsCases.Cases),MemberType=typeof(AdmittedToolsCases))]
    public Task SameProfileAccessProofAndRetainedPinCasesBindNativeReuseFacade(string scenario)
    {var e=new AdmittedToolsEnvironment(scenario);return AdmittedToolsCases.AssertContract(e,new DocumentationAdmittedToolBootstrap(e.Original.Profile,Modules.Sources.Acquisition.ToolArtifactAccess.Offline,"proofs/gradle.json",e.Receipts,e));}
}
