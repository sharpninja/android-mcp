using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public enum ArchitectureRendererPhase { JavaVersion,FontProbe,RenderCode,ValidateWorkspace,ExportWorkspace,RenderNative }
public sealed record ArchitectureRendererContext(ToolIdentity Docker,string WorkRoot,string PlantUmlJar,
    string ReceiptRoot,string UserIdentity);
public sealed record ArchitectureRendererCommand(ArchitectureRendererPhase Phase,IReadOnlyList<string> SourcePaths);
public interface IArchitectureRendererTransport
{
    Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,
        ArchitectureRendererCommand command,CancellationToken cancellationToken=default);
}
