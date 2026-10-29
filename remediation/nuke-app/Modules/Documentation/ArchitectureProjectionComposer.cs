using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>One renderer engine over the authoritative design-stage plan and shared immutable runtime.</summary>
public sealed class ArchitectureProjectionComposer(IArtifactGenerationRuntime workRuntime,IArtifactStore work,
    IArtifactReceiptVerifier receipts,IArchitectureAssetPreparation preparation,IArchitectureRendererTransport renderer,
    IRendererImageAdmission admission,string admittedWorkGenerationId,
    DocumentationLegacyReceipts? legacyReceipts=null):IArchitectureStageProjectionComposer
{
    private const string ExportPrefix="build/structurizr-export/structurizr-";
    private const string ProfiledPrefix="build/structurizr-profiled/structurizr-";
    private const string DiagramPrefix="docs/diagrams/";
    private const string DockerConfig=".docker-cli/config.json";
    private const string Skin="architecture/render-skin.puml";
    private static readonly IArchitectureViewIndexReader Views=new ArchitectureViewIndexReader();
    private readonly IDesignStagePlanner stages=new DesignStagePlanner();

    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> codeViews)
    {
        var selected=FullSelection();var native=NativeKeys(inputs);
        return new(inputs,profile,codeViews,Publications(inputs,codeViews,native,selected,false,false).Keys.ToArray());
    }
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,
        IReadOnlyList<ToolArtifactReceipt> admittedTools,CancellationToken cancellationToken=default)
    {
        // The old host already admitted tools and bound the same cache into context.
        return MaterializeAsync(plan.Inputs,plan.Profile,plan.CodeViews,context,FullSelection(),false,false,_=>Task.CompletedTask,cancellationToken);
    }
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,
        DocumentationRendererProfile? profile,IReadOnlyList<Artifact> codeViews,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admitTools)
    {
        var metadata=stages.Describe();
        if(selection.Errors.Count>0||selection.RegistrySha256!=metadata.Sha256||selection.Aliases.Any(alias=>!metadata.Metadata.Aliases.Any(known=>known.Name==alias)))
            throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Use an admitted current design-stage selection");
        if(Has(selection,DesignStage.Tools)&&(profile is null||context is null))throw new ArchitectureAssetException("RENDERER_CONTEXT","Pinned renderer profile and local context are required");
        if(Has(selection,DesignStage.Tools)&&admitTools is null)throw new ArchitectureAssetException("TOOL_ADMISSION","A deferred native tool admission is required");
        var exports=selection.Aliases.Any(alias=>metadata.Metadata.Aliases.Single(known=>known.Name==alias).Selects.Contains(DesignStage.ArchitectureExport));
        var native=Has(selection,DesignStage.ArchitectureExport)?NativeKeys(inputs):Array.Empty<string>();
        var emitLegacy=legacyReceipts is not null&&Has(selection,DesignStage.ArchitectureValidate);
        var publications=Publications(inputs,codeViews,native,selection,exports,emitLegacy);
        return new(publications.Keys.ToArray(),token=>MaterializeAsync(inputs,profile,codeViews,context,selection,exports,emitLegacy,admitTools,token));
    }
    private DesignStagePlan FullSelection(){var metadata=stages.Describe();return stages.Select(new(["generateArchitectureDiagrams"],metadata.Sha256));}
    private static bool Has(DesignStagePlan selected,DesignStage stage)=>selected.Stages.Contains(stage);

    private async Task<IReadOnlyList<Artifact>> MaterializeAsync(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,
        IReadOnlyList<Artifact> codeViews,ArchitectureRendererContext? context,DesignStagePlan selected,bool exports,bool emitLegacy,Func<CancellationToken,Task>? admitTools,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var native=Has(selected,DesignStage.ArchitectureExport)?NativeKeys(inputs):Array.Empty<string>();
        var published=Publications(inputs,codeViews,native,selected,exports,emitLegacy);
        if(!Has(selected,DesignStage.Tools))return codeViews.Where(code=>published.ContainsKey(code.RelativePath)).ToArray();
        ArgumentException.ThrowIfNullOrWhiteSpace(admittedWorkGenerationId);
        var paths=WorkPaths(inputs,codeViews,native,selected,emitLegacy);
        var recipe=new ArtifactGenerationRecipe(inputs.Select(a=>new ArtifactSourceSpec(a.RelativePath,a.MediaType,receipts.Describe(a))).ToArray(),captured=>new(paths.Keys.ToArray(),async innerToken=>
        {
            foreach(var source in captured.Concat(codeViews))await work.WriteAsync(source,innerToken);
            await work.WriteAsync(new(DockerConfig,JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>()),"application/json"),innerToken);
            // The shared planner has already formed the union and its topological order.
            foreach(var stage in selected.Stages)
            {
                switch(stage)
                {
                    case DesignStage.Tools:
                        await admitTools!(innerToken);await admission.AdmitAsync(profile!,context!,innerToken);break;
                    case DesignStage.CodeRender:
                        await renderer.ExecuteAsync(profile!,context!,new(ArchitectureRendererPhase.RenderCode,codeViews.Select(a=>a.RelativePath).ToArray()),innerToken);
                        foreach(var code in codeViews)
                        {
                            var rawPath=code.RelativePath[..^5]+".svg";var raw=new Artifact(rawPath,await work.ReadAsync(rawPath,innerToken),"image/svg+xml");
                            await work.WriteAsync(preparation.NormalizeVector(raw,DiagramPrefix+Path.GetFileNameWithoutExtension(code.RelativePath)+".svg"),innerToken);
                        }
                        break;
                    case DesignStage.ArchitectureValidate:
                        var validation=await renderer.ExecuteAsync(profile!,context!,new(ArchitectureRendererPhase.ValidateWorkspace,[]),innerToken);
                        if(emitLegacy)
                            await work.WriteAsync(legacyReceipts!.Architecture(captured.Single(source=>source.RelativePath=="architecture/workspace.dsl"),validation),innerToken);
                        break;
                    case DesignStage.ArchitectureExport:
                        await renderer.ExecuteAsync(profile!,context!,new(ArchitectureRendererPhase.ExportWorkspace,[]),innerToken);
                        foreach(var key in native)await work.WriteAsync(new(ProfiledPrefix+key+".puml",ApplyExistingProfile(await work.ReadAsync(ExportPrefix+key+".puml",innerToken)),"text/vnd.plantuml"),innerToken);
                        break;
                    case DesignStage.ArchitectureRender:
                        await renderer.ExecuteAsync(profile!,context!,new(ArchitectureRendererPhase.RenderNative,native.Select(key=>ProfiledPrefix+key+".puml").ToArray()),innerToken);
                        foreach(var key in native)
                        {
                            var rawPath=ProfiledPrefix+key+".svg";var raw=new Artifact(rawPath,await work.ReadAsync(rawPath,innerToken),"image/svg+xml");
                            await work.WriteAsync(preparation.NormalizeVector(raw,DiagramPrefix+"structurizr-"+key+".svg"),innerToken);
                        }
                        break;
                }
            }
            var output=new List<Artifact>();foreach(var path in paths)output.Add(new(path.Key,await work.ReadAsync(path.Key,innerToken),path.Value));return output;
        }));
        await workRuntime.ExecuteAsync(admittedWorkGenerationId,recipe,token);
        var result=new List<Artifact>();foreach(var item in published)result.Add(new(item.Key,await work.ReadAsync(item.Value.Source,token),item.Value.Media));return result;
    }
    private static SortedDictionary<string,(string Source,string Media)> Publications(IReadOnlyList<Artifact> inputs,IReadOnlyList<Artifact> code,
        IReadOnlyList<string> native,DesignStagePlan selected,bool exports,bool emitLegacy)
    {
        var result=new SortedDictionary<string,(string Source,string Media)>(StringComparer.Ordinal);
        if(Has(selected,DesignStage.CodeExtract))foreach(var artifact in code)result.Add(artifact.RelativePath,(artifact.RelativePath,artifact.MediaType));
        if(Has(selected,DesignStage.CodeRender))foreach(var artifact in code){var path=DiagramPrefix+Path.GetFileNameWithoutExtension(artifact.RelativePath)+".svg";result.Add(path,(path,"image/svg+xml"));}
        if(Has(selected,DesignStage.ArchitectureRender))foreach(var key in native){var path=DiagramPrefix+"structurizr-"+key+".svg";result.Add(path,(path,"image/svg+xml"));}
        if(emitLegacy){var receipt=DocumentationLegacyReceipts.Publication(DesignStage.ArchitectureValidate);result.Add(receipt.Path,(receipt.Path,receipt.Media));}
        if(exports)
        {
            var skin=inputs.SingleOrDefault(artifact=>artifact.RelativePath==Skin)??throw new ArchitectureAssetException("MISSING_RENDER_SKIN","Published profiled exports require the admitted render skin");
            result.Add(Skin,(Skin,skin.MediaType));foreach(var key in native)result.Add(ExportPrefix+key+".puml",(ProfiledPrefix+key+".puml","text/vnd.plantuml"));
        }
        return result;
    }
    private static IReadOnlyDictionary<string,string> WorkPaths(IReadOnlyList<Artifact> inputs,IReadOnlyList<Artifact> code,IReadOnlyList<string> native,DesignStagePlan selected,bool emitLegacy)
    {
        var paths=inputs.Concat(code).ToDictionary(artifact=>artifact.RelativePath,artifact=>artifact.MediaType,StringComparer.Ordinal);paths.Add(DockerConfig,"application/json");
        if(Has(selected,DesignStage.CodeRender))foreach(var artifact in code){paths.Add(artifact.RelativePath[..^5]+".svg","image/svg+xml");paths.Add(DiagramPrefix+Path.GetFileNameWithoutExtension(artifact.RelativePath)+".svg","image/svg+xml");}
        if(Has(selected,DesignStage.ArchitectureExport))foreach(var key in native){paths.Add(ExportPrefix+key+".puml","text/vnd.plantuml");paths.Add(ProfiledPrefix+key+".puml","text/vnd.plantuml");}
        if(Has(selected,DesignStage.ArchitectureRender))foreach(var key in native){paths.Add(ProfiledPrefix+key+".svg","image/svg+xml");paths.Add(DiagramPrefix+"structurizr-"+key+".svg","image/svg+xml");}
        if(emitLegacy){var receipt=DocumentationLegacyReceipts.Publication(DesignStage.ArchitectureValidate);paths.Add(receipt.Path,receipt.Media);}
        return paths;
    }
    private static string[] NativeKeys(IReadOnlyList<Artifact> inputs)
    {
        var dsl=inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl");var keys=Views.Read(dsl).NativeKeys;
        if(keys.Count==0||keys.Distinct(StringComparer.Ordinal).Count()!=keys.Count)throw new ArchitectureAssetException("NATIVE_VIEW_PLAN","Native workspace view keys must be present and unique");
        return keys.ToArray();
    }
    private static ReadOnlyMemory<byte> ApplyExistingProfile(ReadOnlyMemory<byte> raw)
    {
        const string include="!include ../../architecture/render-skin.puml";var text=Encoding.UTF8.GetString(raw.Span);if(text.Contains(include,StringComparison.Ordinal))return raw;
        var marker=text.IndexOf("@startuml",StringComparison.Ordinal);if(marker<0)throw new ArchitectureAssetException("NATIVE_EXPORT","Structurizr export has no PlantUML start marker");
        return Encoding.UTF8.GetBytes(text.Insert(marker+"@startuml".Length,"\n"+include));
    }
}
