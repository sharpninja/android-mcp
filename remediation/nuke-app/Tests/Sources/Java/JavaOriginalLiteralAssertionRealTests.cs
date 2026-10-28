using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaOriginalLiteralAssertionRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    public static IEnumerable<object[]> Cases=>JavaOriginalLiteralAssertionCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void SameSixOriginalLiteralAssertionsBindExistingNativeCasesAndContexts(string id)
    {
        var test=JavaOriginalLiteralAssertionCases.Get(id);
        var rig=new JavaOriginalLiteralRig(test.AstId is null?null:new JavaSourceExtractor(fixture.Parser),test.ContextId is null?null:new JavaFileContext());
        JavaOriginalLiteralAssertionCases.AssertContract(test,rig);
    }
}
