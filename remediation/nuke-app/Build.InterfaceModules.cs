using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ExtractInterfaceModules(string project, CanonicalJson canonical, ReadOnlyMemory<byte> admitted)
    {
        var wire = JsonSerializer.Deserialize<InterfaceModuleWorkflowFile>(admitted.Span)
            ?? throw new ArgumentException("Interface module workflow request is required");
        var generationRoot = AdmitWorkflowRoot(project, wire.GenerationStoreRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(project, generationRoot, comparison)) throw new ArgumentException("Interface module generation store must be separate from retained inputs");
        var inputs = new FileArtifactStore(project, bytes => ContentHash.Sha256(bytes.Span));
        var output = new FileArtifactStore(generationRoot, bytes => ContentHash.Sha256(bytes.Span));
        var identity = new SourceProducerIdentityBuilder().Create();
        var workflow = new InterfaceModuleWorkflow(inputs, output, output,
            new FileArtifactLeaseProvider(Path.Combine(RootDirectory.ToString(), ".nuke", "source-extract-leases")), new SourcesRuntimeFactory(), canonical, new LegacyJsonValueEquality(), new SystemClock());
        var result = await workflow.RunAsync(new(wire.Inventory, wire.DeclaredSources, wire.GenerationRoot, SourceExtractionProfile.Resolve(wire.ProfileId), identity.Identity));
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(result.Report), JsonSerializationProfile.LegacyAsciiIdentity)));
    }
}
