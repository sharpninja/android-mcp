using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

public sealed record OperationHelpRequest(IReadOnlyList<OperationDefinition> Definitions,Artifact SdkConfiguration,
    Artifact ProjectConfiguration,Artifact PackageLock,IReadOnlyList<string> GlobalRequiredParameters);
public sealed record OperationHelpResult(Artifact? Document,Artifact? Markdown,IReadOnlyList<PublicationIssue> Issues)
{
    public bool IsValid=>Document is not null && Markdown is not null && Issues.Count==0;
}
public interface IOperationHelpBuilder
{
    OperationHelpResult Build(OperationHelpRequest request);
}
