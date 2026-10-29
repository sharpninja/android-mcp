using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignFullArchitectureRecipeRealTests
{
    [Theory,MemberData(nameof(FullArchitectureRecipeCases.Cases),MemberType=typeof(FullArchitectureRecipeCases))]
    public Task SameCapturedModelUnionAndCommitCasesBindNativeFactory(string scenario)
    {
        var e=new FullArchitectureRecipeEnvironment(scenario);var services=new RecordingFullArchitectureServices(e);
        var legacy=new RecordingCompositionServices(e.Architecture.Inner.Composition);
        return FullArchitectureRecipeCases.AssertRecipe(e,new DesignCompositionRecipeFactory(services,legacy,services,services,services,legacy,services));
    }
}
