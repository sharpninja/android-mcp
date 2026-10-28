namespace AndroidMcp.BuildTool.Documentation;

/// <summary>The existing nine project descriptors, shared by source planning and stage input selection.</summary>
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
}
