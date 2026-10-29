using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaAnnotationFailureRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Fact]
    public void SameOriginalAnnotationFailurePropagatesFromPinnedNativeVisitor()=>
        JavaAnnotationFailureCases.AssertContract(new JavaSourceExtractor(fixture.Parser));
}
