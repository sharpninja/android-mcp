using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignLegacyReceiptStageMockTests
{
    public static IEnumerable<object[]> Cases=>new[]{"VERIFY","MANIFEST","PACKAGE","UNION","EXPORT-UNION","BEFORE-FAIL","AFTER-FAIL","PACKAGE-FAIL","SOURCE-CHANGED","RENDER-FAIL","CANCEL",
        "LEGACY-ARCH-MISSING","LEGACY-PUBLIC-COLLISION","LEGACY-ALIAS-COLLISION","LEGACY-MODEL-UNVALIDATED","LEGACY-MODULE-UNVALIDATED"}.Select(scenario=>new object[]{scenario});
    [Theory,MemberData(nameof(Cases))]
    public Task CorrectOptInReceiptAndManifestConjunctionsPass(string scenario)
    {var e=new DesignDocumentationStageEnvironment(scenario,true);return DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e));}
    [Fact]
    public Task DefaultConstructionKeepsTheExistingPackageUnion()
    {var e=new DesignDocumentationStageEnvironment("PACKAGE");Assert.False(e.LegacyReceiptsEnabled);return DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e));}
    [Theory]
    [InlineData("VERIFY","legacy-module")][InlineData("VERIFY","legacy-model")][InlineData("VERIFY","legacy-ui")]
    [InlineData("VERIFY","legacy-docs-before")][InlineData("VERIFY","legacy-docs-omit")][InlineData("VERIFY","legacy-phase")]
    [InlineData("PACKAGE","legacy-alias")][InlineData("PACKAGE","twice")][InlineData("PACKAGE","legacy-output-alias")]
    [InlineData("BEFORE-FAIL","before")][InlineData("AFTER-FAIL","after")]
    public async Task EachReceiptTimingCoverageValidationAndAliasFaultIsRejected(string scenario,string mutation)
    {var e=new DesignDocumentationStageEnvironment(scenario,true);Assert.NotNull(await Record.ExceptionAsync(()=>DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e,mutation))));}
}
