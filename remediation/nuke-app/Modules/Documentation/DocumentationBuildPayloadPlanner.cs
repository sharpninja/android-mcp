using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure projection of the existing native collector's correlated records.
/// No SDK evaluation, source discovery, source read, hash implementation or output writer.</summary>
public sealed class DocumentationBuildPayloadPlanner(IStrictJsonInspector strict,IArtifactReceiptVerifier receipts)
    :IDocumentationBuildPayloadPlanner
{
    private static readonly JsonSerializerOptions JsonOptions=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public DocumentationBuildPayloadPlan Plan(DocumentationBuildPayloadInput input)
    {
        if(!ArtifactPaths.IsRelative(input.SourceManifest.RelativePath)||!ArtifactPaths.IsRelative(input.NativeEvaluation.RelativePath)||
            !receipts.Matches(input.SourceManifest,input.Snapshot.Manifest)||
            !receipts.Matches(input.NativeEvaluation,input.Snapshot.NativeEvaluationReceipt)||
            input.Snapshot.ContentFingerprint!=input.Snapshot.Manifest.Sha256)
            throw Error("BUILD_SOURCE_PIN");
        var manifest=Read<TestGateSourceManifest>(input.SourceManifest);
        var evaluation=Read<TestGateSdkEvaluation>(input.NativeEvaluation);
        string root,host;
        try
        {
            root=CanonicalRoot(input.SourceRoot);host=Path.GetFullPath(input.HostProject);
            if(!Path.IsPathFullyQualified(input.HostProject)||host!=input.HostProject||
                Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(host)!)!=root)throw Error("BUILD_SOURCE_HOST");
        }
        catch(ArgumentException){throw Error("BUILD_SOURCE_HOST");}
        if(manifest.HostProject!=host||evaluation.Projects is null||manifest.Inputs is null)throw Error("BUILD_SOURCE_HOST");
        var hosts=evaluation.Projects.Where(project=>project.ProjectPath==host).ToArray();
        if(hosts.Length!=1||hosts[0].Issues is null||hosts[0].Issues.Count!=0||hosts[0].Inputs is null||hosts[0].Imports is null||hosts[0].ProjectReferences is null)
            throw Error("BUILD_SOURCE_HOST");
        var project=hosts[0];
        if(project.ProjectReferences.Count!=0)throw Error("BUILD_SOURCE_PROJECT_REFERENCE");
        if(manifest.Configuration!=input.NativeConfiguration.Configuration||manifest.TargetFramework!=input.NativeConfiguration.TargetFramework)
            throw Error("BUILD_SOURCE_CONFIGURATION");
        if(evaluation.SdkFiles is null||!evaluation.SdkFiles.SequenceEqual(input.NativeConfiguration.Sdk.AssemblyFiles))throw Error("BUILD_SOURCE_SDK");

        var physical=new Dictionary<string,TestGateHashedInput>(StringComparer.Ordinal);
        var hashed=new Dictionary<(string Kind,string Path,string Defining),TestGateHashedInput>();
        foreach(var row in manifest.Inputs)
        {
            if(string.IsNullOrWhiteSpace(row.Kind)||string.IsNullOrWhiteSpace(row.AbsolutePath)||string.IsNullOrWhiteSpace(row.DefiningProject)||row.Length<0)
                throw Error("BUILD_SOURCE_COVERAGE");
            if(physical.TryGetValue(row.AbsolutePath,out var previous)&&(previous.Sha256!=row.Sha256||previous.Length!=row.Length))throw Error("BUILD_SOURCE_PIN_CONFLICT");
            physical.TryAdd(row.AbsolutePath,row);
            hashed.TryAdd((row.Kind,row.AbsolutePath,row.DefiningProject),row);
        }
        IReadOnlyList<TestGateEvaluatedInput> Inputs(TestGateEvaluatedProject value)
        {
            if(value.Inputs is null||value.Imports is null)throw Error("BUILD_SOURCE_COVERAGE");
            return value.Inputs.Concat(value.Imports.Select(path=>new TestGateEvaluatedInput("Import",path,value.ProjectPath))).ToArray();
        }
        var observed=evaluation.Projects.SelectMany(Inputs).ToArray();
        var keys=observed.Select(row=>(row.Kind,row.AbsolutePath,row.DefiningProject)).ToHashSet();
        if(!keys.SetEquals(hashed.Keys))throw Error("BUILD_SOURCE_COVERAGE");

        var scopes=input.PrerequisiteScopes.ToArray();
        if(scopes.Any(scope=>string.IsNullOrWhiteSpace(scope.Id)||scope.Id=="restore-generated"||scope.Id.Any(character=>!(char.IsAsciiLetterLower(character)||char.IsAsciiDigit(character)||character=='-'))||!Path.IsPathFullyQualified(scope.Root)||CanonicalRoot(scope.Root)!=scope.Root||
            !input.NativeConfiguration.ApprovedReadRoots.Contains(scope.Root,StringComparer.Ordinal))||
            scopes.Select(scope=>scope.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=scopes.Length||
            scopes.Select(scope=>scope.Root).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=scopes.Length)
            throw Error("BUILD_SOURCE_PREREQUISITE");
        foreach(var scope in scopes)
        {
            if(scope.Root==root||Relative(root,scope.Root)is not null||Relative(scope.Root,root)is not null)throw Error("BUILD_SOURCE_PREREQUISITE");
            if(scopes.Any(other=>!ReferenceEquals(other,scope)&&(other.Root==scope.Root||Relative(scope.Root,other.Root)is not null||Relative(other.Root,scope.Root)is not null)))
                throw Error("BUILD_SOURCE_PREREQUISITE");
            if(scope.Id=="sdk"&&scope.Root!=CanonicalRoot(input.NativeConfiguration.Sdk.SdkDirectory))throw Error("BUILD_SOURCE_PREREQUISITE");
        }
        var sources=new Dictionary<string,DocumentationBuildPayloadRoute>(StringComparer.OrdinalIgnoreCase);
        var prerequisites=new List<DocumentationBuildPrerequisite>();
        foreach(var item in Inputs(project))
        {
            var pin=hashed[(item.Kind,item.AbsolutePath,item.DefiningProject)];
            var relative=Relative(root,item.AbsolutePath);
            if(relative is not null)
            {
                if(relative.StartsWith("obj/",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("bin/",StringComparison.OrdinalIgnoreCase))
                {
                    if(relative.StartsWith("obj/",StringComparison.OrdinalIgnoreCase)&&item.Kind is "Import" or "ProjectAssetsFile")
                    {prerequisites.Add(new("restore-generated",relative,item.Kind,pin.Sha256,pin.Length));continue;}
                    throw Error("BUILD_SOURCE_GENERATED_SOURCE");
                }
                var source=new ArtifactSourceSpec(relative,"application/octet-stream",new(relative,pin.Sha256,pin.Length,"application/octet-stream"));
                var route=new DocumentationBuildPayloadRoute(source,"build-tool/"+relative);
                if(sources.TryGetValue(relative,out var existing))
                {if(existing.Source.RelativePath!=relative)throw Error("BUILD_SOURCE_COLLISION");}
                else sources.Add(relative,route);
                continue;
            }
            if(item.Kind is "Compile" or "EmbeddedResource" or "AdditionalFiles" or "Content" or "None" or "Project")throw Error("BUILD_SOURCE_EXTERNAL_SOURCE");
            var external=scopes.Select(scope=>(scope,relative:Relative(scope.Root,item.AbsolutePath))).Where(value=>value.relative is not null).ToArray();
            if(external.Length!=1)throw Error("BUILD_SOURCE_PREREQUISITE");
            prerequisites.Add(new(external[0].scope.Id,external[0].relative!,item.Kind,pin.Sha256,pin.Length));
        }
        foreach(var required in new[]{Path.GetFileName(host),"global.json","packages.lock.json"})
            if(!sources.ContainsKey(required))throw Error("BUILD_SOURCE_REQUIRED");
        return new(Array.AsReadOnly(sources.Values.OrderBy(route=>route.Source.RelativePath,StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(prerequisites.Distinct().OrderBy(row=>row.ScopeId,StringComparer.Ordinal).ThenBy(row=>row.RelativePath,StringComparer.Ordinal).ThenBy(row=>row.Kind,StringComparer.Ordinal).ToArray()));
    }
    private T Read<T>(Artifact artifact)
    {
        var inspection=strict.Inspect(artifact.Bytes);
        if(inspection.Value is null||inspection.CanonicalDiagnostics.Count!=0)throw Error("BUILD_SOURCE_JSON");
        try{return JsonSerializer.Deserialize<T>(inspection.Value.Value.GetRawText(),JsonOptions)??throw Error("BUILD_SOURCE_JSON");}
        catch(JsonException){throw Error("BUILD_SOURCE_JSON");}
    }
    private static string CanonicalRoot(string path)
    {
        if(!Path.IsPathFullyQualified(path))throw Error("BUILD_SOURCE_HOST");
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if(root!=Path.TrimEndingDirectorySeparator(path))throw Error("BUILD_SOURCE_HOST");
        return root;
    }
    private static string? Relative(string root,string path)
    {
        if(!Path.IsPathFullyQualified(path))return null;
        var relative=Path.GetRelativePath(root,path).Replace('\\','/');
        return ArtifactPaths.IsRelative(relative)?relative:null;
    }
    private static DocumentationBuildPayloadException Error(string code)=>new(code);
}
