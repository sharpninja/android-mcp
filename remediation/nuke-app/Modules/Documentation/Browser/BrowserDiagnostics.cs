using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("AndroidMcp.Build.Tests")]

namespace AndroidMcp.BuildTool.Documentation.Browser;

// Component-internal observation only. It is not a CDP command, scheduler,
// deadline or production policy. The ordinary constructor uses no observer.
internal interface ICdpLifecycleObserver
{
    ValueTask BeforeRegistrationAsync(int id,CancellationToken cancellationToken);
    void Terminal(Exception error);
}
internal interface ICdpClientDiagnostics
{
    int PendingCommands {get;}
}
