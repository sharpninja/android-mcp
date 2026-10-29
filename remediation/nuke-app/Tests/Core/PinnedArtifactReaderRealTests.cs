using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
public sealed class PinnedArtifactReaderRealTests
{
    [Theory,MemberData(nameof(PinnedArtifactReaderCases.All),MemberType=typeof(PinnedArtifactReaderCases))]
    public Task SameOneReadExactPinsAndOpaqueBytes(PinnedArtifactCase test)=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new PinnedArtifactReader(fixture.Store));
    [Theory,MemberData(nameof(PinnedArtifactMetadataCases.All),MemberType=typeof(PinnedArtifactMetadataCases))]
    public Task SameMalformedPinBeforeIoCases(PinnedArtifactCase test)=>PinnedArtifactReaderCases.AssertCase(test,fixture=>new PinnedArtifactReader(fixture.Store));
}
