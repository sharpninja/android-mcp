using System.Globalization;

namespace Avalonia.RemoteControl.Server.Logging;

internal sealed class RemoteControlLogRedactor
{
    private readonly IReadOnlyCollection<string> sensitiveNameFragments;

    public RemoteControlLogRedactor(IEnumerable<string> sensitiveNameFragments)
    {
        this.sensitiveNameFragments = sensitiveNameFragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
            .Select(fragment => fragment.Trim())
            .ToArray();
    }

    public string RedactText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return ContainsSensitiveFragment(value) ? "[redacted]" : value;
    }

    public string RedactStructuredValue(string key, object? value)
    {
        if (ContainsSensitiveFragment(key))
        {
            return "[redacted]";
        }

        return RedactText(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private bool ContainsSensitiveFragment(string value)
    {
        return sensitiveNameFragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
