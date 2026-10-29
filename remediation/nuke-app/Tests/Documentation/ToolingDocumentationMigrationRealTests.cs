using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ToolingDocumentationMigrationRealTests
{
    [Fact]
    public void CanonicalSourceDataBindsTheSameIndependentSnapshotAssertions()=>ToolingDocumentationMigrationCases.AssertSnapshot(()=>
    {
        // Preserve the reviewed workstation-migration snapshot assertions when
        // current presentation-only source data is promoted separately.
        var fixture=DocumentationFixture.Current;
        return ToolingDocumentationMigrationCases.Files.Select(file=>new Artifact(file.CanonicalPath,
            fixture.ReadOriginalDesignBytes(file.CanonicalPath),file.MediaType)).ToArray();
    });
}
