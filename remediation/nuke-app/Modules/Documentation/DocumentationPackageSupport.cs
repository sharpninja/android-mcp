using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// The profile is the sole wrapper/version/pin authority. This projects source
// descriptors and composes existing preparations; it does not acquire or write.
public sealed class DocumentationPackageSupport(IArtifactReceiptVerifier receipts):IDocumentationPackageSupport
{
    public IReadOnlyList<ArtifactSourceSpec> Describe(DocumentationRendererProfile profile)
    {
        var configuration=new[]{"build.gradle.kts","tooling/nuke-design-stages.json","settings.gradle.kts","gradle.properties","gradle/wrapper/gradle-wrapper.properties"};
        var sources=configuration.Select(path=>new ArtifactSourceSpec(path,"application/octet-stream"))
            .Concat(profile.Downloads.Where(pin=>!pin.LocalPath.StartsWith(".tools/",StringComparison.Ordinal)).Select(pin=>
                new ArtifactSourceSpec(pin.LocalPath,"application/octet-stream",new(pin.LocalPath,pin.Sha256,pin.SizeBytes??throw Error(),"application/octet-stream")))).ToArray();
        if(sources.Any(source=>!ArtifactPaths.IsRelative(source.RelativePath))||sources.Select(source=>source.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=sources.Length)throw Error();
        return Array.AsReadOnly(sources);
    }
    public ArtifactGenerationPreparation Append(DocumentationRendererProfile profile,ArtifactGenerationPreparation child,IReadOnlyList<Artifact> captured)
    {
        var sources=Describe(profile);
        if(child.VerifyBeforeCommitAsync is null||captured.Count!=sources.Count||captured.Select(item=>item.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=captured.Count)throw Error();
        var byPath=captured.ToDictionary(item=>item.RelativePath,StringComparer.Ordinal);
        foreach(var source in sources)
            if(!byPath.TryGetValue(source.RelativePath,out var item)||(source.ExpectedIdentity is {} pin&&!receipts.Matches(item,pin)))throw Error();
        var outputs=child.OutputPaths.Concat(captured.Select(item=>item.RelativePath)).ToArray();
        if(outputs.Any(path=>!ArtifactPaths.IsRelative(path))||outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=outputs.Length)throw Error();
        var retained=Array.AsReadOnly(captured.ToArray());
        return new(Array.AsReadOnly(outputs),async token=>
        {
            token.ThrowIfCancellationRequested();var generated=await child.MaterializeAsync(token);
            return Array.AsReadOnly(generated.Concat(retained).ToArray());
        },child.VerifyBeforeCommitAsync);
    }
    private static DocumentationBuildPayloadException Error()=>new("BUILD_SUPPORT");
}
