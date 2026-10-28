# Existing HIDL and raw retention caller scope

These five cases are staged outside the app while checkpoint007 sync is frozen. They are tests only, with no production patch or execution claimed. The original files remain unchanged:

- test_hidl_import.py SHA256 f2ed5252f0134c57e2f5d39354c9bad86b440aac1fe5d8c29f34b86e15c88691 contains three original methods.
- test_unnormalized_source_retention.py SHA256 9c8ed7ab045230ef917d082c851b20fa1f3d465ceb2e1167f9574a4fc361820c contains two original methods.

The staged cases preserve the exact source bytes, HIDL scope, explicit nullable API/platform inputs, SQL category filters and public flat report assertions. Ten mock rows contain correct replies and ninety-five individually wrong reply controls. Native bindings will use the same cases and assertions after a green mock gate, through the existing Source dispatcher/extractor, Build, private SQLite stage and canonical schema engines.

The structural import and package-version guard already have implementation paths. Their original DB assertions are not closed merely by Source extractor or scoped-projection proofs. The current generic issue persistence has demonstrated compatibility gaps: no mandatory issue for clean HIDL evidence, a generic invalid-encoding category instead of the original hidl-source query, always-base64 whole-source SQL raw, and no flat raw_source_encoding/normalizer_complete/compiler_validation/wire_resolution/android_availability report fields. This is distinct from whether exact source bytes survive in nested concerns or a raw-source sidecar.

Public report compatibility and byte preservation remain separate obligations. The UTF8 HIDL method requires SQL raw to equal the original text and the exact flat report fields. The invalid-byte methods require base64-decoded SQL raw to equal the original bytes under the original category query. No negative control is waived because some other metadata location retains equivalent evidence.

The annotation archive consumer is separate. Source's one archive parser retains member raw text/base64 and encoding, and its adapter already emits annotation-xml issues. Current Build stores those SQL issues with the whole ZIP base64 and retains detailed member concerns in parse_reports.source_metadata.annotation_archive.issues. That does not satisfy a requirement for per-member SQL raw evidence or locators. The original Round2 method currently asserts evidence/mapping/permission/issue counts, not per-member raw content; its exact original fixture must be bound independently before normal closure.

The aggregate SourceContractMetadata caller is also separate. Source leaf callback/taxonomy/applicability facts are already shared. Catalog still needs aggregate envelope/set validation, exact raw UTF8 occurrence hash/provenance checks, same-snapshot evidence joins, duplicate-context rejection, aggregate-unresolved inference clearing and attachment before effective record hashing. MetadataCatalog currently permits functional-group documents only, so its thirteen group CRUD cases do not close callback-descriptor CRUD or metadata aggregate behavior.

No general pipeline infrastructure, second parser/codec/hash algorithm, schema weakening, recipe mutation, runtime authority, corpus parity or HV acceptance is included in this bounded increment.
