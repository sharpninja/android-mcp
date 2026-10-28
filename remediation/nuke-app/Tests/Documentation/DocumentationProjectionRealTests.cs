using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationProjectionRealTests
{
    private static readonly Lazy<DocumentationProjection> Projection = new(()=>new DocumentationProjector().Project(DocumentationFixture.Current.Input));

    [Theory,MemberData(nameof(DocumentationProjectionAssertions.Cases),MemberType=typeof(DocumentationProjectionAssertions))]
    public void SameAssertionsAndIndependentFixturesBindNativeProjector(ProjectionAssertionCase test) => test.Assert(Projection.Value);

    [Fact]
    public void RepeatedNativeProjectionIsByteIdenticalAndDoesNotModifyInputs()
    {
        var fixture=DocumentationFixture.Current;
        var blueprintBefore=fixture.Input.BlueprintMarkdown.Bytes.ToArray();
        var designBefore=fixture.Input.DesignMarkdown.Bytes.ToArray();
        var repeated=new DocumentationProjector().Project(fixture.Input);
        Assert.Equal(Projection.Value.Artifacts.Select(a=>a.RelativePath),repeated.Artifacts.Select(a=>a.RelativePath));
        foreach(var artifact in repeated.Artifacts)
            Assert.True(DocumentationFixture.Get(Projection.Value,artifact.RelativePath).Bytes.Span.SequenceEqual(artifact.Bytes.Span),artifact.RelativePath);
        Assert.True(blueprintBefore.AsSpan().SequenceEqual(fixture.Input.BlueprintMarkdown.Bytes.Span));
        Assert.True(designBefore.AsSpan().SequenceEqual(fixture.Input.DesignMarkdown.Bytes.Span));
    }
}
