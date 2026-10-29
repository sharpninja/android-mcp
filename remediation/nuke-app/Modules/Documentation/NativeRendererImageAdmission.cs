using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Admits actual pinned image bytes, Java version and required font through shared native services.</summary>
public sealed class NativeRendererImageAdmission(IProcessRunner runner,IProcessCaptureReader captures,
    IArchitectureRendererTransport renderer,IContentHash hashes):IRendererImageAdmission
{
    public async Task<RendererImageAdmission> AdmitAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,
        CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requests=profile.RendererFiles.Select((pin,index)=>
        {
            if(!ArtifactPaths.IsRelative(pin.RelativePath))throw new ArchitectureAssetException("RENDERER_PIN","Image file must use its admitted relative path");
            return ArchitectureDockerRequest.Create(profile,context,ArchitectureDockerProgram.ReadFile,
                new[]{"/usr/local/structurizr-cli/"+pin.RelativePath},"IdentityFile"+index.ToString(CultureInfo.InvariantCulture));
        }).ToArray();
        var processes=new List<ProcessResult>();
        for(var index=0;index<requests.Length;index++)
        {
            var result=await runner.RunAsync(requests[index],cancellationToken);processes.Add(result);
            if(result.Cancelled)throw new OperationCanceledException(cancellationToken);
            if(result.ExitCode!=0)throw new ArchitectureAssetException("RENDERER_PROCESS","Image identity capture failed; retained stderr: "+result.StandardErrorPath);
            ReadOnlyMemory<byte> bytes;
            try{bytes=await captures.ReadAsync(result,false,cancellationToken);}
            catch(IOException error){throw new ArchitectureAssetException("RENDERER_PIN","Image binary capture unavailable: "+error.Message);}
            if(!string.Equals(hashes.Sha256(bytes.Span),profile.RendererFiles[index].Sha256,StringComparison.Ordinal))
                throw new ArchitectureAssetException("RENDERER_PIN","Image identity mismatch: "+profile.RendererFiles[index].RelativePath);
        }
        var java=await renderer.ExecuteAsync(profile,context,new(ArchitectureRendererPhase.JavaVersion,[]),cancellationToken);processes.Add(java);
        var javaText=Encoding.UTF8.GetString((await captures.ReadAsync(java,true,cancellationToken)).Span);
        var javaVersion=Regex.Match(javaText,@"(?m)^(?:openjdk|java) version ""([^""]+)""",RegexOptions.CultureInvariant);
        if(!javaVersion.Success||javaVersion.Groups[1].Value!=profile.JavaVersion)
            throw new ArchitectureAssetException("RENDERER_JAVA","Required pinned Java version: "+profile.JavaVersion);
        var font=await renderer.ExecuteAsync(profile,context,new(ArchitectureRendererPhase.FontProbe,[]),cancellationToken);processes.Add(font);
        var fontText=Encoding.UTF8.GetString((await captures.ReadAsync(font,false,cancellationToken)).Span);
        var family=Regex.Match(fontText,@":\s*""([^""]+)""",RegexOptions.CultureInvariant);
        if(!family.Success||family.Groups[1].Value!="DejaVu Sans")
            throw new ArchitectureAssetException("RENDERER_FONT","Required renderer font is missing: DejaVu Sans");
        return new(profile.Image,profile.JavaVersion,"DejaVu Sans",profile.RendererFiles.ToArray(),processes.ToArray());
    }
}
