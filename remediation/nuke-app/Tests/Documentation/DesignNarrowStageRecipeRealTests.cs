using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignNarrowStageRecipeRealTests
{
    [Theory,MemberData(nameof(DesignNarrowStageRecipeCases.Cases),MemberType=typeof(DesignNarrowStageRecipeCases))]
    public Task SameScopeTimingAndFailureCasesBindExistingFactoryOverload(DesignNarrowRecipeCase test)
    {
        var e=new DesignNarrowRecipeEnvironment(test);var services=new RecordingNarrowStageServices(e);
        var packages=new RecordingCompositionServices(e.Original.Architecture.Inner.Composition);
        var factory=new DesignCompositionRecipeFactory(services,services,services,services,services,packages,services,
            null,null,null,null,services);
        return DesignNarrowStageRecipeCases.AssertRecipe(e,factory.Architecture);
    }
}
