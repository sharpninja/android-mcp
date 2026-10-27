# APEX source/module metadata v2

Finite pinned Google source cohort:163 manifests and138 nearest build files,
301paths/300distinct Git objects; all downloaded and receipt/hash reconciled.
One transient503 retried successfully at the same pinned URL; no denial bypass.
The v1 strict JSON pass retained14 comment-bearing manifests as unsupported;
v2 handles comments without changing string literals and preserves raw sources.
All163 manifests now parsed;160 apex/apex_defaults declarations and114 explicit
build-to-manifest links retained.12 fixtures pass.

Manifest name and version are source declarations. Many explicit comments call
version0 a build placeholder. No installed-version range or product applicability
is inferred. Soong names, apex_available selectors, manifest name, AIDL frozen
version and Android API version remain separate domains. Defaults/expression
dependencies remain unresolved; a missing manifest property is not auto-linked.

Use raw_properties/raw_source_text and source identities for provenance; normalized
version codes are decimal strings (signed64 max) suitable for language-neutral
Kotlin/SQLite consumption. Unrecognized/out-of-range raw values remain unknown.
Producer metadata cannot grant OS/client access. Integrate into a fresh matched
builder/schema snapshot; running release011 is unchanged. No upload occurred.
