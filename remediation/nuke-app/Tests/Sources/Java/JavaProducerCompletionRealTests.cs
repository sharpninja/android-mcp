using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerCompletionRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaProducerCompletionCases.Cases),MemberType=typeof(JavaProducerCompletionCases))]
    public void SameCompleteOriginalRowsAndReportsBindExistingPinnedVisitor(string id)=>
        JavaProducerCompletionCases.AssertContract(JavaProducerCompletionCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
