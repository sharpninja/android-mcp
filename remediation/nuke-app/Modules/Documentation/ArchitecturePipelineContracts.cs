using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record RendererImageAdmission(string Image,string JavaVersion,string FontFamily,
    IReadOnlyList<RendererFilePin> VerifiedFiles,IReadOnlyList<ProcessResult> Processes);
public interface IRendererImageAdmission
{
    Task<RendererImageAdmission> AdmitAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,
        CancellationToken cancellationToken=default);
}
