# Android binding/permission catalog — release 008 (PARTIAL)

The primary runtime file is `catalog.sqlite`: 799,527 discovery entries,
1,122,576 source occurrences and 43,128 source records.
The frozen acquisition inventory selects 43,128 paths. Its statuses
are {"downloaded": 43128}. It includes the original AOSP
corpus, Google's API37.2 source-data archive and platform metadata, plus any
explicitly identified supplemental source acquisition checkpoint.
It is not a global all-current-completeness claim: other repositories and
unresolved semantics are enumerated in the coverage and outside-corpus audit.

API37.2, package revision1 and SDK Extension24 are separate metadata facts.
All16,893 Java files were scanned (530,212 declaration records; zero grammar
errors);12 source members are empty and yield no invented declarations. The
official API lifecycle index has90,272 current declaration keys, with3,409
historical/future records excluded from that current view. Source text and JVM
identities remain distinct unless an exact method/type link is proven. Source
fields are not used to transfer permissions without a descriptor proof.

Permission metadata contains 1,715 names and 1,476
manifest-defined names. Core AOSP17r1 contributes1,160 definition elements:
1,138 unique names and22 duplicate elements retained as evidence. The source
identity reconciliation has no missing/extra imported paths. All552 legacy
release002 declaration-shape variants are retained in this generation.
All source occurrences/annotations and raw AIDL schemas remain accessible.
Unknown or unsupported metadata is retained; missing annotations are unknown,
not permission-free. Positive aggregate evidence retains absent-source variants
and is not a universal OS authority claim. OS grants and client policy remain
separate from source metadata.

Storage schema:1.2.0; Entry/API contract:1.x. Registry
generation:008; package identity namespace:2; source processing provenance:3.
Old snapshots and grants are not silently retargeted. Exact source facts use
nullable API applicability and unknown channel where unproven. Field types,
concrete type kinds, generic resolution, unsupported AIDL syntax, generated wire codes
and codecs remain unknown where the sources cannot prove them.

The frozen gate passed148 tests, including import purity, indexed Point access,
annotation alias ambiguity/type-variable rejection, cache integrity and registry
rules. The published release passed exhaustive schema, integrity/FK, hash/ETag,
source/declaration and export reconciliation. Reports are sidecars; validation
and permission export do not modify the published release directory.

`entries.jsonl` and `catalog.json` contain the same consolidated Entry documents.
Per-table JSONL retains declarations, sources, definitions and annotation evidence.
`permission-export-release008` contains the separately schema-validated permission
union. `release-manifest.json` gives exact hashes and uncompressed storage sizes.

No Android bridge, HTTP service, deployment, device operation, debugging change,
SDK installation or license acceptance occurred. OpenAPI3.1 is a CRUD contract.
The design/contract hostile-validation loop is owned by the separate review task;
no98/98 result is claimed here. Use coverage, not entry count, to assess scope.

Bulk raw snapshots remain preserved in the task workspace. Delivery variants may
omit bulk sources; frozen inventories, SDK archive receipts, metadata-range
receipts, source hashes, restoration scripts and attribution identify exact bytes.
Reproduce into a new output directory using the frozen code snapshot and input
inventory. No source code from Android was executed or built during extraction.
