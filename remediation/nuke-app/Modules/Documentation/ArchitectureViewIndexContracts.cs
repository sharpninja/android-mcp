using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record ArchitectureViewIndex(IReadOnlyList<string> NativeKeys,IReadOnlyList<string> ImageSvgPaths);
public interface IArchitectureViewIndexReader
{
    ArchitectureViewIndex Read(Artifact workspaceDsl);
}
