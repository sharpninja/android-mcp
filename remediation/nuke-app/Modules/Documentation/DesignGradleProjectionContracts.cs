using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record DesignGradleProjection(Artifact Descriptor,Artifact BuildScript);
public sealed record DesignGradleProjectionIssue(string Code,string RelativePath);
public interface IDesignGradleProjection
{
    DesignGradleProjection Render(DesignStageManifest stages,OperationDefinition operation);
    IReadOnlyList<DesignGradleProjectionIssue> Check(DesignStageManifest stages,OperationDefinition operation,
        IReadOnlyList<Artifact> retainedProjection);
}
