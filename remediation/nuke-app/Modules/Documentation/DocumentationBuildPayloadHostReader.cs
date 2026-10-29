using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed class DocumentationBuildPayloadHostReader(IPinnedArtifactReader reader,IStrictJsonInspector strict)
    :IDocumentationBuildPayloadHostReader
{
    private static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public async Task<DocumentationBuildPayloadHostConfiguration> ReadAsync(StoredArtifact request,string expectedDotNetHost,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes=await reader.ReadAsync(request,cancellationToken);
        DocumentationBuildPayloadHostConfiguration value;
        try
        {
            var inspected=strict.Inspect(bytes);
            if(inspected.Value is null||inspected.CanonicalDiagnostics.Count!=0)throw Error();
            value=JsonSerializer.Deserialize<DocumentationBuildPayloadHostConfiguration>(inspected.Value.Value.GetRawText(),Options)??throw Error();
            if(value.SchemaVersion!=1||value.DotNet is null||string.IsNullOrWhiteSpace(value.DotNet.ExecutablePath)||
                value.DotNet.Sha256 is not {Length:64}||!value.DotNet.Sha256.All(char.IsAsciiHexDigit)||string.IsNullOrWhiteSpace(value.DotNet.Version)||
                value.Native is null||value.Native.Sdk is null||value.Native.Sdk.AssemblyFiles is not {Count:>0}||
                !Path.IsPathFullyQualified(value.Native.Sdk.SdkDirectory)||string.IsNullOrWhiteSpace(value.Native.TargetFramework)||
                value.Native.ApprovedReadRoots is not {Count:>0}||value.Native.ApprovedReadRoots.Any(root=>!Path.IsPathFullyQualified(root))||
                value.PrerequisiteScopes is null)throw Error();
        }
        catch(Exception error)when(error is JsonException or InvalidOperationException or ArgumentException){throw Error();}
        if(!Path.IsPathFullyQualified(value.DotNet.ExecutablePath)||value.DotNet.ExecutablePath!=expectedDotNetHost)
            throw new DocumentationBuildPayloadException("BUILD_SOURCE_HOST_TOOL");
        return value with{Native=value.Native with{ApprovedReadRoots=Array.AsReadOnly(value.Native.ApprovedReadRoots.ToArray()),
            Sdk=value.Native.Sdk with{AssemblyFiles=Array.AsReadOnly(value.Native.Sdk.AssemblyFiles.ToArray())}},
            PrerequisiteScopes=Array.AsReadOnly(value.PrerequisiteScopes.ToArray())};
    }
    private static DocumentationBuildPayloadException Error()=>new("BUILD_SOURCE_REQUEST");
}
