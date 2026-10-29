using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaAnnotationOffsetRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaAnnotationOffsetCases.Cases),MemberType=typeof(JavaAnnotationOffsetCases))]
    public void SameOriginalAnnotationCodePointOffsetsBindExistingNativeVisitor(string id)=>
        JavaAnnotationOffsetCases.AssertContract(JavaAnnotationOffsetCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
