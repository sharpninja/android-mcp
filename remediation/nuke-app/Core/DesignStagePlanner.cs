using System.Collections.ObjectModel;
using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Single design-stage graph and Gradle alias vocabulary. No execution or host I/O.</summary>
public sealed class DesignStagePlanner : IDesignStagePlanner
{
    private readonly DesignStageManifest manifest;
    private readonly IReadOnlyDictionary<DesignStage, DesignStageDefinition> stages;
    private readonly IReadOnlyDictionary<string, DesignAliasDefinition> aliases;

    public DesignStagePlanner() : this(new CanonicalJson()) { }
    public DesignStagePlanner(ICanonicalJson canonical)
    {
        static ReadOnlyCollection<T> R<T>(params T[] values) => Array.AsReadOnly(values);
        static DesignStageDefinition D(DesignStage stage, params DesignStage[] dependencies) => new(stage, R(dependencies));
        static DesignAliasDefinition A(string alias, params DesignStage[] selects) => new(alias, R(selects));
        var metadata = new DesignStageMetadata(1, R(
            D(DesignStage.Tools), D(DesignStage.UiAssets), D(DesignStage.ModuleGraph, DesignStage.Tools),
            D(DesignStage.ModelContracts, DesignStage.Tools, DesignStage.UiAssets), D(DesignStage.CodeExtract),
            D(DesignStage.CodeRender, DesignStage.Tools, DesignStage.CodeExtract),
            D(DesignStage.ArchitectureValidate, DesignStage.ModuleGraph, DesignStage.ModelContracts, DesignStage.CodeRender),
            D(DesignStage.ArchitectureExport, DesignStage.ArchitectureValidate),
            D(DesignStage.ArchitectureRender, DesignStage.ArchitectureExport),
            D(DesignStage.DocsVerify, DesignStage.ArchitectureRender, DesignStage.UiAssets),
            D(DesignStage.Manifest, DesignStage.DocsVerify), D(DesignStage.Package, DesignStage.Manifest)),
            R(A("verifyToolchain", DesignStage.Tools), A("validateModuleGraph", DesignStage.ModuleGraph),
                A("validateModelContracts", DesignStage.ModelContracts), A("extractCodeViews", DesignStage.CodeExtract),
                A("renderCodeViews", DesignStage.CodeRender), A("validateArchitecture", DesignStage.ArchitectureValidate),
                A("exportArchitecture", DesignStage.ArchitectureExport), A("generateArchitectureDiagrams", DesignStage.ArchitectureRender),
                A("generateUiAssets", DesignStage.Tools, DesignStage.UiAssets), A("renderStoryboards", DesignStage.Tools, DesignStage.UiAssets),
                A("verifyDesignDocs", DesignStage.DocsVerify), A("generateArtifactManifest", DesignStage.Manifest), A("packageDesignDocs", DesignStage.Package)));
        manifest = new(metadata, canonical.Hash(JsonSerializer.SerializeToElement(metadata)));
        stages = new ReadOnlyDictionary<DesignStage, DesignStageDefinition>(metadata.Stages.ToDictionary(x => x.Stage));
        aliases = new ReadOnlyDictionary<string, DesignAliasDefinition>(metadata.Aliases.ToDictionary(x => x.Name, StringComparer.Ordinal));
    }

    public DesignStageManifest Describe() => manifest;

    public DesignStagePlan Select(DesignStageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Aliases);
        DesignStagePlan Fail(params DesignStageError[] errors) => new([], [], manifest.Sha256, Array.AsReadOnly(errors));
        if (!string.Equals(request.ExpectedRegistrySha256, manifest.Sha256, StringComparison.Ordinal))
            return Fail(new DesignStageError("REGISTRY_MISMATCH", request.ExpectedRegistrySha256));
        if (request.Aliases.Count == 0) return Fail(new DesignStageError("EMPTY_SELECTION", ""));
        var selectedAliases = request.Aliases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var errors = selectedAliases.Where(alias => alias is null || !aliases.ContainsKey(alias))
            .Select(alias => new DesignStageError("UNKNOWN_ALIAS", alias ?? "")).ToArray();
        if (errors.Length > 0) return Fail(errors);
        var selectedStages = new HashSet<DesignStage>();
        void Include(DesignStage stage)
        {
            if (!selectedStages.Add(stage)) return;
            foreach (var dependency in stages[stage].DependsOn) Include(dependency);
        }
        foreach (var alias in selectedAliases)
            foreach (var stage in aliases[alias].Selects) Include(stage);
        // Metadata order is the declared deterministic topological order.
        return new(Array.AsReadOnly(manifest.Metadata.Stages.Where(x => selectedStages.Contains(x.Stage)).Select(x => x.Stage).ToArray()),
            Array.AsReadOnly(selectedAliases), manifest.Sha256, []);
    }
}
