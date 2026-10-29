using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog.Migration;

public sealed record CatalogMigrationResult(StoredArtifact Source,StoredArtifact Destination,StoredArtifact RetainedStage,
    IReadOnlyList<StoredArtifact> TargetMigrations);
public interface ICatalogCopyMigration
{
    Task<CatalogMigrationResult> MigrateAsync(string source,string destination,CancellationToken cancellationToken=default);
}
