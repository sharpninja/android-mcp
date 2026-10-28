using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;

namespace AndroidMcp.BuildTool.Tests.Core;

// Test fixture continuation, not an external workflow or a second source-row projection.
// Catalog nests Workflow.Sources/Stores and supplies its own explicit consumer metadata.
public sealed record SourcesExtractCliContinuation(SourcesExtractWorkflowFile Workflow,StoredArtifact ExtractIndex,
    string ProjectRoot,string RuntimeRoot);
public static class SourcesExtractCliFixture
{
    public static async Task<SourcesExtractCliContinuation> CreateAsync()
    {
        var observed=await new NativeSourcesExtractCliProbe().RunAsync();
        await SourcesExtractCliCases.AssertCase(new Retained(observed));
        return new(observed.SecondWorkflow,observed.SecondSummary.GetProperty("Index").Deserialize<StoredArtifact>()
            ??throw new InvalidOperationException("Successful child did not return its generation pin"),observed.ProjectRoot,observed.RuntimeRoot);
    }
    private sealed class Retained(SourcesExtractCliObservation observed):ISourcesExtractCliProbe
    {public Task<SourcesExtractCliObservation> RunAsync()=>Task.FromResult(observed);}
}
