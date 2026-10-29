using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapHookRealTests
{
    [Theory,MemberData(nameof(BootstrapHookCases.Cases),MemberType=typeof(BootstrapHookCases))]
    public Task SameOriginalOrderAndFailureCasesBindNativeBootstrapComposition(string scenario)
    {var e=new BootstrapHookEnvironment(scenario);return BootstrapHookCases.AssertContract(e,new DocumentationToolBootstrap(e.Tools,e.Effects));}
    [Fact]
    public void ExistingOneArgumentConstructorRemainsAvailableAtIlBoundary()=>Assert.NotNull(typeof(DocumentationToolBootstrap).GetConstructor([typeof(IToolArtifactAcquirer)]));
}
