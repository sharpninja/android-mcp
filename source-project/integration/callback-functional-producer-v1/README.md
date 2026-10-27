# Callback and functional evidence producer v1

This local handoff addresses the new callback-stream and small functional-group
discovery requirements at the source producer layer. Remediation owns the
canonical schemas, registration/stream protocol, discovery and activation policy.

callback_source_facts.py exports callback/listener/interface type candidates,
method signatures, parameter indices/directions/annotations, reply modes,
registration/removal candidates, candidate lifecycle pairs, permissions, source
provenance and existing applicability variants. Exact source oneway flags are
retained. Missing flags remain unknown; no aggregate owner from another source
version is borrowed. Roles inferred from names or a Binder interface parameter
remain candidates and cannot trigger automatic registration or cleanup.

functional_source_facts.py exports explicit android.annotation.SystemService
facts (including raw expressions), and searchable owner/package namespace axes.
These do not automatically become functional groups. All unclassified types stay
searchable; the imported base and local override mapping remain separate. A
consumer may assign many groups, but must record its mapping evidence and sizes.
No functional description absent from source signatures has been invented.

The full release010 scan is a measured baseline, not the current production build
or global completeness. Scan counts and byte hashes are in the two report.json
files. Existing unnormalized native/generated and unresolved reference types
remain coverage gaps. 224151 nonprimitive parameters were not classified as
callback candidates; this count includes ordinary non-callback references and
unresolved roles, not 224151 confirmed missing callbacks.

The callback audit report's tests:9 field predates the added null-parameter-name
regression. The bundled v2 log records all10 tests pass against exactly the
producer hash in that report; it is the authoritative regression count. The
first failed scan and its partial files remain in the source workspace separately.

Integrate the pure helpers in an isolated next candidate and include their hashes
in the source processing fingerprint. Produce corresponding versioned metadata
from each Entry occurrence, preserving source/version axes; do not flatten an
aggregate name match into a proven lifecycle. Runtime stream tokens, sequence,
backpressure, consent, cancellation/death and executor behavior need the separate
consumer contract; no stream or device action has been performed here.
