namespace AndroidMcp.BuildTool.Modules.Review;

/// <summary>Category dispatcher API. Dependencies remain explicit and injectable.</summary>
public sealed record ReviewOperationInput(ReviewSnapshot? Snapshot = null, PreparedReview? Prepared = null,
    ReviewCapability? Capability = null, string? ArtifactRoot = null, string? WorkingDirectory = null,
    ReviewRecoveryInput? Recovery = null, byte[]? Response = null, ReviewTerminal? Terminal = null, string? Marker = null);

public sealed class ReviewWorkflow(ReviewPrepareService prepare, ReviewLaunchService launch,
    ReviewRecoverService recover, ReviewAssessService assess)
{
    public async Task<object> ExecuteAsync(string operation, ReviewOperationInput input, CancellationToken cancellationToken = default)
    {
        static T Need<T>(T? value, string name) where T : class
            => value ?? throw new ReviewException("MISSING_PARAMETER", name);
        return operation.ToLowerInvariant() switch
        {
            "prepare" => await prepare.PrepareAsync(Need(input.Snapshot, "snapshot"), cancellationToken),
            "launch" => await launch.LaunchAsync(Need(input.Prepared, "prepared"), Need(input.Capability, "capability"),
                Need(input.ArtifactRoot, "artifact-root"), Need(input.WorkingDirectory, "working-directory"), cancellationToken),
            "recover" => recover.Recover(Need(input.Recovery, "recovery")),
            "assess" => assess.Assess(Need(input.Response, "response"), Need(input.Terminal, "terminal"), Need(input.Marker, "marker")),
            _ => throw new ReviewException("UNKNOWN_OPERATION", operation)
        };
    }
}
