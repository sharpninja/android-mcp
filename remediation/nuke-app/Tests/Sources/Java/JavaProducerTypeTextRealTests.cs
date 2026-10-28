using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerTypeTextRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Fact]
    public void SameExplicitJavaLangSubpackageImportBindsExistingVisitor()=>
        JavaContractCases.AssertContract(JavaProducerTypeTextCases.Case,new JavaSourceExtractor(fixture.Parser));
}
