using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Original published-hash inventory caller through existing native artifact services.</summary>
public sealed class PublishedInterfaceHashWorkflow(IArtifactStore inputs, IArtifactStore output, IArtifactTreeReader outputTree,
    IArtifactLeaseProvider leases, ISourcesRuntimeFactory runtimes, IProfiledCanonicalJson canonical, IClock clock)
    : IPublishedInterfaceHashWorkflow
{
    private const string AidlReason = "stable AIDL version hash", HidlReason = "HIDL ancestor hash-index candidate";
    private static readonly Regex FrozenPath = SourceSyntax.Rx(@"(?:^|/)aidl_api/([^/]+)/([^/]+)/\.hash$");
    private static SourceExtractException Failure(string code, string message) => new("INTERFACE_HASH_" + code, message);
    public async Task<PublishedInterfaceHashWorkflowResult> RunAsync(PublishedInterfaceHashWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        _ = ArtifactPaths.Segments(request.GenerationRoot);
        var bytes = await new PinnedArtifactReader(inputs).ReadAsync(request.Inventory, cancellationToken);
        var inventory = SourceFactProducer.StrictObject(JsonNode.Parse(canonical.Serialize(bytes)));
        var sources = SourceFactProducer.CopyRequired(inventory, "sources");
        var facts = new List<JsonObject>(); var reports = new JsonArray(); var counts = new JsonObject();
        var pins = new Dictionary<string, StoredArtifact>(StringComparer.OrdinalIgnoreCase) { [request.Inventory.RelativePath] = request.Inventory };
        ISourcesRuntime? runtime = null;
        try
        {
            foreach (var value in SourceFactProducer.Iterable(sources))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = SourceFactProducer.StrictObject(value); var reasons = SourceFactProducer.CopyRequired(source, "selection_reasons");
                var format = SourceFactProducer.ContainsImport(reasons, AidlReason) ? "aidl" : SourceFactProducer.ContainsImport(reasons, HidlReason) ? "hidl" : null;
                if (format is null) continue;
                var path = SourceFactProducer.CopyRequired(source, "local_path")?.GetValue<string>() ?? throw new ArgumentException("Selected local_path must be text");
                _ = ArtifactPaths.Segments(path);
                var raw = await inputs.ReadAsync(path, cancellationToken); var digest = ContentHash.Sha256(raw.Span);
                var expected = SourceFactProducer.CopyRequired(source, "sha256");
                if (expected is not JsonValue scalar || !scalar.TryGetValue<string>(out var declared) || declared != digest)
                    throw Failure("DIGEST", "Selected source bytes differ from their declared SHA256");
                var text = SourceText.DecodeUtf8(raw.Span);
                runtime ??= runtimes.Create(request.Profile);
                var parsed = format == "aidl" ? runtime.Concerns.PublishedHashes.AidlHashes(text) : runtime.Concerns.PublishedHashes.HidlHashes(text);
                var identity = new JsonObject();
                foreach (var key in new[] { "repository", "revision", "path", "sha256", "retrieved_at" }) identity[key] = SourceFactProducer.CopyRequired(source, key);
                JsonObject scope;
                if (format == "aidl")
                {
                    var logical = identity["path"]?.GetValue<string>() ?? throw new ArgumentException("Selected AIDL path must be text");
                    var match = FrozenPath.Match(logical);
                    scope = match.Success ? new() { ["interface_module_name_from_path"] = match.Groups[1].Value, ["frozen_dump_version_from_path"] = match.Groups[2].Value } : new() { ["path_scope"] = "unknown" };
                }
                else scope = new() { ["path_scope"] = "HIDL ancestor hash index; individual identifiers retain package versions" };
                var parsedFacts = parsed["facts"]!.AsArray(); var unknown = parsed["unknown_lines"]!.AsArray();
                foreach (var item in parsedFacts)
                {
                    var fact = (JsonObject)item!.DeepClone(); fact["source"] = identity.DeepClone(); fact["scope"] = scope.DeepClone(); facts.Add(fact);
                    Increment(format + ":hash-facts", 1);
                }
                reports.Add(new JsonObject { ["source"] = identity, ["scope"] = scope, ["format"] = format, ["facts"] = parsedFacts.Count,
                    ["unknown_lines"] = unknown.DeepClone(), ["compiled_runtime_hash_proven"] = false });
                Increment(format + ":files", 1); Increment(format + ":unknown-lines", unknown.Count);
                var pin = new StoredArtifact(path, digest, raw.Length, "application/octet-stream");
                if (pins.TryGetValue(path, out var prior))
                {
                    if (prior.RelativePath != path || prior.Sha256 != pin.Sha256 || prior.Length != pin.Length)
                        throw Failure("SOURCE_ALIAS", "Repeated source path has conflicting portable identity");
                }
                else pins.Add(path, pin);
            }
        }
        finally { runtime?.Dispose(); }
        if (!request.Producer.ProducerFiles.TryGetValue("native/assembly", out var assembly)) throw new ArgumentException("Observed native assembly identity is required");
        var factsPath = request.GenerationRoot + "/facts.jsonl"; var reportPath = request.GenerationRoot + "/report.json";
        JsonObject? report = null;
        var manifests = new ManifestFactory(request.GenerationRoot + "/index.json", request.Profile.Id, request.Producer, canonical);
        var engine = new ArtifactGenerationRuntime(inputs, output, leases, new ArtifactReceiptVerifier(new NativeContentHash()), manifests, "interface-hashes/" + request.GenerationRoot);
        var recipe = new ArtifactGenerationRecipe(pins.Values.Select(pin => new ArtifactSourceSpec(pin.RelativePath, pin.MediaType, pin)).ToArray(), _ =>
            new([factsPath, reportPath], async token =>
            {
                var slash = request.GenerationRoot.LastIndexOf('/'); var parent = slash < 0 ? null : request.GenerationRoot[..slash];
                var entries = await outputTree.EnumerateAsync(parent, false, token);
                if (entries.Any(entry => string.Equals(entry.RelativePath, request.GenerationRoot, StringComparison.OrdinalIgnoreCase)))
                    throw Failure("OUTPUT_EXISTS", "Preserve existing immutable evidence output directory");
                report = new() { ["at"] = clock.Now.ToString("O"), ["counts"] = counts.DeepClone(), ["files"] = reports.DeepClone(), ["code_sha256"] = assembly,
                    ["native_producer"] = JsonSerializer.SerializeToNode(request.Producer), ["historical_python_identity"] = false,
                    ["runtime_observations"] = false, ["metadata_cannot_grant_os_or_client_authority"] = true, ["complete"] = false };
                using var lines = new MemoryStream();
                foreach (var fact in facts) { await lines.WriteAsync(Json(fact), token); lines.WriteByte(10); }
                return new Artifact[] { new(factsPath, lines.ToArray(), "application/x-ndjson"), new(reportPath, Json(report), "application/json") };
            }));
        var generation = await engine.ExecuteAsync(request.GenerationRoot, recipe, cancellationToken);
        var factsPin = generation.Outputs.Single(pin => pin.RelativePath == factsPath); var reportPin = generation.Outputs.Single(pin => pin.RelativePath == reportPath);
        var summary = (JsonObject)report!.DeepClone(); summary.Remove("files");
        summary["native_generation"] = JsonSerializer.SerializeToNode(new { Facts = factsPin, Report = reportPin, Index = generation.Manifest });
        return new(report!, summary, factsPin, reportPin, generation.Manifest);

        void Increment(string key, int amount) => counts[key] = (counts[key]?.GetValue<int>() ?? 0) + amount;
        byte[] Json(JsonNode value) => canonical.Serialize(JsonSerializer.SerializeToElement(value), JsonSerializationProfile.LegacyAsciiIdentity);
    }
    private sealed class ManifestFactory(string path, string profile, SourceExtractProducerIdentity producer, ICanonicalJson canonical) : IArtifactGenerationManifestFactory
    {
        public string RelativePath => path;
        public Artifact Create(string generationId, IReadOnlyList<StoredArtifact> inputs, IReadOnlyList<StoredArtifact> outputs) =>
            new(path, canonical.Serialize(JsonSerializer.SerializeToElement(new { Schema = "androidmcp-native-interface-hash-generation-v1", GenerationId = generationId, ProfileId = profile, Producer = producer, Inputs = inputs, Outputs = outputs })), "application/json");
    }
}
