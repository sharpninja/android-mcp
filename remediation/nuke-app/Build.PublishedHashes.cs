using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ExtractPublishedHashes(string project, CanonicalJson canonical, ReadOnlyMemory<byte> admitted)
    {
        var wire = JsonSerializer.Deserialize<PublishedInterfaceHashWorkflowFile>(admitted.Span)
            ?? throw new ArgumentException("Published interface hash workflow request is required");
        var generationRoot = AdmitWorkflowRoot(project, wire.GenerationStoreRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(project, generationRoot, comparison)) throw new ArgumentException("Hash generation store must be separate from retained inputs");
        var inputs = new FileArtifactStore(project, bytes => ContentHash.Sha256(bytes.Span));
        var output = new FileArtifactStore(generationRoot, bytes => ContentHash.Sha256(bytes.Span));
        var identity = new SourceProducerIdentityBuilder().Create();
        var workflow = new PublishedInterfaceHashWorkflow(inputs, output, new NativeArtifactWorkspace(generationRoot),
            new FileArtifactLeaseProvider(Path.Combine(RootDirectory.ToString(), ".nuke", "source-extract-leases")), new SourcesRuntimeFactory(), canonical, new SystemClock());
        var result = await workflow.RunAsync(new(wire.Inventory, wire.GenerationRoot, SourceExtractionProfile.Resolve(wire.ProfileId), identity.Identity));
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(result.Summary), JsonSerializationProfile.LegacyAsciiIdentity)));
    }
}
