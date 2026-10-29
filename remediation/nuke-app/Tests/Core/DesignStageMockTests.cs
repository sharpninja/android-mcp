using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record DesignStageCase(string Id, string[] Aliases, DesignStage[] Stages,
    DesignStageError[] Errors, string? DigestOverride = null);

// Independent transcription of the retained 13 Gradle task contracts, including
// historical UI side effects. These values are never obtained from the planner.
public static class DesignStageCases
{
    private static DesignStage[] S(params DesignStage[] values) => values;
    private static readonly DesignStage[] Architecture = S(DesignStage.Tools, DesignStage.UiAssets,
        DesignStage.ModuleGraph, DesignStage.ModelContracts, DesignStage.CodeExtract,
        DesignStage.CodeRender, DesignStage.ArchitectureValidate);
    public static readonly DesignStageMetadata ExpectedMetadata = new(1,
    [
        new(DesignStage.Tools, []), new(DesignStage.UiAssets, []),
        new(DesignStage.ModuleGraph, [DesignStage.Tools]),
        new(DesignStage.ModelContracts, [DesignStage.Tools, DesignStage.UiAssets]),
        new(DesignStage.CodeExtract, []),
        new(DesignStage.CodeRender, [DesignStage.Tools, DesignStage.CodeExtract]),
        new(DesignStage.ArchitectureValidate, [DesignStage.ModuleGraph, DesignStage.ModelContracts, DesignStage.CodeRender]),
        new(DesignStage.ArchitectureExport, [DesignStage.ArchitectureValidate]),
        new(DesignStage.ArchitectureRender, [DesignStage.ArchitectureExport]),
        new(DesignStage.DocsVerify, [DesignStage.ArchitectureRender, DesignStage.UiAssets]),
        new(DesignStage.Manifest, [DesignStage.DocsVerify]), new(DesignStage.Package, [DesignStage.Manifest])
    ],
    [
        new("verifyToolchain", [DesignStage.Tools]),
        new("validateModuleGraph", [DesignStage.ModuleGraph]),
        new("validateModelContracts", [DesignStage.ModelContracts]),
        new("extractCodeViews", [DesignStage.CodeExtract]),
        new("renderCodeViews", [DesignStage.CodeRender]),
        new("validateArchitecture", [DesignStage.ArchitectureValidate]),
        new("exportArchitecture", [DesignStage.ArchitectureExport]),
        new("generateArchitectureDiagrams", [DesignStage.ArchitectureRender]),
        new("generateUiAssets", [DesignStage.Tools, DesignStage.UiAssets]),
        new("renderStoryboards", [DesignStage.Tools, DesignStage.UiAssets]),
        new("verifyDesignDocs", [DesignStage.DocsVerify]),
        new("generateArtifactManifest", [DesignStage.Manifest]),
        new("packageDesignDocs", [DesignStage.Package])
    ]);
    public static string Digest => new CanonicalJson().Hash(JsonSerializer.SerializeToElement(ExpectedMetadata));
    public static IEnumerable<object[]> Cases()
    {
        yield return C("tools", ["verifyToolchain"], S(DesignStage.Tools));
        yield return C("module", ["validateModuleGraph"], S(DesignStage.Tools, DesignStage.ModuleGraph));
        yield return C("model-ui-side-effect", ["validateModelContracts"], S(DesignStage.Tools, DesignStage.UiAssets, DesignStage.ModelContracts));
        yield return C("extract-only", ["extractCodeViews"], S(DesignStage.CodeExtract));
        yield return C("code-render", ["renderCodeViews"], S(DesignStage.Tools, DesignStage.CodeExtract, DesignStage.CodeRender));
        yield return C("validate", ["validateArchitecture"], Architecture);
        yield return C("export", ["exportArchitecture"], [.. Architecture, DesignStage.ArchitectureExport]);
        yield return C("all-diagrams", ["generateArchitectureDiagrams"], [.. Architecture, DesignStage.ArchitectureExport, DesignStage.ArchitectureRender]);
        yield return C("ui", ["generateUiAssets"], S(DesignStage.Tools, DesignStage.UiAssets));
        yield return C("storyboard-same-producer", ["renderStoryboards"], S(DesignStage.Tools, DesignStage.UiAssets));
        yield return C("docs", ["verifyDesignDocs"], [.. Architecture, DesignStage.ArchitectureExport, DesignStage.ArchitectureRender, DesignStage.DocsVerify]);
        yield return C("manifest", ["generateArtifactManifest"], [.. Architecture, DesignStage.ArchitectureExport, DesignStage.ArchitectureRender, DesignStage.DocsVerify, DesignStage.Manifest]);
        yield return C("package", ["packageDesignDocs"], Enum.GetValues<DesignStage>());
        yield return C("overlapping-union-once", ["generateUiAssets", "validateModelContracts", "renderStoryboards", "validateModelContracts"], S(DesignStage.Tools, DesignStage.UiAssets, DesignStage.ModelContracts));
        yield return C("all-thirteen-once", ExpectedMetadata.Aliases.Select(x => x.Name).Reverse().ToArray(), Enum.GetValues<DesignStage>());
        yield return C("subset-with-no-tools", ["extractCodeViews"], S(DesignStage.CodeExtract));
        yield return C("unknown-no-effects", ["generateUiAssets", "notATask"], [], [new("UNKNOWN_ALIAS", "notATask")]);
        yield return C("case-sensitive", ["VerifyToolchain"], [], [new("UNKNOWN_ALIAS", "VerifyToolchain")]);
        yield return C("empty-selection", [], [], [new("EMPTY_SELECTION", "")]);
        yield return [new DesignStageCase("stale-metadata-no-effects", ["packageDesignDocs"], [], [new("REGISTRY_MISMATCH", new string('0', 64))], new string('0', 64))];
    }
    private static object[] C(string id, string[] aliases, DesignStage[] stages, DesignStageError[]? errors = null) =>
        [new DesignStageCase(id, aliases, stages, errors ?? [])];
    public static void AssertCase(DesignStageCase test, IDesignStagePlanner planner)
    {
        var manifest = planner.Describe();
        Assert.Equal(Digest, manifest.Sha256);
        Assert.Equal(new CanonicalJson().Hash(JsonSerializer.SerializeToElement(ExpectedMetadata)),
            new CanonicalJson().Hash(JsonSerializer.SerializeToElement(manifest.Metadata)));
        var result = planner.Select(new(test.Aliases, test.DigestOverride ?? Digest));
        Assert.Equal(Digest, result.RegistrySha256);
        Assert.Equal(test.Stages, result.Stages);
        Assert.Equal(test.Errors, result.Errors);
        Assert.Equal(test.Errors.Length == 0 ? test.Aliases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [], result.Aliases);
        Assert.Equal(result.Stages.Count, result.Stages.Distinct().Count());
    }
    public static DesignStagePlan Correct(DesignStageCase test) => new(test.Stages,
        test.Errors.Length == 0 ? test.Aliases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [], Digest, test.Errors);
}

public sealed class ScriptedDesignStagePlanner(DesignStageManifest manifest, DesignStagePlan plan) : IDesignStagePlanner
{
    public DesignStageManifest Describe() => manifest;
    public DesignStagePlan Select(DesignStageRequest request) => plan;
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-005")]
public sealed class DesignStageMockTests
{
    [Theory, MemberData(nameof(DesignStageCases.Cases), MemberType = typeof(DesignStageCases))]
    public void CorrectMockExercisesExactSelection(DesignStageCase test) => DesignStageCases.AssertCase(test,
        new ScriptedDesignStagePlanner(new(DesignStageCases.ExpectedMetadata, DesignStageCases.Digest), DesignStageCases.Correct(test)));

    [Theory, MemberData(nameof(DesignStageCases.Cases), MemberType = typeof(DesignStageCases))]
    public void IndependentWrongResponsesAreRejected(DesignStageCase test)
    {
        var good = DesignStageCases.Correct(test);
        var mutations = new[]
        {
            good with { RegistrySha256 = new string('f',64) },
            good with { Stages = [.. good.Stages, DesignStage.Package] },
            good with { Aliases = [.. good.Aliases, "extra"] },
            good with { Errors = [.. good.Errors, new("WRONG", "wrong")] }
        };
        foreach (var wrong in mutations)
            Assert.NotNull(Record.Exception(() => DesignStageCases.AssertCase(test,
                new ScriptedDesignStagePlanner(new(DesignStageCases.ExpectedMetadata, DesignStageCases.Digest), wrong))));
        var badManifest = DesignStageCases.ExpectedMetadata with { SchemaVersion = 2 };
        Assert.NotNull(Record.Exception(() => DesignStageCases.AssertCase(test,
            new ScriptedDesignStagePlanner(new(badManifest, DesignStageCases.Digest), good))));
        if (good.Stages.Count > 0)
            Assert.NotNull(Record.Exception(() => DesignStageCases.AssertCase(test,
                new ScriptedDesignStagePlanner(new(DesignStageCases.ExpectedMetadata, DesignStageCases.Digest), good with { Stages = good.Stages.Skip(1).ToArray() }))));
    }
}
