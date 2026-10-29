using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaCommentMetadataRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaCommentMetadataCases.Cases),MemberType=typeof(JavaCommentMetadataCases))]
    public void SameOriginalCommentMetadataBindsExistingNativeVisitor(string id)=>
        JavaCommentMetadataCases.AssertContract(JavaCommentMetadataCases.Find(id),new JavaSourceExtractor(fixture.Parser));
}
