using System.Collections.Generic;

namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Represents a safe, read-only property value exposed in a control snapshot.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="DeclaringType">The declaring type name.</param>
/// <param name="Value">The rendered property value, or a redaction marker.</param>
/// <param name="ValueType">The value type name.</param>
/// <param name="CanWrite">Whether the property exposes a public setter.</param>
/// <param name="IsRedacted">Whether the value was redacted by policy.</param>
/// <param name="IsEnum">Whether the property type is an enum.</param>
/// <param name="EnumValues">The enum values exposed by the property type.</param>
public sealed record RemoteControlPropertySnapshot(
    string Name,
    string DeclaringType,
    string Value,
    string ValueType,
    bool CanWrite,
    bool IsRedacted,
    bool IsEnum = false,
    IReadOnlyList<string>? EnumValues = null);
