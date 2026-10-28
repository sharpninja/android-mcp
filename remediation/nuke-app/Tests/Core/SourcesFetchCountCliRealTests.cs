using System.Text.Json;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesFetchCountCliProbe:ISourcesFetchCountCliProbe
{
    public async Task<SourcesFetchCountCliObservation> RunAsync()
    {
        // Reuse the existing two-child native fixture and all original assertions.
        var acquisition=await new NativeSourcesFetchCliProbe().RunAsync();
        var root=Path.GetDirectoryName(acquisition.RuntimeRoot)??throw new InvalidOperationException("Fixture root unavailable");
        async Task<JsonElement> Summary(int child)
        {
            var stdout=await File.ReadAllTextAsync(Path.Combine(root,"process-"+child,"stdout.bin"));
            var line=Assert.Single(stdout.Split('\n').Select(value=>value.Trim()),value=>value.StartsWith("{\"Manifest\":",StringComparison.Ordinal));
            using var document=JsonDocument.Parse(line);return document.RootElement.Clone();
        }
        return new(acquisition,await Summary(1),await Summary(2));
    }
}
[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchCountCliRealTests
{
    [Fact]public Task SameFixtureCurrentPhysicalCountsAndImmutableReceiptAssertions()
        =>SourcesFetchCountCliCases.AssertCase(new NativeSourcesFetchCountCliProbe());
}
