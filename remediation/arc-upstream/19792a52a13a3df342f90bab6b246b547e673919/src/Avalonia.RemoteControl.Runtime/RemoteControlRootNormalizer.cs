using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server;

internal static class RemoteControlRootNormalizer
{
    public static Control Normalize(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return TopLevel.GetTopLevel(root) ?? root;
    }
}
