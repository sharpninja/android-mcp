namespace AndroidMcp.BuildTool.Documentation.Validation;

public interface IRepositoryDocumentationCheck
{
    Task<RepositoryDocumentationCheckResult> CheckAsync(CancellationToken cancellationToken=default);
}
