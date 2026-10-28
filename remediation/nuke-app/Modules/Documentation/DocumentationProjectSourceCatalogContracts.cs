namespace AndroidMcp.BuildTool.Documentation;

public enum DocumentationProjectInputKind
{
    WorkspaceDsl,CodeMarkdown,ModuleContract,RenderSkin,ToolchainLock,RendererLock,BlueprintMarkdown,DesignMarkdown,SourcePins,
    Readme,NativeIdentities,BindingReasons,ScreenStateResultSchema,Review023Dispositions,Review024Dispositions
}
public sealed record DocumentationProjectInput(DocumentationProjectInputKind Kind,DocumentationModelSourceSpec Source);
public interface IDocumentationProjectSourceCatalog
{
    IReadOnlyList<DocumentationProjectInput> DescribeProjectSources();
}
