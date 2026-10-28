using System.Text.RegularExpressions;
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
        if(!Enum.IsDefined(command.Phase)||!Regex.IsMatch(profile.Image,@"^[a-z0-9./_-]+@sha256:[a-f0-9]{64}$")||!Regex.IsMatch(context.UserIdentity,@"^[0-9]+:[0-9]+$")||new[]{context.WorkRoot,context.PlantUmlJar,context.ReceiptRoot}.Any(path=>!Path.IsPathFullyQualified(path)||path.Contains(',')||path.Contains('\0')))
            throw new ArchitectureAssetException("RENDERER_ARGUMENT","Renderer requires admitted immutable image, local absolute roots and numeric user identity");
        var args=new List<string>{"run","--rm","--pull=never","--network","none","--cap-drop","ALL","--security-opt","no-new-privileges","--user",context.UserIdentity,"--mount","type=bind,source="+context.WorkRoot+",target=/work","--mount","type=bind,source="+context.PlantUmlJar+",target=/tools/plantuml.jar,readonly","--workdir","/work","--entrypoint",command.Phase==ArchitectureRendererPhase.FontProbe?"fc-match":"java",profile.Image};
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
        var result=await runner.RunAsync(new(context.Docker,args,context.WorkRoot,new Dictionary<string,string>(),context.ReceiptRoot+"/"+command.Phase),cancellationToken);
        if(result.Cancelled)throw new OperationCanceledException(cancellationToken);
        if(result.ExitCode!=0)throw new ArchitectureAssetException("RENDERER_PROCESS","Native renderer failed; retained stderr: "+result.StandardErrorPath);
        return result;
    }
}
