using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Composes typed tool acquisition; never implements transport, extraction or installation.</summary>
public sealed class DocumentationToolBootstrap:IDocumentationToolBootstrap
{
    private readonly IToolArtifactAcquirer tools;
    private readonly IDocumentationBootstrapFileEffects? fileEffects;
    public DocumentationToolBootstrap(IToolArtifactAcquirer tools):this(tools,null){ }
    public DocumentationToolBootstrap(IToolArtifactAcquirer tools,IDocumentationBootstrapFileEffects? fileEffects)
    {this.tools=tools;this.fileEffects=fileEffects;}
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? offlineGradleChecksumProof,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(access))throw new ArchitectureAssetException("BOOTSTRAP_ARGUMENT","Choose explicit online or offline tool access");
        if(access==ToolArtifactAccess.Offline&&string.IsNullOrWhiteSpace(offlineGradleChecksumProof))
            throw new ArchitectureAssetException("BOOTSTRAP_PROOF_REQUIRED","Offline Gradle bootstrap requires a retained published-checksum proof");
        if(access==ToolArtifactAccess.Offline&&!ArtifactPaths.IsRelative(offlineGradleChecksumProof))
            throw new ArchitectureAssetException("BOOTSTRAP_ARGUMENT","Offline proof must be a safe bound-store relative path");
        var requests=profile.Downloads.Select(pin=>
        {
            if(pin.SizeBytes is null||pin.SizeBytes<0||!ArtifactPaths.IsRelative(pin.LocalPath))throw new ArchitectureAssetException("BOOTSTRAP_ARGUMENT","Tool cache path and exact byte length must be admitted");
            var zip=pin.LocalPath==".tools/gradle-"+profile.GradleVersion+"-bin.zip";
            var bounded=new ToolArtifactBounds(pin.SizeBytes.Value,zip?512L*1024*1024:0,zip?128L*1024*1024:0,zip?10000:0);
            var redirects=profile.ToolRedirects is {} declared&&declared.TryGetValue(pin.LocalPath,out var allowed)?allowed:null;
            var artifact=new ToolArtifactPin(pin.LocalPath,pin.Revision,pin.Url,pin.Sha256,pin.SizeBytes.Value,zip?ToolArtifactFormat.Zip:ToolArtifactFormat.Stored,bounded,zip?new Uri(pin.Url.AbsoluteUri+".sha256"):null,redirects);
            return new ToolArtifactRequest(artifact,access,pin.LocalPath,zip?".tools":null,zip&&access==ToolArtifactAccess.Offline?offlineGradleChecksumProof:null);
        }).ToArray();
        var result=new List<ToolArtifactReceipt>();
        foreach(var request in requests){
            var receipt=await tools.AcquireAsync(request,cancellationToken);
            if(fileEffects is not null){
                if(request.Pin.Format==ToolArtifactFormat.Zip)await fileEffects.GradleExtractedAsync(profile,receipt,cancellationToken);
                if(request.Pin.Id=="gradle/wrapper/gradle-wrapper.jar")await fileEffects.WrappersAcquiredAsync(profile,cancellationToken);
            }
            result.Add(receipt);
        }
        return result;
    }
}
