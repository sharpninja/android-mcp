namespace AndroidMcp.BuildTool.Documentation.Browser;

// Declaration only. The existing BrowserQaRequest will receive an optional
// trailing GenerationSelection after the mock gate, retaining all defaults.
public sealed record BrowserQaGenerationSelection(string ProjectRoot,string UiManifestSha256,
    string? ArchitectureRoot=null,string? ArchitectureManifestSha256=null);
public sealed class BrowserQaInputException(string code,string message):IOException(message)
{public string Code {get;}=code;}
