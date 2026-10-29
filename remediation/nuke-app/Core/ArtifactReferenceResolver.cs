using System.Net;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Core;

// Pure logical reference handling. ArtifactPaths remains the authority for
// subsequent filesystem admission; this helper never probes the host tree.
public sealed class ArtifactReferenceResolver(Func<string,string> decodeHtml):IArtifactReferenceResolver
{
    private static readonly Regex Scheme=new("^[A-Za-z][A-Za-z0-9+.-]*:",RegexOptions.CultureInvariant);
    private static readonly Regex Drive=new("^[A-Za-z]:",RegexOptions.CultureInvariant);
    private static readonly Regex GradleRemote=new("^(?:https?:)?//",RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
    public ArtifactReferenceResolution Resolve(ArtifactReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(!Enum.IsDefined(request.Profile))return Invalid("unknown-profile");
        if(!ArtifactPaths.IsRelative(request.SourcePath))return Invalid("invalid-base");
        ArgumentNullException.ThrowIfNull(request.Reference);
        if(request.Profile is ArtifactReferenceProfile.GradleOfflineAsset or ArtifactReferenceProfile.RepositoryOfflineAsset)
        {
            var repository=request.Profile==ArtifactReferenceProfile.RepositoryOfflineAsset;
            var asset=repository?decodeHtml(request.Reference):request.Reference;
            var remote=repository?asset.StartsWith("https:",StringComparison.Ordinal)||asset.StartsWith("http:",StringComparison.Ordinal)||asset.StartsWith("//",StringComparison.Ordinal):GradleRemote.IsMatch(asset);
            return remote?new(ArtifactReferenceDisposition.Remote,null,false,"offline-external"):Ignored("asset-not-remote");
        }
        var repositoryMarkdown=request.Profile==ArtifactReferenceProfile.RepositoryMarkdown;
        var target=repositoryMarkdown?Fragment(request.Reference.Trim('<','>')):Fragment(request.Reference).Trim('<','>');
        if(target.Length==0)return Ignored("empty");
        if(Drive.IsMatch(target))return Invalid("windows-drive");
        if(repositoryMarkdown?Scheme.IsMatch(target):target.Contains("://",StringComparison.Ordinal)||target.StartsWith("data:",StringComparison.Ordinal))return Ignored("uri");
        if(repositoryMarkdown)target=WebUtility.UrlDecode(target.Replace("+","%2B",StringComparison.Ordinal));
        if(Drive.IsMatch(target))return Invalid("windows-drive");
        if(target.StartsWith("/",StringComparison.Ordinal))return Invalid("rooted-path");
        if(target.Contains('\\'))return Invalid("backslash");
        if(target.Contains(':')||target.IndexOf('\0')>=0)return Invalid("unsafe-path");
        // BCL lexical normalization uses a synthetic namespace root only. It
        // does not access files or authorize access to any resolved host path.
        var root=Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar.ToString(),"__androidmcp_logical_artifacts__"));
        var directory=Path.GetDirectoryName(request.SourcePath.Replace('/',Path.DirectorySeparatorChar))??"";
        string normalized;
        try{normalized=Path.GetFullPath(Path.Combine(root,directory,target.Replace('/',Path.DirectorySeparatorChar)));}
        catch(ArgumentException){return Invalid("unsafe-path");}
        var relative=Path.GetRelativePath(root,normalized).Replace(Path.DirectorySeparatorChar,'/');
        if(relative==".."||relative.StartsWith("../",StringComparison.Ordinal)||Path.IsPathRooted(relative))return Invalid("namespace-escape");
        return new(ArtifactReferenceDisposition.Local,relative=="."?"":relative,repositoryMarkdown,null);
    }
    private static string Fragment(string value){var index=value.IndexOf('#');return index<0?value:value[..index];}
    private static ArtifactReferenceResolution Invalid(string reason)=>new(ArtifactReferenceDisposition.Invalid,null,false,reason);
    private static ArtifactReferenceResolution Ignored(string reason)=>new(ArtifactReferenceDisposition.Ignored,null,false,reason);
}
