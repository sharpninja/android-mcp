using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record ArchitecturePreparationInput(Artifact Workspace,Artifact CodeMarkdown);
public sealed class ArchitectureAssetException(string code,string message):IOException(message)
{
    public string Code {get;}=code;
}
public interface IArchitectureAssetPreparation
{
    IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input);
    Artifact NormalizeVector(Artifact rawSvg,string outputPath);
}
