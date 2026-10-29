# Exact additive sole-helper contract requirements

Proposal only. PlanCore remains the sole external Publication helper's responsibility. Preserve Plan/PlanSeed serialization and SourcePin/SourceArchivePin identity. Do not reparse OriginalRow.

Append optional `RehydrationLegacyPlatformFacts? LegacyPlatformFacts = null` to RehydrationCanonicalMetadataReference and omit it from serialization when null. Suggested detached facts:

```csharp
public sealed record RehydrationLegacyPlatformValue(
    bool Present, JsonValueKind Kind, JsonElement? Value);
public sealed record RehydrationLegacyPlatformFacts(
    RehydrationLegacyPlatformValue Row,
    RehydrationLegacyPlatformValue Transport);
```

Value is a detached JsonElement.Clone, null only for an absent/undefined fact; explicit JSON null retains Present=true/Kind=Null and its cloned null element. Capture both exact row.platform_api_version and transport.platform_api_version inside the existing walk. This is necessary because native transport.platform_version can short-circuit the prior row string admission, allowing false/truthy nonstring rows into captureSnapshot. Never substitute that native spelling as legacy input.

Populate the optional facts only when the seven-field string projection cannot represent the selected legacy behavior: an admitted nonstring row, or an absent/null/empty row with a present nonstring transport platform. A nonempty row string dominates any transport value and needs no extra fact. Keep all existing seven field values unchanged; old string-domain cases/wires and old SourcePin archive platform remain unchanged. Do not broaden Plan/PlanSeed validation or change an earlier OptionalText refusal to manufacture new inputs.

Native helper tests should retain the prior24 metadata and17 Snapshot cases and assert Plan/PlanSeed bytes/SourcePin identity, one parse, detached/read-only facts, missing-vs-null, exact kind/value, row truthy precedence, false-row fallback and no fact/wire field for already represented strings. Wrong presence/kind/value/source-row/parse-count mutations must reject before native activation.

Minimal shared conversion needed for the currently staged callers: Python JSON truth (null/false/numeric zero/empty string/empty container false), unchanged strings, True/False spellings, and finite integer/float text. Root's canonical Core/CanonicalJson.Number already owns arbitrary-size integer and finite floating rendering including signed zero/exponents. Reuse that numeric authority through an agreed narrow shared seam; do not clone its algorithm or treat generic JSON serialization as Python str. Existing LegacyJsonValueEquality can supply exact numeric-zero comparison. No suitable public scalar-text seam was found in the current private copy; Core remains owner-controlled.

Truthy array/object str requires recursive Python repr and is not the same as raw JSON. Do not invent a parallel formatter to declare that domain closed. Preserve it as an explicit remaining domain unless Root assigns one shared conversion authority and tests it. Whitespace platform, exact package, XML safety and positive size pins remain enforced by the unchanged SdkArchiveVersionValidator after conversion. Default dry-run/exact reuse/Generic still do not read proof facts or sidecars.
