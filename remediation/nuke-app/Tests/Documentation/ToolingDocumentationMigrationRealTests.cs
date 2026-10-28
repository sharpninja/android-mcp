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
        var designRoot=Path.GetFullPath("../../design-c4",ToolingDocumentationMigrationCases.Root);
        return ToolingDocumentationMigrationCases.Files.Select(file=>new Artifact(file.CanonicalPath,
            File.ReadAllBytes(Path.Combine(designRoot,file.CanonicalPath)),file.MediaType)).ToArray();
    });
}
