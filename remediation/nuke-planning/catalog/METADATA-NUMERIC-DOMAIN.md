# Existing metadata numeric domain

The original `source_metadata_reference.py` validates `estimated_tokens` against
the canonical `FunctionalGroup` schema and then checks whether that numeric value
is nonzero. The schema specifies an integer with minimum zero and no upper bound.
The migrated `CatalogSourceMetadata.ValidateGroup` currently calls
`GetValue<int>()`. That narrower representation is not a complete replacement of
the admitted original domain.

The staged fourteen-case increment reuses `CatalogMetadataBodyCase`, its existing
assertions, and its independent scripted provider. It covers integer boundaries,
integer-valued fraction/exponent spellings, zero spellings, estimator requirements,
and unchanged negative/fractional schema rejection. Correct responses and all seven
existing independent wrong-field controls must pass before changing production.

The native binding will use the same cases and `NativeCatalogMetadataBodyProvider`.
Any correction must reuse the existing numeric/schema authority. It must not add a
second JSON parser, silently clamp estimates, weaken schema limits or reinterpret
source metadata as runtime authority.

Dependency-revision representation and SQLite revision-width obligations remain
separate open items. This increment does not establish complete producer parity.
No Python invocation or baseline generation is needed.

The mock gate passed 104 cases, including all 28 numeric mock/control tests.
Afterward, one `GetValue<int>()` zero check was replaced with
`JsonNode.DeepEquals(value, JsonValue.Create(0))`. The same fourteen cases bound to
the native validator passed in the 280-case affected gate, with zero failures,
skips or warnings. See `evidence/metadata-numeric-native-proof.json` for exact
source pins, TRX identities, and limitations. The full suite after this later
increment remains separate from immutable checkpoint 009.

This uses .NET's existing numeric equality, rather than another numeric parser:
[JsonElement.DeepEquals](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.deepequals?view=net-10.0)
defines numeric equivalence without rounding; the pinned
[.NET 10 node implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Text.Json/src/System/Text/Json/Nodes/JsonValueOfElement.cs)
delegates element-backed comparisons to that operation. The admitted vectors do
not establish every arbitrary-precision case in the pinned schema library.
