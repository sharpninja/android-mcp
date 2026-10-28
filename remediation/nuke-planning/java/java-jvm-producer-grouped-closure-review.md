# Java/JVM producer grouped closure review

Local inspection: 2026-10-28T12:42. This contributes exact responsibilities to the Source-owned published198 ledger; it does not replace that ledger, change retirement states or claim HV. No Python, .NET, compiler or browser was executed for this review. The source inventory was read at SHA `1029f8a5325755423cb2ccaacbd8ddde8f9f33089fd6ee488f20110692ae1339` (published198, zero unmapped owners,34 normal verified files,200 qualified method scopes at that read).

## Pure JVM producer: both published copies

Paths: `remediation/catalog/tooling/jvm_source_link.py` and `rehydration/producers/release-011/tooling/jvm_source_link.py`; both independently inspected, SHA `6cc57fd9f4dcf7b8f432377ad486242583c02cb280d7ea7e383b7c41afb4f5cb`.

Both contain only `PRIMITIVES`, `encode`, and `key`, with no file, package discovery, cache, command-line entry point or external runtime dependency. The same `JvmSourceLinker` implements these bodies. Exact clause allocation:

- Primitive descriptor table, null/empty input, trimming, repeated array suffixes and varargs: `ENC-byte` through `ENC-void`, `ENC-null`, `ENC-empty`, `ENC-array`, `ENC-varargs`.
- Reject void array, unresolved variable/alias/wildcard/partial name and unknown generic outer: `ENC-voidarray`, `ENC-T`, `ENC-String`, `ENC-wildcard`, `ENC-partial`, `ENC-unknownouter`.
- Strip balanced nested generic arguments while preserving only an explicitly mapped outer; reject positive/negative imbalance: `ENC-generic`, `ENC-nestedgeneric`, `ENC-unclosed`, `ENC-negative`.
- Explicit mapped reference and nested binary spelling: `ENC-reference`, `ENC-nested`.
- Return no descriptor for a mapped null/empty value, including array/generic/member argument/return use: six `JVM-PRODUCER-*` cases. The old `TryGetValue` condition was insufficient and has been corrected; whitespace remains truthy as in Python.
- Require nonempty declared binary owner and an exactly matching owner map; type declarations return owner: `KEY-owner-null`, `KEY-owner-mismatch`, `KEY-owner-unknown`, `KEY-class/interface/enum/annotation`.
- Fields and unsupported kinds do not prove typed identities: `KEY-field`, `KEY-unsupported`.
- Method arguments must all resolve and cannot be void; return must resolve; constructors use `<init>` and V: `KEY-primitives`, `KEY-T`, `KEY-voidargument`, `KEY-unknownargument`, `KEY-constructor`.

Current native source pin is `JavaSemanticServices.cs` SHA `dfe6f0514712e2f2c0b41fe59175f9981a04263126ea1bafe2ffa45bdb6dbb3d`. All preceding body clauses have direct managed-native evidence:39 existing JVM cases and six new falsey-owner cases in TRX `20261028T1125115312785-28d05369fba04ec7a19469f3c6fdfc17`, SHA `262ec3a160532d760325e05ead23f98303e499066d3a38ca59f849f4abff712f`. This is a complete pure-body contribution, not yet a complete producer/caller-profile claim.

Original callers were read: `build_catalog.add` calls `key` for Java-source declarations using the selected platform's SDK owner map and accepts a key only if present in that platform's SDK key map; `annotation_link.xml_jvm_key` delegates parameter/return encoding to `encode`. Native callers are `CatalogBuild` and `SourceTypeFacts`. Actual database integration exists as `SOURCE-IMPORT-02`; annotation SQL integration exists as `ANN-03/04/05/06`. Catalog owner confirms the remaining registered CLI gap: the seven existing Catalog CLI cases are non-Java and the combined Sources CLI case is SDK-only. A Java-selected registered CLI fixture is now authorized and staging against the one existing probe. No release011 historical cache/profile equivalence follows from identical function bytes.

## Java context producer: both published copies

Paths: `remediation/catalog/tooling/java_context.py` and `rehydration/producers/release-011/tooling/java_context.py`; SHA `4276f8abb3c0040fd1c8b4e9da52a163eb35d2d7716319327a884c0ed83fc55d`.

The sole body `attach` builds a cache-path report map, skips non-Java sources, requires matching cache and parser hashes, copies exactly package/imports/wildcard imports/parser/parse-error count/excluded-implementation counts, and adds the exact static-import uncertainty statement. These are mapped to `JavaFileContext.Attach`, `CTX-imports/cache/parser/missing/nonjava`, and `ORIG-CONTEXT-IMPORTS-EXACT` (exact imports cardinality, with an extra-key control). The native adapter clones retained JSON rather than sharing mutable report nodes. The implementation additionally rejects empty hash fields; full unconstrained-Python-object parity is not asserted from an admitted-source domain.

Actual published callers matter: repository text search found no invocation of `attach` outside its tests. `build_catalog.py` consumes an already attached `java_file_context` only in the processing fingerprint. A new live attachment pipeline must not be invented to close this helper. Current Catalog retains historical context as verified-unused audit and obtains new context from native extraction/cache metadata. That identity/profile transition is a separate Source/Catalog admission responsibility, not a missing second Java context implementation.

## Java AST producer: both published copies

Paths: `remediation/catalog/tooling/java_source.py` and `rehydration/producers/release-011/tooling/java_source.py`; SHA `ebc8f756771921d607ce7f30c20d66e69fd92d5a8a1a8fb9d52ff51b35064940`.

The one replacement remains `JavaSourceExtractor` over the pinned in-process native parser. Existing cases map package/import context, annotation name/argument qualification, declared types/binary nesting, overloads/constructors/annotation elements, fields/enum constants/dimensions, receiver parameters, excluded implementations, line/byte locators, malformed/missing nodes and immutable input. Those mappings do not establish every producer branch merely because the original eight test methods passed.

This review demonstrated and corrected concrete omitted clauses, after eight new discriminating mocks passed:

- Original record components are fields before the body, with empty declaration annotations, retained component annotations, unresolved propagation scope and unknown component visibility.
- A compact constructor uses the record header parameters, including their names/types/annotations, rather than an absent constructor `parameters` field.
- Nested type rows retain the containing `owner_kind`; interface/annotation nested class/interface/enum/annotation declarations default public, while nested class declarations retain package visibility without an explicit modifier.

Four same-case native bindings now pass, alongside all13 AST cases and six literal assertions, in TRX `20261028T1238431673238-3257e28506064311ba9a9fce856212e0`, SHA `b6ea2309b76e2fb53d8007a5a10e0825ffae507fb709924c481cbf0d3f2a7e4f`. Current extractor SHA is `2141d1c679dfe30b354a4564926c31280c03481b3e24428bcb47a95c29b5a697`.

Chronology is explicit: added `JAVA-10` previously expected the wrong omission of a record component and a zero-argument compact constructor. That fixture has been retained byte-for-byte, and only its demonstrated expected values were corrected; input bytes did not change. Earlier green13 is not full producer parity. Qualified original-method ledger now has SHA `ffc6d4ccc3301caf86dc9c0c421cedfb8201c2d0338ccd98c763374b09949f5e`, retaining prior `a9c45e6d...` and all original receipts.

One additional demonstrated producer gap remains unimplemented at this review: original `type_text` skips explicit-import replacement only for `full == 'java.lang.' + simple`, while native `TypeText` skips every `full.StartsWith('java.lang.')`. Thus `import java.lang.annotation.Retention;` with return/parameter `Retention` must become `java.lang.annotation.Retention` under the original rule but currently remains `Retention`. Annotation-name qualification uses a separate branch and does not establish type-text parity. A finite discriminating case is needed; no second parser or new symbol inference is required.

Registered Sources/Catalog caller proof must also be scoped to the explicit selected native manifest. The authorized current native regeneration does not claim historical Python wheel/package strings or release011 cache-byte identity. The complete AST producer files therefore remain open even after the corrected declaration cases.

## JVM erasure tests and caller responsibilities

Both canonical and release011 `test_annotation_jvm_erasure.py` have SHA `4fca71f6c2342e826de0bd676a2eee2ac56285097174840d94d850a4d369ca65`. Their seven methods map every assertion to `SourceTypeFacts.XmlJvmKey` using the existing shared LegacySignature lexical policy: qualified outer generic erasure (`XMLKEY-01`), unknown parameter/return T (`02/03`), unqualified java.lang alias (`04`), generic array plus annotation ordinal (`05`), malformed generic/void parameter (`06/07`), constructor versus ordinary class-named method (`08/09`), known nested binary type (`10`). Source owns this normal test-file closure; it must not be duplicated into a Java XML parser.

Both `test_annotation_jvm_erasure_import.py` copies have SHA `c918750834a2b1f60d9d2a853ea8dcdfbb8109d5e2b193aaedf991b69ceb77db`. Their four method clauses are `ANN-03` exact generic-erasure binding and target-match kind, `ANN-04` unresolved T, `ANN-05` no platform borrowing, and `ANN-06` ordinary A()I versus constructor. Native SQL observations retain evidence, values, one external permission relation when bound, and immutable originals. The source/check helpers' real archive/index/DB preparation is mapped to the existing `NativeCatalogAnnotationImportProvider` and shared build provider, with authorized native cache regeneration rather than historical gzip generation.

Catalog receipt authority is `catalog/evidence/producer-db-normal-method-closure.json`, SHA `0739c463c89a85bc3b3f05f818613a6df106f6f1a91a9d49349ef54be64d9330`. It explicitly distinguishes corrected-carrier original assertion closure from historical profile parity; it must not be relabeled as whole release011 producer execution. Source owns the ledger import, Catalog owns SQL/CLI/profile claims. No broad method-count-only duplicate closure is proposed.

## Remaining finite work

1. Bind the approved Java-selected registered Catalog fixture to the existing CLI probe, reusing SOURCE-IMPORT-02 preparation and SQL observer. Preserve the explicit native002 manifest and immutable raw inputs.
2. Mock-first the exact java.lang subpackage TypeText replacement difference, then amend the same extractor if that case demonstrates it.
3. Let Source/Catalog record the explicit current-native versus frozen-profile disposition. Pure helper body proof, current registered caller proof and retained historical producer/cache identities are distinct fields; none substitutes for another.

No Source-owned authority, parser, producer-fingerprint algorithm, cache codec, SQL observer or CLI implementation was duplicated by this review.
