using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignPortableBuildPayloadStageMockTests
{
    public static IEnumerable<object[]> Cases=>new[]{"PACKAGE","UNION","EXPORT-UNION","VERIFY","MANIFEST","BEFORE-FAIL","AFTER-FAIL","PACKAGE-FAIL",
        "SOURCE-CHANGED","RENDER-FAIL","CANCEL","PAYLOAD-FAIL","PAYLOAD-SOURCE-CHANGED","PAYLOAD-COLLISION","PAYLOAD-UNSAFE","PAYLOAD-HOOK-MISSING"}.Select(scenario=>new object[]{scenario});
    [Theory,MemberData(nameof(Cases))]
    public Task CorrectPayloadCoverageConjunctionAndSourceHooksPass(string scenario)
    {var e=new DesignDocumentationStageEnvironment(scenario,true,true);return DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e));}
    [Theory]
    [InlineData("payload-omit")][InlineData("payload-bytes")][InlineData("payload-hook")]
    public async Task EachOmittedChangedPayloadOrFinalSourceHookIsRejected(string mutation)
    {var e=new DesignDocumentationStageEnvironment("PACKAGE",true,true);Assert.NotNull(await Record.ExceptionAsync(()=>DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e,mutation))));}
}
