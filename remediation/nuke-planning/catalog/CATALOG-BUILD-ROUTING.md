# Catalog build source routing and output boundary

Read directly from tooling/build_catalog.py, _build/add and the deferred annotation pass. Sources owns one dispatcher and its exact kind/scope alias policy. Catalog consumes that dispatcher and must not reimplement parsing or guess a source kind from a file extension.

- sdk-signature/source-signature: signature records become java declaration identities.
- java-source: native cached declaration records become java-source identities; JVM equivalence requires the shared proven source/JVM linker and exact snapshot evidence.
- sdk-api-index: retain all versioned lifecycle records as jvm identities. Current-target lookup maps are separate from complete history inventory.
- aidl: retain all roots and enriched members, full physical locators, declaration hierarchy/status, lexical reconciliation and unsupported source evidence. No transaction codes or codec proof are inferred.
- manifest: permission definition/raw XML concern data stays separate from ordinary binding declarations. Preserve literal protection-level attributes and child evidence without interpreting runtime grants.
- sdk-annotations: retain the complete ZIP/XML evidence, then perform exact target linking in a deferred pass. Unmatched/ambiguous targets remain searchable evidence. No current-source or cross-snapshot type match is guessed.
- evidence/official-sdk-permission-reference: documented permission symbols are concern metadata, never manifest definitions or authority.
- evidence/aosp-pinned-hidl-source-evidence: HIDL structural declarations retain unknown Android applicability, compiler/wire/runtime status and lossless unsupported source.
- sdk-history and remaining evidence: retain evidence without current declaration inference. Frozen native/interface candidate scopes retain raw UTF8 or base64 unsupported data; availability of a richer concern extractor does not silently change a frozen profile's classification.
- Unknown kinds produce explicit unsupported issues.

The shared extracted declaration row shape is kind, canonical, owner, package, name, raw, line, return_type, parameters, annotations, all_annotations and extras. Concern-only payloads stay in Metadata. Catalog adds the language namespace, canonical SHA256 identity, schema version, permission AST from the Source-owned permission producer, source/provenance rows and explicit unknown runtime/codec/client-grant state. A new C# producer/source fingerprint remains distinct from historical Python processing identity.

The end-to-end native build pipeline must validate catalog/source metadata and source bytes first; populate one private staging catalog; normalize and link source occurrences; project snapshot-scoped types/lifecycle/contracts; preserve immutable declarations and metadata attachment; export full inventory and effective entries; independently reconcile hashes/counts/source references; seal the registry and produce offline default-deny aliases. Export and publication must preserve prior originals. No device operation, deployment, installation, credential change or runtime admission is part of this pipeline.

Current dependency order: the real resolver and atomic storage are proved; shared contract-format3 and lifecycle mocks precede their native implementations; SourceTypeFacts supplies formal/header parsing once; scoped projection, effective inventory, dispatch/alias composition and the Catalog/Build orchestration follow. Existing native test counts do not establish completion of this whole pipeline or frozen-release profile parity.
