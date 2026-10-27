namespace Avalonia.RemoteControl.Server.Commands;

/// <summary>
/// Represents the sanitized result of a remote command.
/// </summary>
/// <param name="Succeeded">Whether the command succeeded.</param>
/// <param name="Message">A sanitized result message.</param>
public sealed record RemoteControlCommandResult(bool Succeeded, string Message);
