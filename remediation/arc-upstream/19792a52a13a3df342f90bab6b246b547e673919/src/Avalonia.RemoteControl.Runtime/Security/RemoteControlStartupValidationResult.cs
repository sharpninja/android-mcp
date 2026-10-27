namespace Avalonia.RemoteControl.Server.Security;

/// <summary>
/// Represents validation results for remote-control server startup options.
/// </summary>
public sealed record RemoteControlStartupValidationResult
{
    /// <summary>
    /// Gets a value indicating whether startup is allowed.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets sanitized validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
