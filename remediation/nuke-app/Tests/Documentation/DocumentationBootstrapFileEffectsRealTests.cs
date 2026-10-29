using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapFileEffectsRealTests
{
    [Theory,MemberData(nameof(BootstrapFileCases.Cases),MemberType=typeof(BootstrapFileCases))]
    public Task SameOriginalEntryModePinAndPropertyCasesBindNativeModuleHelper(string scenario)
    {var e=new BootstrapFileEnvironment(scenario);return BootstrapFileCases.AssertContract(e,new DocumentationBootstrapFileEffects(e,e,e,e.Properties));}
}
