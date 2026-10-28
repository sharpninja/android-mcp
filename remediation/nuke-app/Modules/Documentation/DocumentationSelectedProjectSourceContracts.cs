namespace AndroidMcp.BuildTool.Documentation;

public enum DocumentationProjectSourceScope { Model,GeneratedDesign }
public interface IDocumentationSelectedProjectSourceCatalog:IDocumentationProjectSourceCatalog
{
    IReadOnlyList<DocumentationProjectInput> DescribeProjectSources(DocumentationProjectSourceScope scope);
}
