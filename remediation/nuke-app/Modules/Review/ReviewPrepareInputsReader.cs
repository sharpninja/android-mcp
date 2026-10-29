using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewPrepareRequest(StoredArtifact Snapshot, StoredArtifact GitIdentity,
    StoredArtifact GhIdentity, StoredArtifact PrerequisiteConfiguration);
public sealed record ReviewPrepareInputs(ReviewSnapshot Snapshot, ToolIdentity Git, ToolIdentity Gh,
    ReviewPrerequisiteConfiguration Prerequisites, IReadOnlyList<StoredArtifact> InputPins);

/// <summary>Decode only independently pinned control bytes. Producer authority remains a composition prerequisite.</summary>
public sealed class ReviewPrepareInputsReader(IPinnedArtifactReader reader, ICanonicalJson canonical)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 64
    };

    public async Task<ReviewPrepareInputs> ReadAsync(StoredArtifact requestPin, string workspacePath,
        CancellationToken cancellationToken = default)
    {
        async Task<T> Read<T>(StoredArtifact pin) where T : class
        {
            Require(pin is not null && pin.MediaType == "application/json" && ArtifactPaths.IsRelative(pin.RelativePath));
            ReviewPrepareService.PublicPayload(pin!.RelativePath, "");
            ReadOnlyMemory<byte> bytes;
            try { bytes = await reader.ReadAsync(pin!, cancellationToken); }
            catch (FileNotFoundException) { throw Invalid(); }
            try
            {
                var strict = canonical.Serialize(bytes);
                ReviewPrepareService.PublicPayload("", ReviewPrepareService.Utf8.GetString(strict));
                return JsonSerializer.Deserialize<T>(strict, Options) ?? throw Invalid();
            }
            catch (Exception error) when (error is JsonException or CanonicalJsonException or NotSupportedException)
            { throw Invalid(); }
        }

        var request = await Read<ReviewPrepareRequest>(requestPin);
        var snapshot = await Read<ReviewSnapshot>(request.Snapshot);
        Require(snapshot.UnitProjects is not null && snapshot.IntegrationProjects is not null &&
            snapshot.RequirementIds is not null && snapshot.Evidence is not null &&
            snapshot.UnitProjects.All(Nonempty) && snapshot.IntegrationProjects.All(Nonempty) &&
            snapshot.RequirementIds.All(Nonempty) && snapshot.Evidence.All(x => x is not null &&
                Nonempty(x.Path) && Nonempty(x.Sha256) && Nonempty(x.Kind)) &&
            Nonempty(snapshot.RunId) && Nonempty(snapshot.ParentSessionId) && Nonempty(snapshot.ParentRequestId) &&
            Nonempty(snapshot.Head) && Nonempty(snapshot.PullRequestUrl));
        var git = await Read<ToolIdentity>(request.GitIdentity);
        ValidateTool(git);
        var gh = await Read<ToolIdentity>(request.GhIdentity);
        ValidateTool(gh);
        var configuration = await Read<ReviewPrerequisiteConfiguration>(request.PrerequisiteConfiguration);
        Require(Nonempty(configuration.WorkspacePath) && Path.IsPathRooted(configuration.WorkspacePath) && Nonempty(configuration.PluginName) &&
            Nonempty(configuration.PluginVersion) && configuration.ScopeIntegrationProjects is not null &&
            configuration.ScopeIntegrationProjects.All(Nonempty));
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (!comparer.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuration.WorkspacePath))))
            throw new ReviewException("PREPARE_WORKSPACE_MISMATCH", "Prerequisite workspace differs from the selected project root.");
        foreach (var evidence in new[] { configuration.EvaluationReceipt, configuration.RequirementsReceipt, configuration.ChecklistReceipt })
        {
            Require(evidence is not null && ArtifactPaths.IsRelative(evidence.Path) && IsHash(evidence.Sha256));
            ReviewPrepareService.PublicPayload(evidence!.Path, "");
        }
        return new(snapshot, git, gh, configuration,
            [requestPin, request.Snapshot, request.GitIdentity, request.GhIdentity, request.PrerequisiteConfiguration]);
    }

    private static bool Nonempty(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool IsHash(string? value) => value is not null && Regex.IsMatch(value, "^[a-f0-9]{64}$");
    private static void ValidateTool(ToolIdentity tool) => Require(Nonempty(tool.ExecutablePath) &&
        Path.IsPathRooted(tool.ExecutablePath) && IsHash(tool.Sha256) && Nonempty(tool.Version));
    private static void Require(bool value) { if (!value) throw Invalid(); }
    private static ReviewException Invalid() => new("INVALID_PREPARE_INPUT", "Pinned Review/Prepare control input is invalid.");
}
