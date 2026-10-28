using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationParsedProjectionRealTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void NativeOverloadBindsTheSameSharedModelAssertions(bool opaqueBlueprint)
    {
        var e=new ParsedProjectionEnvironment(opaqueBlueprint);
        ParsedProjectionAssertions.AssertContract(e,new DocumentationProjector(e.Reader));
    }
}
