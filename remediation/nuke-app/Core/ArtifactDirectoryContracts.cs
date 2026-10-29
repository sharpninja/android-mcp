namespace AndroidMcp.BuildTool.Core;

/// <summary>Creates one fresh portable directory and missing parents; existing targets are retained and refused.</summary>
public interface ICreateNewArtifactDirectory
{
    // Caller holds its admitted cooperating generation lease. This contract
    // does not promise atomic absence/creation against noncooperating actors.
    // Later failures retain the directory, including when it remains empty.
    Task CreateNewDirectoryAsync(string relativePath, CancellationToken cancellationToken = default);
}
