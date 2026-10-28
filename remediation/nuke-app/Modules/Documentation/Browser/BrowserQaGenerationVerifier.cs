using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Browser;

public sealed record BrowserQaGenerationSvg(string Root,string RelativePath,string Sha256,long Length,string Role)
{public string AbsolutePath=>ArtifactPaths.Resolve(Root,RelativePath);}
public sealed record BrowserQaVerifiedGeneration(string Role,string Root,string GenerationId,StoredArtifact Manifest,
    IReadOnlyList<StoredArtifact> Artifacts);
public sealed record BrowserQaGenerationAdmission(IReadOnlyList<BrowserQaVerifiedGeneration> Generations,
    IReadOnlyList<BrowserQaGenerationSvg> SvgFiles,int ArchitectureDiagrams,string Scope);

/// <summary>Read-only admission of selected committed projections before a browser or output exists.</summary>
public sealed class BrowserQaGenerationVerifier
{
    private static readonly CanonicalJson Canonical=new();
    private static BrowserQaInputException Failure(string code,string message)=>new(code,message);
    private static bool Inside(string child,string parent)
    {
        var relative=Path.GetRelativePath(parent,child);
        return relative=="."||!Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
    }
    private static string Resolve(string root,string path)
    {try{return ArtifactPaths.Resolve(root,path);}catch(ArgumentException error){throw Failure("generation-artifact-path",error.Message);}}
    private static string Text(JsonObject value,string name)=>value[name]?.GetValue<string>()??throw new FormatException("Missing string field "+name);

    public async Task<T> AdmitAndRunAsync<T>(BrowserQaRequest request,Func<BrowserQaGenerationAdmission,CancellationToken,Task<T>> admittedOperation,CancellationToken cancellationToken=default)
    {
        var admission=await VerifyAsync(request,cancellationToken);
        return await admittedOperation(admission,cancellationToken);
    }

    public async Task<BrowserQaGenerationAdmission> VerifyAsync(BrowserQaRequest request,CancellationToken cancellationToken=default)
    {
        var selection=request.GenerationSelection??throw new ArgumentException("Selected generation identities are required.");
        var ui=Path.GetFullPath(request.ProjectRoot);var output=Path.GetFullPath(request.OutputRoot);
        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        if(!comparer.Equals(ui,Path.GetFullPath(selection.ProjectRoot)))throw Failure("generation-manifest-invalid","Selected UI root differs from the QA project root.");
        if(request.ExpectedArchitectureDiagrams<0)throw Failure("generation-diagram-count","The diagram denominator cannot be negative.");
        var hasArchitecture=!string.IsNullOrWhiteSpace(selection.ArchitectureRoot);
        var hasArchitecturePin=!string.IsNullOrWhiteSpace(selection.ArchitectureManifestSha256);
        if(request.ExpectedArchitectureDiagrams==0?(hasArchitecture||hasArchitecturePin):(!hasArchitecture||!hasArchitecturePin))
            throw Failure("generation-architecture-required","Positive diagram counts require an architecture root and pin; UI-only zero requires both absent.");
        var architecture=hasArchitecture?Path.GetFullPath(selection.ArchitectureRoot!):null;
        foreach(var input in architecture is null?new[]{ui}:new[]{ui,architecture})
            if(Inside(output,input)||Inside(input,output))throw Failure("generation-output-overlap","QA output and selected immutable generations must be disjoint.");
        _=Resolve(output,"browser-qa.json");
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw Failure("generation-output-not-fresh","QA output generation must be fresh.");
        var generations=new List<BrowserQaVerifiedGeneration>{await ReadAsync("ui",ui,selection.UiManifestSha256,cancellationToken)};
        if(architecture is not null)generations.Add(await ReadAsync("architecture",architecture,selection.ArchitectureManifestSha256!,cancellationToken));
        var uiPaths=generations[0].Artifacts.Select(file=>file.RelativePath).ToHashSet(StringComparer.Ordinal);
        void RequireUi(string path)
        {_=Resolve(ui,path);if(!uiPaths.Contains(path))throw Failure("generation-required-artifact","UI generation does not declare required artifact: "+path);}
        RequireUi("docs/storyboards.html");RequireUi("docs/ui-asset-manifest.json");
        try
        {
            var bytes=await File.ReadAllBytesAsync(Resolve(ui,"docs/ui-asset-manifest.json"),cancellationToken);_=Canonical.Serialize(bytes);
            var manifest=JsonNode.Parse(bytes)!.AsObject();
            foreach(var state in manifest["state_svg_files"]!.AsArray())RequireUi("docs/"+state!.GetValue<string>());
            foreach(var step in manifest["storyboards"]!.AsArray().SelectMany(board=>board!["steps"]!.AsArray()))
                RequireUi("docs/screens/steps/"+step!["step_id"]!.GetValue<string>()+".svg");
        }
        catch(BrowserQaInputException){throw;}
        catch(Exception error)when(error is System.Text.Json.JsonException or FormatException or InvalidOperationException or NullReferenceException)
        {throw Failure("generation-manifest-invalid","Malformed UI asset manifest: "+error.Message);}
        var svg=generations.SelectMany(generation=>generation.Artifacts.Where(file=>file.RelativePath.EndsWith(".svg",StringComparison.Ordinal))
            .Select(file=>new BrowserQaGenerationSvg(generation.Root,file.RelativePath,file.Sha256,file.Length,generation.Role))).OrderBy(file=>file.RelativePath,StringComparer.Ordinal).ToArray();
        if(svg.GroupBy(file=>file.RelativePath,StringComparer.Ordinal).Any(group=>group.Count()!=1))throw Failure("generation-artifact-collision","Selected roots share a public SVG label.");
        if(generations.Skip(1).SelectMany(generation=>generation.Artifacts).Any(file=>file.RelativePath.EndsWith(".svg",StringComparison.Ordinal)&&!file.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal)))
            throw Failure("generation-artifact-path","Architecture SVGs must belong to the final docs/diagrams projection.");
        var diagrams=svg.Count(file=>file.Role=="architecture"&&file.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal));
        if(diagrams!=request.ExpectedArchitectureDiagrams)throw Failure("generation-diagram-count","The supplied diagram count differs from the complete verified architecture SVG set.");
        return new(generations,svg,diagrams,architecture is null?"ui-only":"ui-and-architecture");
    }

    private static async Task<BrowserQaVerifiedGeneration> ReadAsync(string role,string root,string expectedPin,CancellationToken token)
    {
        var path=Resolve(root,DocumentationAssetPaths.Manifest);
        if(!File.Exists(path))throw Failure("generation-manifest-missing","Selected generation manifest is missing: "+path);
        var bytes=await File.ReadAllBytesAsync(path,token);var digest=ContentHash.Sha256(bytes);
        if(!Regex.IsMatch(expectedPin??"","\\A[0-9a-fA-F]{64}\\z",RegexOptions.CultureInvariant)||!digest.Equals(expectedPin,StringComparison.OrdinalIgnoreCase))
            throw Failure("generation-manifest-pin","Selected generation manifest does not match its explicit pin.");
        JsonObject document;
        try{_=Canonical.Serialize(bytes);document=JsonNode.Parse(bytes)!.AsObject();}
        catch(Exception error)when(error is System.Text.Json.JsonException or InvalidOperationException or FormatException or ArgumentException or NullReferenceException)
        {throw Failure("generation-manifest-invalid",error.Message);}
        try
        {
            if(Text(document,"schema_version")!="1.0.0"||Text(document,"kind")!="android-mcp-design-generation")throw new FormatException("Unsupported generation manifest.");
            var generation=Text(document,"generation_id");if(string.IsNullOrWhiteSpace(generation))throw new FormatException("Generation ID is empty.");
            var artifacts=new List<StoredArtifact>();var paths=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in document["artifacts"]!.AsArray())
            {
                var entry=item!.AsObject();var relative=Text(entry,"path");var member=Resolve(root,relative);
                if(relative==DocumentationAssetPaths.Manifest||!paths.Add(relative))throw new FormatException("Duplicate or reserved generation artifact: "+relative);
                var hash=Text(entry,"sha256");var length=entry["size_bytes"]!.GetValue<long>();
                if(length<0||!Regex.IsMatch(hash,"\\A[0-9a-fA-F]{64}\\z",RegexOptions.CultureInvariant))throw new FormatException("Invalid artifact identity.");
                if(!File.Exists(member))throw Failure("generation-required-artifact","Declared generation artifact is missing: "+relative);
                using(var stream=File.OpenRead(member))
                    if(stream.Length!=length||!ContentHash.Sha256(stream).Equals(hash,StringComparison.OrdinalIgnoreCase))throw Failure("generation-artifact-pin","Declared generation artifact differs: "+relative);
                artifacts.Add(new(relative,hash.ToLowerInvariant(),length,Text(entry,"media_type")));
            }
            foreach(var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
            {
                var relative=Path.GetRelativePath(root,file).Replace(Path.DirectorySeparatorChar,'/');
                _=Resolve(root,relative);
                if((relative.EndsWith(".svg",StringComparison.Ordinal)||relative.EndsWith(".html",StringComparison.Ordinal))&&!paths.Contains(relative))
                    throw Failure("generation-undeclared-artifact","Undeclared visual artifact: "+relative);
            }
            return new(role,root,generation,new(DocumentationAssetPaths.Manifest,digest,bytes.LongLength,"application/json"),artifacts);
        }
        catch(BrowserQaInputException){throw;}
        catch(Exception error)when(error is FormatException or InvalidOperationException or NullReferenceException)
        {throw Failure("generation-manifest-invalid",error.Message);}
    }

    public async Task VerifyUnchangedAsync(BrowserQaGenerationAdmission admission,CancellationToken cancellationToken=default)
    {
        foreach(var generation in admission.Generations)
        {
            foreach(var file in generation.Artifacts.Append(generation.Manifest))
            {
                var path=Resolve(generation.Root,file.RelativePath);using var stream=File.OpenRead(path);
                if(stream.Length!=file.Length||ContentHash.Sha256(stream)!=file.Sha256)throw Failure("generation-artifact-pin","Selected generation changed during QA: "+file.RelativePath);
            }
            var verified=await ReadAsync(generation.Role,generation.Root,generation.Manifest.Sha256,cancellationToken);
            if(verified.GenerationId!=generation.GenerationId)throw Failure("generation-artifact-pin","Selected generation ID changed during QA.");
        }
    }
}
