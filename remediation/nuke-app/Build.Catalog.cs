using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    // Java remains an explicit selected-manifest admission. The shared loader
    // is bound after its mock and native gates; it never builds or fetches.
    private Task BuildCatalog()=>BuildCatalog(new SourcesRuntimeFactory(new JavaSourceRuntimeLoader()));

    // Root binds the one authoritative Sources runtime factory. This overload
    // adds no extractor registrations, kind aliases, parser or Java fallback.
    private async Task BuildCatalog(ISourcesRuntimeFactory sourcesFactory)
    {
        var project=Path.GetFullPath(ProjectRoot!);var output=Path.GetFullPath(OutputRoot!);var stage=Path.GetFullPath(StageRoot!);
        var manifest=Path.GetFullPath(SourceManifest!,project);var sourceRoot=Path.GetDirectoryName(manifest)!;
        manifest=ArtifactPaths.Resolve(sourceRoot,Path.GetFileName(manifest));_=ArtifactPaths.Resolve(output,"catalog.sqlite");_=ArtifactPaths.Resolve(stage,".catalog-admission");
        var comparison=OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool Same(string left,string right)=>string.Equals(Path.TrimEndingDirectorySeparator(left),Path.TrimEndingDirectorySeparator(right),comparison);
        bool Inside(string child,string parent)
        {
            var relative=Path.GetRelativePath(parent,child);
            return relative=="." || !Path.IsPathRooted(relative) && relative!=".." && !relative.StartsWith(".."+Path.DirectorySeparatorChar,comparison);
        }
        if(Same(output,project) || Same(output,sourceRoot) || Inside(stage,output) || Inside(output,stage))
            throw new InvalidOperationException("Catalog output, retained inputs and private stage must use separate directories");
        var canonical=new CanonicalJson();var manifestBytes=await File.ReadAllBytesAsync(manifest);_=canonical.Serialize(manifestBytes);
        var spec=JsonNode.Parse(manifestBytes)!.AsObject();var needsJava=spec["sources"]!.AsArray().OfType<JsonObject>()
            .Any(source=>source["status"]?.GetValue<string>()=="downloaded" && source["kind"]?.GetValue<string>()=="java-source");
        if(needsJava && string.IsNullOrWhiteSpace(JavaNativeManifest))
            throw new InvalidOperationException("Downloaded java-source rows require an explicitly selected java-native-manifest before catalog outputs");
        var profile=ExtractionProfile switch
        {
            null or "current-candidate"=>SourceExtractionProfile.CurrentCandidate,
            "release-011"=>SourceExtractionProfile.RELEASE011,
            _=>throw new InvalidOperationException("Unknown source extraction profile")
        };
        var java=string.IsNullOrWhiteSpace(JavaNativeManifest) ? null : new SourceJavaAdmission(Path.GetFullPath(JavaNativeManifest,project));
        // Admission/ABI checks happen through the shared source factory before
        // CatalogBuild opens a stage or final artifact. Runtime owns disposal.
        using var runtime=sourcesFactory.Create(profile,java);var concerns=runtime.Concerns;
        var service=new CatalogBuild(project,runtime.Dispatcher,concerns.PermissionExpressions,concerns.AnnotationTargets,concerns.TypeHeaders,canonical);
        var result=await service.BuildAsync(manifest,output,stage);
        Console.WriteLine(JsonSerializer.Serialize(new{result.RegistryId,result.ProducerFingerprint,result.Artifacts,Counts=result.Validation["counts"],Complete=false,OutputRoot=output}));
    }
}
