using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaWordPolicyRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaWordPolicyCases.Cases),MemberType=typeof(JavaWordPolicyCases))]
    public void SameOriginalUnicodeWordPolicyBindsPinnedNativeVisitor(string id)=>
        JavaWordPolicyCases.AssertContract(JavaWordPolicyCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
