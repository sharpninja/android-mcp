using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class StandaloneReceiptCallerRealTests
{
    private static DocumentationLegacyReceipts Receipts()=>new(new CanonicalJson(),new StrictJsonInspector(),new ArtifactReceiptVerifier(new NativeContentHash()));
    [Theory,MemberData(nameof(StandaloneNarrowReceiptCases.Cases),MemberType=typeof(StandaloneNarrowReceiptCases))]
    public Task SameNarrowScopeFailureAndReceiptAssertionsBindNativeFactory(DesignNarrowRecipeCase test)
    {
        var e=new DesignNarrowRecipeEnvironment(test,true);var services=new RecordingNarrowStageServices(e);
        var packages=new RecordingCompositionServices(e.Original.Architecture.Inner.Composition);
        var factory=new DesignCompositionRecipeFactory(services,services,services,services,services,packages,services,
            uiProjector:services,legacyReceipts:Receipts());
        return DesignNarrowStageRecipeCases.AssertRecipe(e,factory.Architecture);
    }
    [Theory,MemberData(nameof(StandaloneModelReceiptCases.Cases),MemberType=typeof(StandaloneModelReceiptCases))]
    public Task SameCapturedScopeRecheckAndReceiptAssertionsBindNativeFactory(string scenario)
    {
        var e=new FullArchitectureRecipeEnvironment(scenario,true);var services=new RecordingFullArchitectureServices(e);
        var composition=new RecordingCompositionServices(e.Architecture.Inner.Composition);
        var factory=new DesignCompositionRecipeFactory(services,new ModuleGraphValidator(),services,services,services,composition,
            services,legacyReceipts:Receipts());
        return FullArchitectureRecipeCases.AssertRecipe(e,factory);
    }
}
