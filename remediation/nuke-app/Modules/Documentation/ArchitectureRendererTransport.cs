using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Native Docker/Java argv only. Renderer identity admission precedes this transport.</summary>
public sealed class ArchitectureRendererTransport(IProcessRunner runner):IArchitectureRendererTransport
{
    public async Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,ArchitectureRendererCommand command,CancellationToken cancellationToken=default)
    {
        var render=command.Phase is ArchitectureRendererPhase.RenderCode or ArchitectureRendererPhase.RenderNative;
        var prefix=command.Phase==ArchitectureRendererPhase.RenderCode?"build/code-views/":"build/structurizr-profiled/";
        if(render&&(command.SourcePaths.Count==0||command.SourcePaths.Any(path=>!ArtifactPaths.IsRelative(path)||!path.StartsWith(prefix,StringComparison.Ordinal)||!path.EndsWith(".puml",StringComparison.Ordinal)))||!render&&command.SourcePaths.Count>0)
            throw new ArchitectureAssetException("RENDERER_ARGUMENT","Renderer source paths do not match the selected phase");
        if(!Enum.IsDefined(command.Phase))throw new ArchitectureAssetException("RENDERER_ARGUMENT","Unknown renderer phase");
        var args=new List<string>();
        switch(command.Phase)
        {
            case ArchitectureRendererPhase.JavaVersion:args.Add("-version");break;
            case ArchitectureRendererPhase.FontProbe:args.Add("DejaVu Sans");break;
            case ArchitectureRendererPhase.RenderCode:
            case ArchitectureRendererPhase.RenderNative:
                args.AddRange(["-Djava.awt.headless=true","-jar","/tools/plantuml.jar","-tsvg","-nometadata","-charset","UTF-8"]);args.AddRange(command.SourcePaths.Select(path=>"/work/"+path));break;
            case ArchitectureRendererPhase.ValidateWorkspace:
            case ArchitectureRendererPhase.ExportWorkspace:
                args.AddRange(["-cp","/usr/local/structurizr-cli:/usr/local/structurizr-cli/lib/*","com.structurizr.cli.StructurizrCliApplication",command.Phase==ArchitectureRendererPhase.ValidateWorkspace?"validate":"export","-workspace","/work/architecture/workspace.dsl"]);
                if(command.Phase==ArchitectureRendererPhase.ExportWorkspace)args.AddRange(["-format","plantuml","-output","/work/build/structurizr-export"]);break;
            default:throw new ArchitectureAssetException("RENDERER_ARGUMENT","Unknown renderer phase");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var request=ArchitectureDockerRequest.Create(profile,context,command.Phase==ArchitectureRendererPhase.FontProbe?ArchitectureDockerProgram.FontMatch:ArchitectureDockerProgram.Java,args,command.Phase.ToString());
        var result=await runner.RunAsync(request,cancellationToken);
        if(result.Cancelled)throw new OperationCanceledException(cancellationToken);
        if(result.ExitCode!=0)throw new ArchitectureAssetException("RENDERER_PROCESS","Native renderer failed; retained stderr: "+result.StandardErrorPath);
        return result;
    }
}
