using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>One set of project descriptors shared by source planning and stage input selection.</summary>
public sealed class DocumentationProjectSourceCatalog : IDocumentationSelectedProjectSourceCatalog
{
    private static readonly IReadOnlyList<DocumentationProjectInput> Sources=Array.AsReadOnly(new[]
    {
        Project(DocumentationProjectInputKind.WorkspaceDsl,"architecture/workspace.dsl","text/plain"),
        Project(DocumentationProjectInputKind.CodeMarkdown,"architecture/code-diagrams.md","text/markdown"),
        Project(DocumentationProjectInputKind.ModuleContract,"architecture/module-contract.json","application/json"),
        Project(DocumentationProjectInputKind.RenderSkin,"architecture/render-skin.puml","text/plain"),
        Project(DocumentationProjectInputKind.ToolchainLock,"toolchain-lock.json","application/json"),
        Project(DocumentationProjectInputKind.RendererLock,"renderer-lock.json","application/json"),
        Project(DocumentationProjectInputKind.BlueprintMarkdown,DocumentationAssetPaths.Blueprint,"text/markdown"),
        Project(DocumentationProjectInputKind.DesignMarkdown,DocumentationAssetPaths.Design,"text/markdown"),
        Project(DocumentationProjectInputKind.SourcePins,"contracts/source-pins.json","application/json")
    });
    private static readonly IReadOnlyList<DocumentationProjectInput> GeneratedSources=Array.AsReadOnly(Sources.Concat(new[]
    {
        Project(DocumentationProjectInputKind.Readme,"README.md","text/markdown"),
        Project(DocumentationProjectInputKind.NativeIdentities,"architecture/native-identities.json","application/json"),
        Project(DocumentationProjectInputKind.BindingReasons,"contracts/binding-reasons.json","application/json"),
        Project(DocumentationProjectInputKind.ScreenStateResultSchema,"contracts/ScreenStateResult.schema.json","application/json"),
        Project(DocumentationProjectInputKind.Review023Dispositions,"docs/review023-dispositions.md","text/markdown"),
        Project(DocumentationProjectInputKind.Review024Dispositions,"docs/review024-dispositions.md","text/markdown")
    }).ToArray());
    private static DocumentationProjectInput Project(DocumentationProjectInputKind kind,string path,string media)
        =>new(kind,new("project:"+path,DocumentationInputRole.ProjectRequired,null,path,media,null));
    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources()=>Sources;
    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources(DocumentationProjectSourceScope scope)=>scope switch
    {
        DocumentationProjectSourceScope.Model=>Sources,
        DocumentationProjectSourceScope.GeneratedDesign=>GeneratedSources,
        _=>throw new ArgumentOutOfRangeException(nameof(scope))
    };

    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources(DesignStagePlan selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var admitted=new DesignStagePlanner().Select(new(selection.Aliases,selection.RegistrySha256));
        if(selection.Errors.Count!=0||admitted.Errors.Count!=0||!admitted.Stages.SequenceEqual(selection.Stages)||!admitted.Aliases.SequenceEqual(selection.Aliases))
            throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Use the current admitted Core stage union");
        if(selection.Stages.Contains(DesignStage.DocsVerify))return GeneratedSources;
        if(selection.Stages.Contains(DesignStage.ModelContracts))return Sources;
        var required=new HashSet<DocumentationProjectInputKind>();
        // Requirements of selected services only. Dependencies and aliases are
        // already resolved by Core; no second graph or alias map exists here.
        if(selection.Stages.Contains(DesignStage.Tools))
        {required.Add(DocumentationProjectInputKind.ToolchainLock);required.Add(DocumentationProjectInputKind.RendererLock);}
        if(selection.Stages.Contains(DesignStage.UiAssets))
        {required.Add(DocumentationProjectInputKind.BlueprintMarkdown);required.Add(DocumentationProjectInputKind.DesignMarkdown);}
        if(selection.Stages.Contains(DesignStage.ModuleGraph))
        {required.Add(DocumentationProjectInputKind.ModuleContract);required.Add(DocumentationProjectInputKind.CodeMarkdown);}
        if(selection.Stages.Contains(DesignStage.CodeExtract))
        {required.Add(DocumentationProjectInputKind.WorkspaceDsl);required.Add(DocumentationProjectInputKind.CodeMarkdown);}
        return Array.AsReadOnly(Sources.Where(source=>required.Contains(source.Kind)).ToArray());
    }
}
