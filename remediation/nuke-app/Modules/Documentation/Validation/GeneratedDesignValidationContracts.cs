using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Validation;

public enum DocumentationValidationPhase { BeforeManifest,AfterManifest }

// The caller supplies its admitted current source/output union and the model
// already parsed by IDocumentationModelReader. No repository scan is implied.
public sealed record GeneratedDesignValidationInput(
    IReadOnlyList<Artifact> Artifacts,DocumentationModel Model,
    Artifact WorkspaceDsl,Artifact UiManifest,DocumentationValidationPhase Phase);

public sealed record DocumentationValidationCounters(
    int ChecksumsChecked,int MarkdownLinksChecked,int RenderedSvgCount,int EditableUiCopies);

// Keep BCL XML diagnostics separately from stable rule codes. Message text may
// vary with the host framework, but no per-file parse failure is discarded.
public sealed record DocumentationParseDiagnostic(string ArtifactPath,int Line,int Column,string Message);

public sealed record GeneratedDesignValidationResult(
    DocumentationValidationPhase Phase,DocumentationValidationCounters Counters,
    IReadOnlyList<string> ExpectedSvgPaths,IReadOnlyList<string> ActualSvgPaths,
    IReadOnlyList<DesignIssue> Issues,IReadOnlyList<DocumentationParseDiagnostic> ParseDiagnostics)
{
    public string Scope=>"generated-design";
    public bool IsValid=>Issues.Count==0;
}

public interface IGeneratedDesignValidator
{
    GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input);
}
