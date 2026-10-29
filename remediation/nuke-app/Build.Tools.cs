using System.Text.Json;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    // One reader preserves the existing compiler identity-file semantics.
    // Native process admission remains the authority for executable pins.
    private static async Task<ToolIdentity> ReadToolIdentity(string path,string description="Tool") =>
        JsonSerializer.Deserialize<ToolIdentity>(await File.ReadAllBytesAsync(path),
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
        ??throw new InvalidOperationException(description+" identity is missing.");
}
