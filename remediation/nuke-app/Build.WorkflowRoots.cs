using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private static string AdmitWorkflowRoot(string project,string path)
    {
        var root=Path.GetFullPath(path,project);
        _=ArtifactPaths.Resolve(root,".sources-extract-admission");
        return Path.TrimEndingDirectorySeparator(root);
    }
}
