using System.Globalization;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Validation;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Five legacy receipt projections over existing successful authorities.
/// JSON compatibility is semantic; the two text formats retain exact bytes.
/// No validator, process execution, file discovery or checksum writer is added.</summary>
public sealed class DocumentationLegacyReceipts(ICanonicalJson canonical,IStrictJsonInspector strict,IArtifactReceiptVerifier receipts)
{
    private sealed record Route(DesignStage Stage,string Name,string Media)
    {
        public string SourcePath=>"build/receipts/"+Name;
        public string AliasPath=>"validation/"+Name;
    }
    // Sole publication/archive mapping. Dependencies and alias unions stay in Core.
    private static readonly Route[] Routes=
    [
        new(DesignStage.ModuleGraph,"module-graph.json","application/json"),
        new(DesignStage.ModelContracts,"model-contracts.json","application/json"),
        new(DesignStage.UiAssets,"ui-projection.json","application/json"),
        new(DesignStage.ArchitectureValidate,"architecture-validation.txt","text/plain"),
        new(DesignStage.DocsVerify,"docs-validation.txt","text/plain")
    ];
    private readonly IDesignStagePlanner stages=new DesignStagePlanner();
    private static Route For(DesignStage stage)=>Routes.Single(route=>route.Stage==stage);
    internal static (string Path,string Media) Publication(DesignStage stage)
    {var route=For(stage);return(route.SourcePath,route.Media);}
    private static ArchitectureAssetException Unvalidated()=>new("LEGACY_RECEIPT_UNVALIDATED","Unsuccessful existing authority cannot publish a success receipt");

    public Artifact Module(ModuleValidationResult result)
    {
        if(result.Issues.Count!=0)throw Unvalidated();
        return Json(For(DesignStage.ModuleGraph),new
        {
            class_count=result.ClassCount,module_edges=result.ModuleEdges.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            module_graph_acyclic=true,scope="Proposed design/source imports; no app runtime proof"
        });
    }
    public Artifact Model(ModelContractValidationResult result)
    {
        if(!result.Validated)throw Unvalidated();
        return Json(For(DesignStage.ModelContracts),new{validated=result.Validated,scope=result.Scope,screens=result.Screens,states=result.States});
    }
    public Artifact Ui(Artifact projectedManifest)
    {
        try
        {
            if(projectedManifest.RelativePath!="docs/ui-asset-manifest.json"||projectedManifest.MediaType!="application/json")
                throw new FormatException("Use the existing projected UI manifest identity");
            var inspection=strict.Inspect(projectedManifest.Bytes);
            if(inspection.CanonicalDiagnostics.Count!=0||inspection.Value is null)throw new FormatException("Projected manifest is not admitted strict JSON");
            var value=inspection.Value.Value;
            var screens=value.GetProperty("screens").EnumerateArray().ToArray();
            var stories=value.GetProperty("storyboards").EnumerateArray().ToArray();
            // Counts and paths are observations of the already projected manifest,
            // not another blueprint parser or a table of expected UI entities.
            return Json(For(DesignStage.UiAssets),new
            {
                screens=screens.Length,controls=screens.Sum(screen=>screen.GetProperty("controls").EnumerateObject().Count()),
                states=screens.Sum(screen=>screen.GetProperty("state_specs").EnumerateObject().Count()),storyboards=stories.Length,
                steps=stories.Sum(story=>story.GetProperty("steps").GetArrayLength()),
                state_svg_files=value.GetProperty("state_svg_files").EnumerateArray().Select(item=>item.GetString()??throw new FormatException("UI path is not a string")).ToArray(),
                canonical_source="docs/ui-blueprint.md",implemented_android_ui=false
            });
        }
        catch(Exception error)when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new ArchitectureAssetException("LEGACY_RECEIPT_UI","Existing projected UI metadata is unavailable: "+error.Message);}
    }
    public Artifact Architecture(Artifact workspace,ProcessResult validation)
    {
        if(validation.Cancelled||validation.ExitCode!=0)throw Unvalidated();
        return Text(For(DesignStage.ArchitectureValidate),"Structurizr validation exited0\nworkspace_sha256="+receipts.Describe(workspace).Sha256+"\n");
    }
    public Artifact Docs(GeneratedDesignValidationResult result)
    {
        if(result.Phase!=DocumentationValidationPhase.BeforeManifest)
            throw new ArchitectureAssetException("LEGACY_RECEIPT_PHASE","The document receipt requires the existing BeforeManifest validation result");
        if(!result.IsValid||result.ParseDiagnostics.Count!=0)throw Unvalidated();
        return Text(For(DesignStage.DocsVerify),"Vector SVGs="+result.Counters.RenderedSvgCount.ToString(CultureInfo.InvariantCulture)+"; Markdown relative links valid; no raster substitutions; no public rendering\n");
    }

    public IReadOnlyList<string> Describe(DesignStagePlan selection,bool enabled)
    {
        var admitted=stages.Select(new(selection.Aliases,selection.RegistrySha256));
        if(selection.Errors.Count!=0||admitted.Errors.Count!=0||!selection.Stages.SequenceEqual(admitted.Stages)||!selection.Aliases.SequenceEqual(admitted.Aliases))
            throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Use the current admitted Core stage union");
        if(!enabled)return [];
        var paths=Routes.Where(route=>selection.Stages.Contains(route.Stage)).Select(route=>route.SourcePath);
        if(selection.Stages.Contains(DesignStage.Package))paths=paths.Concat(Routes.Select(route=>route.AliasPath));
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<Artifact> AddPackageAliases(IReadOnlyList<Artifact> inputs,bool package)
    {
        if(!package)return inputs; // No package-only admission is added to old defaults.
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var input in inputs)
        {
            if(!ArtifactPaths.IsRelative(input.RelativePath))throw new ArchitectureAssetException("LEGACY_RECEIPT_ALIAS_PATH","Unsafe receipt alias input path");
            if(!paths.Add(input.RelativePath))throw new ArchitectureAssetException("LEGACY_RECEIPT_ALIAS_COLLISION","Receipt alias inputs collide");
            if(input.RelativePath.StartsWith("build/receipts/",StringComparison.OrdinalIgnoreCase)&&!Routes.Any(route=>route.SourcePath==input.RelativePath))
                throw new ArchitectureAssetException("LEGACY_RECEIPT_UNKNOWN","Unknown public legacy receipt");
        }
        var aliases=new List<Artifact>();
        foreach(var route in Routes)
        {
            var source=inputs.SingleOrDefault(input=>input.RelativePath==route.SourcePath)
                ??throw new ArchitectureAssetException("LEGACY_RECEIPT_MISSING","Missing public receipt: "+route.SourcePath);
            if(paths.Contains(route.AliasPath))throw new ArchitectureAssetException("LEGACY_RECEIPT_ALIAS_COLLISION","Declared archive receipt alias already exists");
            // Same bytes/media, one mapping, no independent receipt regeneration.
            aliases.Add(source with{RelativePath=route.AliasPath});
        }
        return inputs.Concat(aliases).ToArray();
    }

    private Artifact Json<T>(Route route,T value)
    {
        var json=canonical.Serialize(JsonSerializer.SerializeToElement(value));var bytes=new byte[json.Length+1];
        json.CopyTo(bytes,0);bytes[^1]=(byte)'\n';return new(route.SourcePath,bytes,route.Media);
    }
    private static Artifact Text(Route route,string text)=>new(route.SourcePath,Encoding.UTF8.GetBytes(text),route.Media);
}
