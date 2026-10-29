# Exact source constraint callable parity plan, awaiting Root acknowledgment

Scope is only NEW `Modules/Sources/Constraints/` and `Tests/Sources/Constraints/` in the isolated Documentation app. No production code has been written for this slice. No canonical, Core, existing Sources, registry, authority, factory or metadata-reference-array edits are authorized by this plan. Root acknowledgment and a passing independent mock gate precede production implementation.

## Exact originals and callers

- `tools/source_facts/aidl_source_constraint.py`, dc7c0d698d4ff811dbd8cd604316e8ab3ac03974daeafde7e71da26a0dfaa01d,2902 bytes: public `stable_module(module)`, public `exact_constraint(source,declaration,audit,published_text,module,linkage)`, and nested unknown(reason) return helper. The nested helper is projection behavior, not another public API.
- `tools/source_facts/hidl_source_constraint.py`,194514644686213599fc77326e4f9cd2c17141b761173b8297e92032337a44ed,1882 bytes: public `exact_constraint(source,record,facts)`.
- Original own tests: test_aidl_source_constraint.py e8977f3942fd961b74b7b9dbde4a05b6d8afdfb425b4eea184a65d0a4dc95bc1,2343 bytes, seven original methods; test_hidl_source_constraint.py51abec7fc9f4c2125af3165247f0f56eb07de8e170c98d98893f3d01beea987e,1751 bytes, five original methods. Fixture/source/fact/record helpers are harness setup, not additional test methods.

All four whole files were read from their exact retained published locations under publication/android-mcp-git/tools/source_facts. Sources' existing source-pure-function-caller-map-022.json, SHA f03b47ef3701e701a9eb0a83b3196120e3175c11ce8dc7efbca4ac46283d2147, identifies only the own tests as published imports. InterfaceModules main does not invoke these helpers; source_metadata_reference consumes caller-supplied constraint arrays and does not establish producer behavior. Sources confirmed this ownership split. A separately guarded lookup across exactly49 current direct production Sources C# files found zero stable/exact function or emitted constraint-contract matches. No native equivalent was established by these receipts.

There will be no CLI, category/operation, main dependency, generator, source factory binding, transport or caller acquisition added. The replacing callable C# API and the SAME original tests are the complete required caller boundary for this slice.

## Contract and implementation shape

Namespace AndroidMcp.BuildTool.Sources.Constraints. Add two narrow interfaces/classes in the owned new directory:

`IAidlSourceConstraints.StableModule(JsonObject module) -> bool`

`IAidlSourceConstraints.ExactConstraint(JsonObject source, JsonObject declaration, JsonObject audit, string publishedText, JsonObject module, JsonObject linkage) -> JsonObject`

`IHidlSourceConstraints.ExactConstraint(JsonObject source, JsonObject record, JsonArray facts) -> JsonObject`

Use existing loaded-JSON input trees and JsonNode.DeepClone; do not parse source text, implement JSON serialization/hashing, read files, calculate new source hashes, infer versions, validate a device, or enforce a new downstream schema. Inputs are admitted loaded-JSON object/list shapes used by the originals; required key absence and incompatible nested container shapes remain exceptions rather than invented unknown results. Native KeyNotFoundException/ArgumentException represent Python KeyError/container-type failures; arbitrary Python objects and their aliasing are outside the native loaded-JSON profile. No prevalidation may reorder the original early-return/error boundary.

A small internal owned projection helper may centralize the exact unknown object, interface clause, required-field access and common fixed nonauthority flags. It is not a second JSON/metadata engine. Required source identity keys are repository,revision,path,sha256; preserve all original values, explicit nulls and exact field sets. Do not synthesize provenance. Additional nested hash/module/fact evidence is copied using the existing JSON object model. Preserve original output key insertion order where constructed, though these APIs do not serialize bytes.

Reuse ILegacyJsonValueEquality/LegacyJsonValueEquality for Python loaded-JSON structural/numeric/bool equality, including exact source-input membership and HIDL evidence deduplication. Bool false identity for the AIDL unstable flag remains stricter than numeric zero. Local selection truthiness uses existing JSON value kinds/counts and that equality boundary for numeric zero; no general Python runtime emulator is added. Reuse SourceText.OriginalWord for Python word categories, SourceText.Lines for splitlines and SourceText.Strip for strip. Descriptor validation is only the original predicate over an already parsed descriptor; no Soong/AIDL/HIDL parser is added. Initial identifier characters remain ASCII letter/underscore; following characters use shared original-word Rune semantics. HIDL version digits retain original Unicode decimal-digit behavior. Match the complete descriptor, including rejecting final newline. AIDL's original regex allows a single identifier segment; do not invent a mandatory dot.

The private clone lacks the existing LegacyJsonValueEquality two Core files and has the older SourceText. Root must explicitly authorize prerequisite-only guarded copies before native compilation; no alternative definitions will be made:

- Core/LegacyJsonValueEquality.cs: canonical9c12279b5f55ff7ef0b15e9114f8db29e026117e57817448636365b5f651c2bb,2581 bytes; private target absent.
- Core/LegacyJsonValueEqualityContracts.cs: canonical1acaf609103b51664db226a0fc07722cecc4b921ffc2bddac563895671353056,319 bytes; private target absent.
- Modules/Sources/SourceText.cs: canonical925b87aa5a90b0f63ed8273072e79b431a908a301a86c6b03a132ac0ba87b580,2169 bytes; private prior849c49c822af8c392b180d5edb4914f9461f90bca31ad61ce1a00f3bf8e0145d,1747 bytes. Existing private StrictJsonInspector.IsIntegerToken supports the equality implementation dependency. Originals/prior bytes will be retained, source/prior/post hashes guarded, and no shared service changed.

## AIDL exact order and output

stable_module returns false unless kind is aidl_interface and complete is truthy. It selects every unstable item. Absence is the stable default. Exactly one literal whose value is the Boolean false is stable; duplicate flags, literal true/zero/null and unresolved expressions are not stable. Missing required item keys preserve their exception behavior when evaluated.

exact_constraint follows these ordered checks and exact unknown_reason strings:

1. Interface kind, direct depth(default0, original loaded-JSON equality), methods-parsed: `No complete direct parsed AIDL interface declaration`.
2. Any annotation whose last dotted name is Descriptor: `Descriptor annotation requires explicit compiler descriptor semantics`.
3. String/full descriptor pattern: `No qualified AIDL descriptor`.
4. New four-key source identity must equal a full source_inputs member, not a subset: `Exact declaration is not in reconciled hash input set`.
5. Source repository/revision equal audit values: `Frozen hash audit revision differs`.
6. Audit result verified truthy: `Frozen dump hash did not verify`.
7. Exact linkage status: `Frozen module version not uniquely linked`.
8. stable_module: `Stable AIDL build flag is unresolved or unstable`.
9. Source repository/revision equal module source: `Build declaration revision differs`.
10. The last physical splitlines element, stripped, equals audit computed_hash: `Backend tail -1 hash selection differs from verified hash`. Empty text selects empty string; an extra blank physical line remains significant. Do not call a helper that discards blank lines or take the last nonempty line. Do not add a40-hex validation the original lacks.

Success has only the original keys: contract exact-aidl-source-constraint/1; clause.interface descriptor,minimum_version:null,accepted_hashes:[expected]; frozen_version_literal copied without numeric inference; source_evidence identity,deepcopy(hash_source),deepcopy(module.source); runtime_observation:false; android_platform_range:null; wire_codec_proven:false; os_or_client_authority_granted:false; source_hash_verified:true; backend_selection `pinned aidl_gen_rule.go --hash tail -1`; the exact two original limitations in their original order. Missing required result/evidence fields are read only at the original successful branch position. Unknown result is only {unknown_reason}, with no invented success flags.

## HIDL exact order and output

Checks and exact reasons:

1. Interface record: `Published package/type hash does not establish an interface descriptor`.
2. Full parsed FQName string: `No direct parsed HIDL interface FQName`.
3. Source digest is exact lowercase64-hex string: `No exact SHA256 source bytes`.
4. Match every fact with original format, declared identifier, digest literal and same repository/revision: `No same-revision published HIDL interface hash equals these exact source bytes` when none. Do not infer .hal bytes from types/package facts, version literals or unrelated hash strings.

On success, output exact-hidl-source-constraint/1 and the same interface clause shape. source_evidence begins with the new four-key source identity, then full deep-copied fact source objects, deduplicated by original full loaded-JSON equality in first-seen order. Matching facts are not deduplicated: published_lines preserves all matching line values/order, including repeats. Preserve original source_bytes_hash_equals_published_identifier_hash:true, runtime_observation:false,android_platform_range:null,wire_codec_proven:false,os_or_client_authority_granted:false and exact normative_reference URL literal. No network fetch is implied. Missing line/required metadata and wrong evaluated container shapes are exceptions at the original point, not unknown results.

## Mock-first and SAME native gate

First implement shared case/observation assertions and independent scripted providers only, with no production body. Every original fixture/input/assertion remains literal:

AIDL seven: exact frozen clause/runtime false; unstable literal true and unresolved rejection; trailing physical blank-line rejection; different source bytes not borrowed; Descriptor annotation override unknown; development dump cannot borrow frozen version; absent stable default versus incomplete properties.

HIDL five: exact clause/two evidence identities/runtime false; wrong hash rejection; other repository rejection; type record not interface; another identifier not borrowed.

Add finite discriminating cases for every listed unknown reason and first-failure precedence; wrong declaration depth/status; qualified annotations and descriptor boundaries; absent/duplicate/false-versus-zero unstable flags; verified/complete truthiness; source-input full equality versus extra/missing keys and key order; revision separation; all splitlines/strip boundaries; exact field/null/Boolean/contract/limitation values; hash/version noninference; HIDL format/identifier/hash/repository/revision filtering; multiple matches, evidence dedup/order and repeated published lines; evidence copy isolation; required-field exceptions without input mutation. Include Unicode original-word/decimal digit boundaries using the existing shared helpers. Use the existing JSON equality service for expected membership behavior, with independent fixed expected results, not production-derived values.

Correct independent providers must satisfy every assertion; per-field/row/branch/error/order/input-mutation controls must reject wrong mock responses inside a passing gate. Retain the exact source pins and mock TRX before writing implementation. Then switch only the provider to the real new classes and run the SAME cases/assertions, followed by the affected source shared-helper regression filter through the existing private NUKE native gate. No unbounded full suite in parallel, no original Python execution and no HV.

After proof, seal only owned new module/tests plus the planning/evidence map, with absent-target guards and exact source/prior hashes. Shared prerequisite snapshots are references/guard receipts, not another implementation. Root integrates and owns category/producer inventory decisions. Only the three public original functions, nested unknown projection responsibility and twelve genuine original test methods can be proposed; parameterized boundary cases and fixture helpers do not inflate original method counts.

Pending Root acknowledgment: exact callable/input profile and typed native exception boundary above; prerequisite-only copies of the existing three shared files; independent mock gate followed by SAME native implementation. No production or caller wiring has been changed.
