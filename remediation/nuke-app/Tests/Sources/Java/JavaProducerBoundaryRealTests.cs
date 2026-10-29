using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerBoundaryRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaProducerBoundaryCases.Cases),MemberType=typeof(JavaProducerBoundaryCases))]
    public void SameOriginalProducerBoundaryContractsBindPinnedNativeVisitor(string id)=>
        JavaProducerBoundaryCases.AssertContract(JavaProducerBoundaryCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
