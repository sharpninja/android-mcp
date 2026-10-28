using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Contract only. No production projection implementation exists before the
// independent mock assertion gate is accepted.
public sealed record DocumentationInput(Artifact BlueprintMarkdown, Artifact DesignMarkdown);
public sealed record DocumentationProjection(IReadOnlyList<Artifact> Artifacts);

public interface IDocumentationProjector
{
    DocumentationProjection Project(DocumentationInput input);
}

public sealed record DesignIssue(string Code, string ArtifactPath, string Subject);
public sealed record ModuleValidationInput(System.Text.Json.Nodes.JsonObject ModuleContract, string CodeMarkdown);
public sealed record ModuleValidationResult(int ClassCount, IReadOnlyList<string> ModuleEdges, IReadOnlyList<DesignIssue> Issues,
    IReadOnlyList<string>? DeclaredClasses=null);
public interface IModuleGraphValidator
{
    ModuleValidationResult Validate(ModuleValidationInput input);
}

public sealed record FrozenEvidenceInput(string HtmlPath, IReadOnlyList<Artifact> Artifacts, IReadOnlyList<StoredArtifact> Manifest);
public sealed record FactoredEvidence(Artifact ReviewHtml, Artifact ReconstructionMap, Artifact Verification);
public sealed class EvidenceIntegrityException(string code,string message) : IOException(message)
{
    public string Code { get; }=code;
}
public interface ILosslessEvidenceService
{
    FactoredEvidence Factor(FrozenEvidenceInput input);
    Artifact Reconstruct(FrozenEvidenceInput input,Artifact reviewHtml,Artifact reconstructionMap);
}

public static class DocumentationAssetPaths
{
    public const string Blueprint="docs/ui-blueprint.md";
    public const string Design="docs/Android MCP Bridge Design.md";
    public const string Manifest="generation-manifest.json";
}
public sealed record DocumentationGeneration(string GenerationId,IReadOnlyList<StoredArtifact> Inputs,IReadOnlyList<StoredArtifact> Outputs,StoredArtifact Manifest);
public sealed class DocumentationAssetException(string code,string message,Exception? inner=null) : IOException(message,inner)
{
    public string Code { get; }=code;
}
public interface IDocumentationAssetOperation
{
    Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default);
}
