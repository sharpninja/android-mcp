using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerDeclarationRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaProducerDeclarationCases.Cases),MemberType=typeof(JavaProducerDeclarationCases))]
    public void SameOriginalProducerDeclarationClausesBindExistingVisitor(string id)=>
        JavaProducerDeclarationCases.AssertContract(JavaProducerDeclarationCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
