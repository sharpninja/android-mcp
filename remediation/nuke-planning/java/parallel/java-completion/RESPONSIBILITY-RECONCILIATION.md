# Java original producer and helper responsibilities

This is a contribution to the Source-owned inventory. It does not change the sole inventory, retirement status, historical producer identity, or HV acceptance.

## Exact original function disposition

Both published `java_source.py` copies have one public function, `parse`, and nested helpers `txt`, `mask_comments`, `qualify_anns`, `modifiers`, `type_text`, `parameter`, `row`, `excluded_node`, `type_decl`, its nested `child`, and `errors`. The single replacement remains `JavaSourceExtractor` and its visitor over `JavaNativeParser`. No second parser, callback classifier, taxonomy, or cache implementation is introduced.

- `txt`: `SyntaxTree.Text` and `Slice`; existing `JAVA-06/12` preserve line and UTF-8 locators. The new field cases additionally compare exact byte ranges around supplementary Unicode.
- `mask_comments`, `qualify_anns`, `modifiers`: the visitor delegates scanning and masking to the one shared `SourceSyntax`. Existing `JAVA-01/07/08/09` retain original comments, annotations, explicit imports, java.lang qualification, wildcard uncertainty, and source argument spelling. `JAVA-BOUNDARY-provenance` directly compares the complete annotation document, source raw text, original versus rewritten arguments, offsets, name-resolution clause, static import retention, and exact import/wildcard sets. This does not claim all malformed Unicode/comment combinations.
- `type_text`: existing `JavaProducerTypeTextCases` directly qualifies a java.lang subpackage import while preserving the original exact `full == "java.lang." + simple` exemption. No symbol inference is added.
- `parameter`: existing `JAVA-01/11` cover ordinary/varargs/receiver parameters; the declaration cases retain record component annotations and compact-constructor header parameters.
- `row`: one existing row constructor retains owner, binary spelling, source locator, visibility, modifiers and annotation documents. `JavaProducerDeclarationCases` preserves nested interface/annotation visibility and containing owner kinds.
- `excluded_node`: `JAVA-BOUNDARY-exclusions` checks the exact static-initializer/block counts and proves method-local and anonymous declarations do not become surface rows or invented exclusion entries.
- `type_decl` and `child`: existing AST/declaration cases retain type/member ordering, record components and compact constructors, enum constants, field dimensions and method metadata. The new depth pair accepts depth 128 and rejects depth 129 with the exact reason, range, retained ancestor rows, no nested leaf, missing `raw_excerpt`, empty unsupported raw text and SHA256 of empty text. The corrected visitor changes only this diagnostic-excerpt choice.
- Field branch of `child`: the new constant pair tests exactly 4096 and 4097 Unicode scalar values including quotes; the signature pair tests exactly 16384 and 16385 scalar values including the declaration. Expectations preserve `constant_raw` nulling, full initializer byte range, exact excerpt spelling, and absence of the excerpt flag at the admitted boundary.
- `errors`: existing `JAVA-04/13` retain parser ERROR/missing-token issues. Depth-limit handling now preserves its original distinct issue shape instead of acquiring an AST-error excerpt.

The concrete new normal-proof contribution is the depth diagnostic and the complete constant/signature-limit, exclusion-count and annotation-provenance clauses named above. The whole `parse` function, every corpus input, and historical profile equivalence are not promoted to complete by these eight cases.

## Context helper and its callers

Both published `java_context.py` copies contain only `attach(sources,reports)`. `JavaFileContext.Attach` remains the sole managed replacement: map reports by cache path, skip non-Java rows, require the cache/parser hash match, copy the six context fields and the exact static-import uncertainty statement. Existing `CTX-imports/cache/parser/missing/nonjava` and `ORIG-CONTEXT-IMPORTS-EXACT` retain the two original test methods and exact import cardinality. The managed implementation additionally rejects empty hashes and clones report nodes.

The original tooling has no runtime call to `attach`; tests call it directly. `build_catalog.processing_fingerprint` hashes its code, and the per-source fingerprint includes an already present `java_file_context`. Therefore a new attachment pipeline is not invented. Native extraction supplies current metadata, while Catalog verifies historical cache bytes and retains historical context as unused audit data. That profile transition is owned by Source/Catalog.

## JVM helper and actual callers

Both published `jvm_source_link.py` copies contain only `PRIMITIVES`, `encode`, and `key`. `JvmSourceLinker` is the one managed replacement. Existing 39 contract vectors plus six falsey-owner vectors cover the complete supported typed helper body: primitive table, null/empty, trimming, arrays/varargs, void-array/argument rejection, balanced erasure, known references/nested binary spelling, falsey mapped owners, declared-owner proof, type kinds, field/unsupported rejection, methods and constructors.

The original `build_catalog.add` calls `key` with the selected platform owner map, then requires membership in that platform's SDK key map. Existing `SOURCE-IMPORT-02` and the registered selected-Java Catalog fixture prove this caller on native source extraction. `annotation_link.xml_jvm_key` calls `encode`; Source owns that XML composition and Catalog owns annotation SQL observations. Neither caller is duplicated here.

## Cache helper and truthful identity handoff

`cache_fingerprint.current(kind)` hashes Python version, installed package/Expat versions, and original producer files. Replacing that with a string that impersonates the old Python producer would be false. The current producer identity is the existing `SourceProducerIdentityBuilder`: evaluated C# assembly/MVID/runtime facts plus the selected Java loader's exact native manifest/artifact identity. `JavaSourceRuntimeLoader` remains the sole native manifest/ABI/path/hash admission authority.

The generic `SourceCache`, `HistoricalSourceCache`, `SourcesExtractWriter`, runtime factory and Catalog typed intake remain Source/Catalog responsibilities. Their live integration fixtures and historical profile dispositions are not counted as Java helper-body proof. All originals and earlier failed receipts remain retained.

## Original method accounting

The retained `published-java-sourcefact-method-closure-007.json` has 33 distinct original methods: 14 Java/context/JVM methods and 19 generic callback/functional methods. The Java 14 occur in both canonical and release011 copies, producing 28 published method instances. This increment adds eight producer branch vectors, not eight original methods. It changes none of the existing original inputs/assertions and makes no new whole-file or retirement count claim.

## Chronology and limits

The original eight-vector fixture passed 16 mock tests before the depth correction. The first native run passed seven and failed the signature-16385 vector because this agent's Unicode generator created too few scalar values. That fixture and its TRX are retained. The correction preserves the expected thresholds and excerpt semantics, fixes the independent input generator and asserts each actual input length. The checkpoint extractor is restored before fresh corrected mock proof; the same depth-only implementation is reapplied only afterward.

Stage receipts and the final immutable handoff identify the exact files, SHA256/length, original method/caller dispositions, failed and passing TRX, unchanged corrected test hashes and selected native002 lock. Builds retain NuGet audit settings and report NU1900 when the feed is unavailable. MCP logging/memory loading remain incomplete after the trusted wrapper's health check denial; no denied call is retried. HV remains deferred until Ready.
