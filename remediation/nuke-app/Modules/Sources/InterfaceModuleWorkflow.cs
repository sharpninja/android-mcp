using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The original interface-module inventory caller, through shared artifact and source services.</summary>
public sealed class InterfaceModuleWorkflow(IArtifactStore inputs, IArtifactStore output,
    ICreateNewArtifactDirectory directories, IArtifactLeaseProvider leases, ISourcesRuntimeFactory runtimes,
    IProfiledCanonicalJson canonical, ILegacyJsonValueEquality equality, IClock clock) : IInterfaceModuleWorkflow
{
    private static readonly string[] LiteralNames = ["name", "srcs", "exclude_srcs", "versions", "versions_with_info", "imports", "frozen", "unstable", "stability", "apex_available", "vendor_available", "owner"];
    private sealed record DeclaredGroup(JsonElement Repository, JsonElement Revision, List<JsonNode?> Paths);
    public async Task<InterfaceModuleWorkflowResult> RunAsync(InterfaceModuleWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        _ = ArtifactPaths.Segments(request.GenerationRoot);
        var reader = new PinnedArtifactReader(inputs);
        var inventoryBytes = await reader.ReadAsync(request.Inventory, cancellationToken);
        var inventory = JsonNode.Parse(canonical.Serialize(inventoryBytes));
        var declaredBytes = await reader.ReadAsync(request.DeclaredSources, cancellationToken);
        var declared = SourceFactProducer.StrictObject(JsonNode.Parse(canonical.Serialize(declaredBytes)));
        var groups = new List<DeclaredGroup>();
        foreach (var value in SourceFactProducer.Iterable(SourceFactProducer.CopyRequired(declared, "sources")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = SourceFactProducer.StrictObject(value);
            var kind = SourceFactProducer.CopyRequired(source, "kind");
            var aidl = kind is JsonValue scalar && scalar.TryGetValue<string>(out var kindText) && kindText == "aidl";
            if (!aidl && !Text(SourceFactProducer.CopyRequired(source, "path")).EndsWith(".hal", StringComparison.Ordinal)) continue;
            var repository = Key(SourceFactProducer.CopyRequired(source, "repository")); var revision = Key(SourceFactProducer.CopyRequired(source, "revision"));
            var group = groups.FirstOrDefault(group => equality.Equal(repository, group.Repository) && equality.Equal(revision, group.Revision));
            if (group is null) { group = new(repository, revision, []); groups.Add(group); }
            group.Paths.Add(SourceFactProducer.CopyRequired(source, "path"));
        }
        var rawPins = new List<StoredArtifact>();
        var physical = new Dictionary<string, StoredArtifact>(StringComparer.OrdinalIgnoreCase);
        AddPin(request.Inventory); AddPin(request.DeclaredSources);
        var factsPath = request.GenerationRoot + "/facts.jsonl"; var reportPath = request.GenerationRoot + "/report.json";
        JsonObject? report = null;
        var manifests = new ManifestFactory(request.GenerationRoot + "/index.json", request.Profile.Id, request.Producer, rawPins, canonical);
        var engine = new ArtifactGenerationRuntime(inputs, output, leases, new ArtifactReceiptVerifier(new NativeContentHash()), manifests, "interface-modules/" + request.GenerationRoot);
        var recipe = new ArtifactGenerationRecipe([new(request.Inventory.RelativePath, request.Inventory.MediaType, request.Inventory), new(request.DeclaredSources.RelativePath, request.DeclaredSources.MediaType, request.DeclaredSources)], admittedInputs =>
            new([factsPath, reportPath], async token =>
            {
                // This effect intentionally precedes sources/property/file
                // admission, and survives every later error or failed retry.
                await directories.CreateNewDirectoryAsync(request.GenerationRoot, token);
                var rows = SourceFactProducer.CopyRequired(SourceFactProducer.StrictObject(inventory), "sources");
                var facts = new List<JsonObject>(); var counts = new JsonObject(); ISourcesRuntime? runtime = null;
                try
                {
                    foreach (var value in SourceFactProducer.Iterable(rows))
                    {
                        token.ThrowIfCancellationRequested(); var source = SourceFactProducer.StrictObject(value);
                        if (!SourceFactProducer.ContainsImport(SourceFactProducer.CopyRequired(source, "selection_reasons"), "nearest selected-interface build ancestor")) continue;
                        var logicalPath = Text(SourceFactProducer.CopyRequired(source, "path"));
                        if (!logicalPath.EndsWith(".bp", StringComparison.Ordinal)) { Increment("non-soong-build-files-retained", 1); continue; }
                        var local = Text(SourceFactProducer.CopyRequired(source, "local_path")); _ = ArtifactPaths.Segments(local);
                        var raw = await inputs.ReadAsync(local, token); var digest = ContentHash.Sha256(raw.Span);
                        var expected = SourceFactProducer.CopyRequired(source, "sha256");
                        if (expected is not JsonValue declaredDigest || !declaredDigest.TryGetValue<string>(out var hash) || hash != digest)
                            throw new SourceExtractException("INTERFACE_MODULE_DIGEST", "Selected source bytes differ from their declared SHA256");
                        var text = SourceText.DecodeUtf8(raw.Span); Increment("soong-files-parsed", 1);
                        var pin = new StoredArtifact(local, digest, raw.Length, "application/octet-stream"); if (AddPin(pin)) rawPins.Add(pin);
                        runtime ??= runtimes.Create(request.Profile); var soong = runtime.Concerns.Soong;
                        foreach (var parsed in soong.Modules(text, SoongModuleFamily.Interfaces))
                        {
                            var module = (JsonObject)parsed!.DeepClone(); var identity = new JsonObject();
                            foreach (var key in new[] { "repository", "revision", "path", "sha256" }) identity[key] = SourceFactProducer.CopyRequired(source, key);
                            module["source"] = identity; var properties = module["properties"]!.AsObject(); var literals = new JsonObject();
                            foreach (var name in LiteralNames) literals[name] = soong.Literal(properties, name);
                            module["literal_properties"] = literals;
                            var patterns = soong.Literal(properties, "srcs"); var excluded = soong.Literal(properties, "exclude_srcs");
                            module["source_linkage_status"] = "unresolved source expression/filegroup/defaults";
                            var absentExclude = excluded is null && !properties["items"]!.AsArray().Any(item => item!["name"]!.GetValue<string>() == "exclude_srcs");
                            if (Strings(patterns) is { } selectors && (absentExclude || Strings(excluded) is not null))
                            {
                                var exclusions = absentExclude ? Array.Empty<string>() : Strings(excluded)!;
                                var repository = Key(SourceFactProducer.CopyRequired(source, "repository")); var revision = Key(SourceFactProducer.CopyRequired(source, "revision"));
                                var declaredPaths = groups.FirstOrDefault(group => equality.Equal(repository, group.Repository) && equality.Equal(revision, group.Revision))?.Paths ?? [];
                                try
                                {
                                    var matched = soong.MatchSources(logicalPath, selectors, exclusions, new DeclaredPaths(declaredPaths));
                                    module["matched_declared_paths"] = SourceSyntax.Strings(matched); module["source_linkage_status"] = "literal-source-selectors-matched";
                                }
                                catch (ArgumentException error) { module["source_linkage_reason"] = error.Message; }
                            }
                            Increment(module["kind"]!.GetValue<string>(), 1); Increment("matched-path-links", module["matched_declared_paths"]?.AsArray().Count ?? 0);
                            Increment("modules-with-unresolved-selection", module["source_linkage_status"]!.GetValue<string>() != "literal-source-selectors-matched" ? 1 : 0); facts.Add(module);
                        }
                    }
                }
                finally { runtime?.Dispose(); }
                if (!request.Producer.ProducerFiles.TryGetValue("native/assembly", out var assembly)) throw new ArgumentException("Observed native assembly identity is required");
                report = new()
                {
                    ["at"] = clock.Now.ToString("O"), ["counts"] = counts, ["modules"] = facts.Count, ["code_sha256"] = assembly,
                    ["native_producer"] = JsonSerializer.SerializeToNode(request.Producer), ["historical_python_identity"] = false,
                    ["build_files_executed"] = false, ["runtime_module_or_os_access_inferred"] = false, ["complete"] = false,
                    ["limitations"] = new JsonArray("Literal properties are source facts, not product selection or observed installed module/version", "Expressions, filegroups, defaults, imports and exclusions lacking literal proof remain unresolved", "Declared build module versions, AIDL frozen dump versions and Android/API/APEX versions remain separate")
                };
                using var lines = new MemoryStream(); foreach (var fact in facts) { await lines.WriteAsync(Json(fact), token); lines.WriteByte(10); }
                return new Artifact[] { new(factsPath, lines.ToArray(), "application/x-ndjson"), new(reportPath, Json(report), "application/json") };
                void Increment(string key, int amount) => counts[key] = (counts[key]?.GetValue<int>() ?? 0) + amount;
            }, async token => { foreach (var pin in rawPins) await reader.ReadAsync(pin, token); }));
        var generation = await engine.ExecuteAsync(request.GenerationRoot, recipe, cancellationToken);
        return new(report!, generation.Outputs.Single(pin => pin.RelativePath == factsPath), generation.Outputs.Single(pin => pin.RelativePath == reportPath), generation.Manifest);

        byte[] Json(JsonNode value) => canonical.Serialize(JsonSerializer.SerializeToElement(value), JsonSerializationProfile.LegacyAsciiIdentity);
        bool AddPin(StoredArtifact pin)
        {
            if (!physical.TryGetValue(pin.RelativePath, out var previous)) { physical.Add(pin.RelativePath, pin); return true; }
            if (previous.RelativePath != pin.RelativePath || previous.Sha256 != pin.Sha256 || previous.Length != pin.Length)
                throw new SourceExtractException("INTERFACE_MODULE_SOURCE_ALIAS", "Repeated source path has conflicting portable identity");
            return false;
        }
    }
    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : throw new ArgumentException("Reached source path must be text");
    private static JsonElement Key(JsonNode? value)
    {
        if (value is JsonObject or JsonArray) throw new ArgumentException("Reached repository/revision key must be hashable JSON scalar");
        return JsonSerializer.SerializeToElement(value);
    }
    private static string[]? Strings(JsonNode? value) => value is JsonArray list && list.All(item => item is JsonValue scalar && scalar.TryGetValue<string>(out _)) ? list.Select(item => item!.GetValue<string>()).ToArray() : null;
    // Values are converted only when the existing matcher reaches its path
    // iteration, after its selector refusal. This is a lazy typed projection,
    // not another path/JSON admission engine.
    private sealed class DeclaredPaths(IReadOnlyList<JsonNode?> rows) : IReadOnlyList<string>
    {
        public int Count => rows.Count;
        public string this[int index] => rows[index] is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : throw new InvalidOperationException("Reached declared path must be text");
        public IEnumerator<string> GetEnumerator() { for (var index = 0; index < Count; index++) yield return this[index]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class ManifestFactory(string path, string profile, SourceExtractProducerIdentity producer, IReadOnlyList<StoredArtifact> rawPins, ICanonicalJson canonical) : IArtifactGenerationManifestFactory
    {
        public string RelativePath => path;
        public Artifact Create(string generationId, IReadOnlyList<StoredArtifact> inputs, IReadOnlyList<StoredArtifact> outputs) =>
            new(path, canonical.Serialize(JsonSerializer.SerializeToElement(new { Schema = "androidmcp-native-interface-module-generation-v1", GenerationId = generationId, ProfileId = profile, Producer = producer, Inputs = inputs.Concat(rawPins).ToArray(), Outputs = outputs })), "application/json");
    }
}
