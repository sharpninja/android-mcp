namespace AndroidMcp.BuildTool.Core;

// The C# registry owns this vocabulary. Gradle consumes a generated projection.
public enum DesignStage
{
    Tools, UiAssets, ModuleGraph, ModelContracts, CodeExtract, CodeRender,
    ArchitectureValidate, ArchitectureExport, ArchitectureRender, DocsVerify, Manifest, Package
}
public sealed record DesignStageDefinition(DesignStage Stage, IReadOnlyList<DesignStage> DependsOn);
public sealed record DesignAliasDefinition(string Name, IReadOnlyList<DesignStage> Selects);
public sealed record DesignStageMetadata(int SchemaVersion, IReadOnlyList<DesignStageDefinition> Stages,
    IReadOnlyList<DesignAliasDefinition> Aliases);
public sealed record DesignStageManifest(DesignStageMetadata Metadata, string Sha256);
public sealed record DesignStageRequest(IReadOnlyList<string> Aliases, string ExpectedRegistrySha256);
public sealed record DesignStageError(string Code, string Value);
public sealed record DesignStagePlan(IReadOnlyList<DesignStage> Stages, IReadOnlyList<string> Aliases,
    string RegistrySha256, IReadOnlyList<DesignStageError> Errors);
public interface IDesignStagePlanner
{
    DesignStageManifest Describe();
    DesignStagePlan Select(DesignStageRequest request);
}
