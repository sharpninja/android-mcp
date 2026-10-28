using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

internal enum ArchitectureDockerProgram {Java,FontMatch,ReadFile}

/// <summary>One local, isolated Docker argv envelope for admission and rendering.</summary>
internal static class ArchitectureDockerRequest
{
    public static ProcessRequest Create(DocumentationRendererProfile profile,ArchitectureRendererContext context,
        ArchitectureDockerProgram program,IReadOnlyList<string> programArguments,string receiptSuffix)
    {
        if(!Regex.IsMatch(profile.Image,@"^[a-z0-9./_-]+@sha256:[a-f0-9]{64}$")||!Regex.IsMatch(context.UserIdentity,@"^[0-9]+:[0-9]+$")||
            new[]{context.WorkRoot,context.PlantUmlJar,context.ReceiptRoot}.Any(path=>!Path.IsPathFullyQualified(path)||path.Contains(',')||path.Contains('\0')))
            throw new ArchitectureAssetException("RENDERER_ARGUMENT","Renderer requires admitted immutable image, local absolute roots and numeric user identity");
        var entrypoint=program switch{ArchitectureDockerProgram.Java=>"java",ArchitectureDockerProgram.FontMatch=>"fc-match",ArchitectureDockerProgram.ReadFile=>"cat",_=>throw new ArgumentOutOfRangeException(nameof(program))};
        var arguments=new List<string>{"--host","unix:///var/run/docker.sock","--config",context.WorkRoot+"/.docker-cli","run","--rm","--pull=never","--network","none","--cap-drop","ALL","--security-opt","no-new-privileges","--user",context.UserIdentity,"--mount","type=bind,source="+context.WorkRoot+",target=/work","--mount","type=bind,source="+context.PlantUmlJar+",target=/tools/plantuml.jar,readonly","--workdir","/work","--entrypoint",entrypoint,profile.Image};
        arguments.AddRange(programArguments);
        return new(context.Docker,arguments,context.WorkRoot,new Dictionary<string,string>(),context.ReceiptRoot+"/"+receiptSuffix,ClearEnvironment:true);
    }
}
