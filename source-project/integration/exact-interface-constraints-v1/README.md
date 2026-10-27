# Exact interface source predicates v1

All530 AIDL frozen dump hashes verified against full pinned-tree membership and
9691 acquired source files. The new hash verifier is a read-only Python
reproduction of the pinned Google build algorithm; downloaded code was not run.
The pinned aidl_gen_rule.go selects physical tail -1 and passes it as --hash;
the AIDL constraint helper checks this separately from hash tool last-nonempty
verification. It rejects ambiguous/unstable module flags, unlinked frozen module
versions, partial parsing, nonexact source inputs and Descriptor overrides.
1199/1607 parsed interface declarations have exact predicates,408remain unknown.

The HIDL predicate requires exact current.txt descriptor plus source SHA256
equality in the same repository/revision and a parsed interface declaration.
592/765 interfaces satisfy this;173remain unknown. The normative reference is
https://source.android.com/docs/core/architecture/hidl/hashing . Runtime device
facts are never supplied by this source producer; the resolver fixture only
simulates trusted versus missing/untrusted/hash-mismatching observations.

Builder integration must map every source_evidence identity to actual imported
source IDs, preserve the predicate and its proof in hashed Entry metadata, and
include all producer code/data fingerprints in immutable input provenance. The
audited full-tree membership proof and pinned compiler metadata must remain
hash-bound inputs; do not trust an arbitrary external verified:true JSON flag.
These source predicates are orthogonal to complete type-dependency checks,
runtime reachability, transport/wire codecs, OS access and per-client grants.
Neither frozen AIDL N nor HIDL @major.minor is an Android/APEX version interval.

This is a consumer integration handoff, not a claim these predicates already
exist in running immutable release011. It must be wired into a fresh builder
fixture and frozen candidate together with the canonical resolver schema.
14 AIDL tests and10 distinct HIDL/resolver tests pass. The HIDL log executes15
because unittest also collects the imported5 fixture cases; none failed.
No device/system/Claude action or upload occurred.
