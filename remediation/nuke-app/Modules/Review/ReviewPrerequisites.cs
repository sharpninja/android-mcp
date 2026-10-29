using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewPrerequisiteConfiguration(string WorkspacePath, string PluginName, string PluginVersion,
    ReviewEvidence EvaluationReceipt, ReviewEvidence RequirementsReceipt, ReviewEvidence ChecklistReceipt,
    IReadOnlyList<string> ScopeIntegrationProjects);
public sealed record ReviewPrerequisites(string Head, IReadOnlyList<string> UnitProjects,
    IReadOnlyList<string> IntegrationProjects, IReadOnlyList<string> RequirementIds,
    StoredArtifact EvaluationReceipt, StoredArtifact RequirementsReceipt,
    StoredArtifact ChecklistReceipt, StoredArtifact InventoryReceipt);
public interface IReviewPrerequisiteSource
{
    Task<ReviewPrerequisites> ObserveAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>Trusted composition supplies pinned native-evaluation and official-plugin receipts.
/// Git observations independently establish the complete project-definition denominator at the exact head.</summary>
public sealed class GitReviewPrerequisiteSource(IProcessRunner runner, IProcessCaptureReader captures,
    IArtifactStore store, ICanonicalJson canonical, Func<ReadOnlyMemory<byte>, string> sha256,
    ToolIdentity git, string workingDirectory, string privateReceiptRoot, ReviewPrerequisiteConfiguration configuration)
    : IReviewPrerequisiteSource
{
    public static IReadOnlyList<string> RequiredChecks { get; } = System.Array.AsReadOnly(new[]
    {
        "holistic-review", "what-if", "propagation-audit", "phase-boundaries", "search-before-adding",
        "no-new-mechanism", "mapping-lint", "verbatim-user-scope", "delegated-standing-rules"
    });

    public async Task<ReviewPrerequisites> ObserveAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ReviewPrepareService.Require(Regex.IsMatch(snapshot.Head, "^[0-9a-fA-F]{40,64}$"), "HEAD_MISMATCH", "Git discovery requires an immutable full commit ID.");
        var invocation = Guid.NewGuid().ToString("N");
        var sequence = 0;
        async Task<ReadOnlyMemory<byte>> GitAsync(params string[] args)
        {
            var result = await runner.RunAsync(new(git, args, workingDirectory, new Dictionary<string, string>(),
                Path.Combine(privateReceiptRoot, snapshot.RunId, invocation, (++sequence).ToString("D4"))), cancellationToken);
            ReviewPrepareService.Require(result.ExitCode == 0 && !result.Cancelled, "PREREQUISITE_UNAVAILABLE", "Read-only Git observation failed.");
            return await captures.ReadAsync(result, false, cancellationToken);
        }
        var observedHead = ReviewPrepareService.Utf8.GetString((await GitAsync("rev-parse", "--verify", snapshot.Head + "^{commit}")).Span).Trim();
        ReviewPrepareService.Require(observedHead == snapshot.Head, "HEAD_MISMATCH", "Git returned a different commit.");
        var tree = await GitAsync("ls-tree", "-r", "--name-only", "-z", snapshot.Head);
        var projectPaths = ReviewPrepareService.Utf8.GetString(tree.Span).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(IsProjectDefinition).Order(StringComparer.Ordinal).ToArray();
        ReviewPrepareService.Require(projectPaths.Length > 0 && projectPaths.Distinct(StringComparer.Ordinal).Count() == projectPaths.Length,
            "PROJECT_METADATA_UNRESOLVED", "No complete, unique project-definition inventory was observed.");
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in projectPaths)
        {
            ReviewPrepareService.Require(!Path.IsPathRooted(path) && !path.Split('/').Contains(".."), "INVALID_PATH", "Invalid Git project path.");
            definitions.Add(path, sha256(await GitAsync("show", snapshot.Head + ":" + path)));
        }

        async Task<(JsonDocument Document, StoredArtifact Artifact)> ReceiptAsync(ReviewEvidence evidence, string errorCode)
        {
            ReadOnlyMemory<byte> bytes;
            try { bytes = await store.ReadAsync(evidence.Path, cancellationToken); }
            catch (FileNotFoundException) { throw new ReviewException(errorCode, "A required trusted receipt is missing: " + evidence.Path); }
            ReviewPrepareService.Require(sha256(bytes) == evidence.Sha256, "EVIDENCE_HASH_MISMATCH", evidence.Path);
            try
            {
                _ = canonical.Serialize(bytes); // One shared authority rejects duplicate fields and malformed JSON.
                var document = JsonDocument.Parse(bytes);
                ReviewPrepareService.Require(Text(document.RootElement, "head") == snapshot.Head, "HEAD_MISMATCH", evidence.Path);
                return (document, new(evidence.Path, evidence.Sha256, bytes.Length, "application/json"));
            }
            catch (JsonException error) { throw new ReviewException(errorCode, error.Message); }
            catch (CanonicalJsonException error) { throw new ReviewException(errorCode, error.Message); }
        }

        var evaluated = await ReceiptAsync(configuration.EvaluationReceipt, "PROJECT_METADATA_UNRESOLVED");
        using var evaluation = evaluated.Document;
        var e = evaluation.RootElement;
        ReviewPrepareService.Require(Text(e, "source") == "native-project-evaluation" && !string.IsNullOrWhiteSpace(Text(e, "toolSha256")),
            "PROJECT_METADATA_UNRESOLVED", "Native evaluation provenance is absent.");
        var projects = Array(e, "projects", "PROJECT_METADATA_UNRESOLVED");
        ReviewPrepareService.Require(projects.Select(x => Text(x, "path")).ToHashSet(StringComparer.Ordinal).SetEquals(projectPaths) && projects.Length == projectPaths.Length,
            "PROJECT_METADATA_UNRESOLVED", "Native evaluation omits or adds a project definition.");
        var units = new List<string>();
        var integrations = new List<string>();
        foreach (var project in projects)
        {
            var path = Text(project, "path");
            ReviewPrepareService.Require(Text(project, "definitionSha256") == definitions[path], "EVIDENCE_HASH_MISMATCH", path);
            ReviewPrepareService.Require(project.TryGetProperty("isTestProject", out var test) && test.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "PROJECT_METADATA_UNRESOLVED", "Test metadata must be explicitly evaluated; filenames are not evidence.");
            var kind = Text(project, "kind");
            if (test.GetBoolean())
            {
                ReviewPrepareService.Require(kind is "unit" or "integration" or "mixed", "PROJECT_METADATA_UNRESOLVED", "Test kind is unresolved.");
                if (kind is "unit" or "mixed") units.Add(path);
                if (kind is "integration" or "mixed") integrations.Add(path);
            }
            else ReviewPrepareService.Require(kind == "none", "PROJECT_METADATA_UNRESOLVED", "Non-test project classification is unresolved.");
        }

        // A mixed project is one definition and one full, unfiltered native run.
        // The trusted native producer owns this provenance; no role/filename heuristic is used.
        foreach (var project in units.Intersect(integrations, StringComparer.Ordinal))
        {
            const string code = "FULL_TEST_RUN_UNVERIFIED";
            var runs = Array(e, "fullUnfilteredTrx", code).Where(x => Text(x, "project") == project).ToArray();
            ReviewPrepareService.Require(runs.Length == 1, code, "Mixed project needs exactly one full native TRX origin.");
            var run = runs[0];
            ReviewPrepareService.Require(run.TryGetProperty("filter", out var filter) && filter.ValueKind == JsonValueKind.Null,
                code, "A filtered/unknown run cannot establish both coverage roles.");
            var evidence = snapshot.Evidence.Where(x => x.Kind is "unit-trx" or "integration-trx" && x.Project == project).ToArray();
            ReviewPrepareService.Require(evidence.Length == 1, code, "Mixed project must reuse one full TRX body.");
            var item = evidence[0];
            ReviewPrepareService.Require(Text(run, "path") == item.Path && Text(run, "sha256") == item.Sha256 &&
                Text(run, "head") == snapshot.Head && item.Head == snapshot.Head && Text(run, "runId") == item.RunId &&
                Text(run, "host") == item.Host && Text(run, "command") == item.Command,
                code, "The full native TRX origin differs from supplied evidence.");
        }

        var requirements = await ReceiptAsync(configuration.RequirementsReceipt, "REQUIREMENTS_INCOMPLETE");
        using var requirementDocument = requirements.Document;
        var r = requirementDocument.RootElement;
        ReviewPrepareService.Require(Text(r, "source") == "required-mcp-plugin" && Text(r, "plugin") == configuration.PluginName &&
            Text(r, "pluginVersion") == configuration.PluginVersion && Text(r, "workspace") == configuration.WorkspacePath,
            "REQUIREMENTS_INCOMPLETE", "Requirements must come from the verified required plugin and active workspace.");
        var requirementIds = ValidateRequirements(r);

        var checkedReceipt = await ReceiptAsync(configuration.ChecklistReceipt, "PREFLIGHT_INCOMPLETE");
        using var checklist = checkedReceipt.Document;
        var c = checklist.RootElement;
        ReviewPrepareService.Require(Text(c, "scope") == "design-and-nuke-only", "PREFLIGHT_INCOMPLETE", "Explicit user scope is missing.");
        var scopeIntegrations = Strings(c, "scopeIntegrationProjects", "INTEGRATION_SCOPE_INCOMPLETE");
        ReviewPrepareService.Require(scopeIntegrations.ToHashSet(StringComparer.Ordinal).SetEquals(configuration.ScopeIntegrationProjects) &&
            configuration.ScopeIntegrationProjects.All(x => integrations.Contains(x, StringComparer.Ordinal)),
            "INTEGRATION_SCOPE_INCOMPLETE", "Scoped integration lanes do not match the trusted review plan.");
        var checks = Array(c, "checks", "PREFLIGHT_INCOMPLETE");
        ReviewPrepareService.Require(checks.Length == RequiredChecks.Count && checks.Select(x => Text(x, "code")).ToHashSet(StringComparer.Ordinal).SetEquals(RequiredChecks),
            "PREFLIGHT_INCOMPLETE", "Every per-round self-review check is mandatory.");
        foreach (var check in checks)
        {
            ReadOnlyMemory<byte> bytes;
            try { bytes = await store.ReadAsync(Text(check, "path"), cancellationToken); }
            catch (FileNotFoundException) { throw new ReviewException("PREFLIGHT_INCOMPLETE", "Checklist evidence is missing."); }
            ReviewPrepareService.Require(sha256(bytes) == Text(check, "sha256"), "EVIDENCE_HASH_MISMATCH", "Checklist evidence changed.");
        }
        var inventory = canonical.Serialize(ReviewPrepareService.Json(new { snapshot.Head, snapshot.RunId,
            definitions, unitProjects = units.Order(StringComparer.Ordinal), scopeIntegrationProjects = configuration.ScopeIntegrationProjects.Order(StringComparer.Ordinal),
            evaluationSha256 = evaluated.Artifact.Sha256, requirementsSha256 = requirements.Artifact.Sha256, checklistSha256 = checkedReceipt.Artifact.Sha256 }));
        var artifact = await store.WriteAsync(new($"reviews/{snapshot.RunId}/project-inventory.json", inventory, "application/json"), cancellationToken);
        return new(snapshot.Head, units.Order(StringComparer.Ordinal).ToArray(), configuration.ScopeIntegrationProjects.Order(StringComparer.Ordinal).ToArray(),
            requirementIds, evaluated.Artifact, requirements.Artifact, checkedReceipt.Artifact, artifact);
    }

    private static bool IsProjectDefinition(string path) => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(path) is "build.gradle" or "build.gradle.kts" or "pom.xml" or "package.json";

    private static IReadOnlyList<string> ValidateRequirements(JsonElement root)
    {
        const string code = "REQUIREMENTS_INCOMPLETE";
        var functional = Array(root, "functional", code);
        var technical = Array(root, "technical", code);
        var testing = Array(root, "testing", code);
        var mappings = Array(root, "mappings", code);
        var all = functional.Concat(technical).Concat(testing).ToArray();
        var ids = all.Select(x => Text(x, "id")).ToArray();
        ReviewPrepareService.Require(functional.Length > 0 && technical.Length > 0 && testing.Length > 0 &&
            ids.All(x => !string.IsNullOrWhiteSpace(x)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
            code, "Active FR/TR/TEST records are incomplete or ambiguous.");
        ReviewPrepareService.Require(functional.All(x => Text(x, "id").StartsWith("FR-", StringComparison.Ordinal)) &&
            technical.All(x => Text(x, "id").StartsWith("TR-", StringComparison.Ordinal)) && testing.All(x => Text(x, "id").StartsWith("TEST-", StringComparison.Ordinal)),
            code, "Requirement kinds do not match their canonical IDs.");
        var technicalIds = technical.Select(x => Text(x, "id")).ToHashSet(StringComparer.Ordinal);
        var testIds = testing.Select(x => Text(x, "id")).ToHashSet(StringComparer.Ordinal);
        var mappedTechnical = new HashSet<string>(StringComparer.Ordinal);
        var mappedTests = new HashSet<string>(StringComparer.Ordinal);
        ReviewPrepareService.Require(mappings.Length == functional.Length, code, "Every FR requires one explicit mapping.");
        foreach (var fr in functional)
        {
            var criteria = Array(fr, "acceptanceCriteria", code);
            var criteriaIds = criteria.Select(x => Text(x, "id")).ToArray();
            ReviewPrepareService.Require(criteria.Length > 0 && criteria.All(x => !string.IsNullOrWhiteSpace(Text(x, "id")) && !string.IsNullOrWhiteSpace(Text(x, "text"))) &&
                criteriaIds.Distinct(StringComparer.Ordinal).Count() == criteria.Length, code, "FR acceptance criteria are missing.");
            var matching = mappings.Where(x => Text(x, "frId") == Text(fr, "id")).ToArray();
            ReviewPrepareService.Require(matching.Length == 1, code, "FR mapping is missing or duplicated.");
            var mapping = matching[0];
            var trs = Strings(mapping, "trIds", code);
            var tests = Strings(mapping, "testIds", code);
            ReviewPrepareService.Require(trs.Length > 0 && tests.Length > 0 && trs.All(technicalIds.Contains) && tests.All(testIds.Contains), code, "Mapping targets are missing.");
            mappedTechnical.UnionWith(trs); mappedTests.UnionWith(tests);
            var coverage = Array(mapping, "criteria", code);
            ReviewPrepareService.Require(coverage.Length == criteria.Length && coverage.Select(x => Text(x, "acId")).ToHashSet(StringComparer.Ordinal).SetEquals(criteriaIds),
                code, "Acceptance-criterion coverage is incomplete.");
            foreach (var ac in coverage)
            {
                var covered = Strings(ac, "testIds", code);
                ReviewPrepareService.Require(covered.Length > 0 && covered.All(x => tests.Contains(x, StringComparer.Ordinal)), code, "AC test coverage references an unmapped TEST.");
            }
        }
        ReviewPrepareService.Require(mappedTechnical.SetEquals(technicalIds) && mappedTests.SetEquals(testIds), code, "Unmapped TR/TEST records remain in scope.");
        // Satisfaction/status may remain pending HV. Completeness means full records, ACs, mappings and coverage.
        return ids.Order(StringComparer.Ordinal).ToArray();
    }
    private static string Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString()! : "";
    private static JsonElement[] Array(JsonElement value, string key, string error)
    {
        ReviewPrepareService.Require(value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Array,
            error, "Required receipt array is missing: " + key);
        return value.GetProperty(key).EnumerateArray().ToArray();
    }
    private static string[] Strings(JsonElement value, string key, string error)
    {
        var values = Array(value, key, error);
        ReviewPrepareService.Require(values.All(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString())), error, "Invalid receipt identifier array: " + key);
        return values.Select(x => x.GetString()!).ToArray();
    }
}
