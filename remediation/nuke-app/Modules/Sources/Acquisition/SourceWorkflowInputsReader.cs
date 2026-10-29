using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SourceWorkflowInputsReader(IPinnedArtifactReader reader):ISourceWorkflowInputsReader
{
    public async Task<RehydrationPlanRequest> ReadAsync(SourceWorkflowInputs inputs,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recipe=await reader.ReadAsync(inputs.Recipe,cancellationToken);
        var recipeLock=await reader.ReadAsync(inputs.RecipeLock,cancellationToken);
        return new(new(inputs.Recipe.RelativePath,recipe,inputs.Recipe.MediaType),
            new(inputs.RecipeLock.RelativePath,recipeLock,inputs.RecipeLock.MediaType),
            inputs.Release,inputs.SourceLimit,Profile:inputs.AcquisitionProfile);
    }
}
