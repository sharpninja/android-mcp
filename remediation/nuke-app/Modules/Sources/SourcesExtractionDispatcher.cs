using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The sole declared kind/scope routing boundary for source extraction.</summary>
public sealed class SourcesExtractionDispatcher : ISourcesExtractionDispatcher
{
    private sealed record Alias(string Kind, string? Scope, string Extractor);
    // Shared rules are data, not copied candidate/frozen implementations.
    // Native Binder scopes deliberately have no richer-parser alias.
    private static readonly IReadOnlyList<Alias> LegacyAliases =
    [
        new("sdk-signature", null, "signature"), new("source-signature", null, "signature"),
        new("evidence", "aosp-pinned-hidl-source-evidence", "hidl-source"),
        new("evidence", "official-sdk-permission-reference", "permission-reference")
    ];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Alias>> Profiles = new Dictionary<string, IReadOnlyList<Alias>>(StringComparer.Ordinal)
    { [SourceExtractionProfile.RELEASE011.Id] = LegacyAliases, [SourceExtractionProfile.CurrentCandidate.Id] = LegacyAliases };
    private readonly IReadOnlyDictionary<string, ISourceExtractor> extractors;
    private readonly IReadOnlyList<Alias> aliases;
    private readonly SourceExtractionProfile profile;
    public IReadOnlyList<string> SupportedKinds { get; }

    public SourcesExtractionDispatcher(IEnumerable<ISourceExtractor> extractors, SourceExtractionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(extractors);
        this.profile = profile;
        if (!Profiles.TryGetValue(profile.Id, out var selected)) throw new ArgumentException("Unknown source extraction profile", nameof(profile));
        aliases = selected;
        var registrations = new Dictionary<string, ISourceExtractor>(StringComparer.Ordinal);
        foreach (var extractor in extractors)
        {
            if (extractor is null || string.IsNullOrWhiteSpace(extractor.Kind)) throw new ArgumentException("Registered source extractor kind is required", nameof(extractors));
            if (!registrations.TryAdd(extractor.Kind, extractor)) throw new ArgumentException("Duplicate source extractor kind: " + extractor.Kind, nameof(extractors));
        }
        this.extractors = registrations;
        SupportedKinds = Array.AsReadOnly(registrations.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    public ExtractionResult Extract(SourceDocument source, string? kind, string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(source.Context);
        var identity = new JsonObject
        {
            ["requested_kind"] = kind, ["requested_scope"] = scope, ["relative_path"] = source.RelativePath, ["sha256"] = ContentHash.Sha256(source.Bytes.Span),
            ["repository"] = source.Context.Repository, ["revision"] = source.Context.Revision, ["snapshot_id"] = source.Context.SnapshotId,
            ["platform_version"] = source.Context.PlatformVersion, ["profile_id"] = profile.Id
        };
        var alias = aliases.FirstOrDefault(x => x.Kind == kind && x.Scope == (kind == "evidence" ? scope : null));
        var retainedEvidence = alias is null && kind is "evidence" or "sdk-history";
        var canonicalKind = alias?.Extractor ?? kind;
        var extractor = !retainedEvidence && canonicalKind is not null && extractors.TryGetValue(canonicalKind, out var selected) ? selected : null;
        string? text = null;
        if (extractor is not IBinarySourceExtractor)
        {
            try { text = SourceText.DecodeUtf8(source.Bytes.Span); }
            catch (DecoderFallbackException error) { return Unsupported(source, identity, null, "source-encoding-unsupported", error.Message); }
        }
        if (retainedEvidence)
            return Unsupported(source, identity, text, "source-format-unknown", "Source evidence retained without declaration, runtime, version or wire inference");
        if (extractor is null)
            return Unsupported(source, identity, text, "source-kind-unsupported", "No registered extractor for the declared source kind and scope");
        ExtractionResult parsed;
        try { parsed = extractor.Extract(source with { Bytes = source.Bytes.ToArray() }); }
        catch (Exception error) when (error is FormatException or ArgumentException or JsonException or XmlException or InvalidDataException)
        {
            // Infrastructure, cancellation, OOM and native-library faults escape.
            return Unsupported(source, identity, text, "source-extraction-failed", error.Message);
        }
        var originalMetadata = parsed.Metadata is null ? new JsonObject() : (JsonObject)parsed.Metadata.DeepClone();
        var metadata = originalMetadata.ContainsKey("source_identity") ? new JsonObject { ["format_metadata"] = originalMetadata } : originalMetadata;
        metadata["source_identity"] = identity;
        return parsed with { Records = parsed.Records.Select(x => (JsonObject)x.DeepClone()).ToArray(), Issues = parsed.Issues.ToArray(), Metadata = metadata };
    }

    private static ExtractionResult Unsupported(SourceDocument source, JsonObject identity, string? text, string code, string reason) => new(
        [], [new SourceIssue(code, string.IsNullOrWhiteSpace(reason) ? "Source extraction unsupported" : reason, null, null)], StructuralParseStatus.Unsupported,
        new JsonObject
        {
            ["raw_source_encoding"] = text is null ? "base64" : "utf-8", ["raw_source"] = text ?? Convert.ToBase64String(source.Bytes.Span),
            ["normalizer_complete"] = false, ["compiler_validation"] = false, ["source_identity"] = identity
        });
}
