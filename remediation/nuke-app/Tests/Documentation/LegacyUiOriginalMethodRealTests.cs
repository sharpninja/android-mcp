using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyUiOriginalMethodRealTests
{
    private static readonly Lazy<DocumentationProjection> Native=new(()=>new DocumentationProjector().Project(DocumentationFixture.Current.Input));
    [Theory,MemberData(nameof(LegacyUiOriginalMethodMockTests.Methods),MemberType=typeof(LegacyUiOriginalMethodMockTests))]
    public void SameExactOriginalFileMethodAssertionsBindNativeProjector(string method)=>LegacyUiOriginalMethods.AssertMethod(method,Native.Value);
}
