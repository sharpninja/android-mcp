using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignCompositionRealTests
{
    [Theory]
    [InlineData("ARCH-ONLINE")][InlineData("ARCH-OFFLINE")][InlineData("ARCH-MODULE-FAIL")][InlineData("ARCH-TOOL-FAIL")][InlineData("ARCH-RENDER-FAIL")][InlineData("PACKAGE-SUCCESS")][InlineData("PACKAGE-BUILD-FAIL")]
    public Task SameRecipeCasesBindNativeComposition(string scenario)
    {var e=new CompositionEnvironment(scenario);var services=new RecordingCompositionServices(e);return DesignCompositionAssertions.AssertRecipe(e,new DesignCompositionRecipeFactory(services,services,services,services,services,services));}
}
