# Remaining pinned-recipe boundary cases

Checkpoint002 proves the initial 24 rehydration cases. It does not prove archive-container acquisition, a complete current-recipe CLI run, or every cross-row collision. The next validation increment must close these boundaries after the help mock/real gate; it must not silently claim them already covered.

The committed release-011 recipe was read as data: 45,768 rows, 36,936 unique local source paths, zero conflicting source aliases, zero source/cache source-pin mismatches, HTTPS-only effective transports, and one mutable upstream snapshot. There are 16,916 archive-member rows. Source member SHA256 does not establish container identity or download success.

## Cases to mock before implementation

- Valid archive URL/member/container pin preserves every field and exact source-member pin in a pure plan.
- Missing container pin remains explicitly unverified; source-member pin is not promoted to container verification.
- Absolute, parent-traversing or backslash archive-member paths fail before any effects.
- Archive-member transport with missing archive URL or missing member fails as an ambiguous transport.
- Malformed archive hash fails; HTTP/userinfo transports fail unless an explicitly permitted local fixture.
- Two metadata-cache rows sharing a path cannot disagree about cache, parser or source identity.
- A source-blob path may not double as a metadata-cache output path.
- Invalid source/cache hashes and malformed compressed or duplicate-key JSON fail deterministically.
- Snapshot requirements and archive records have explicit serialized field names and preserve exact pins; all verification/completeness flags remain false until separate real acquisition/validation.

Use a separate new case collection so adding mock-only cases does not accidentally make the earlier production-bound cases red before the new mock gate. Bind the same new cases to the real planner only after the green mock receipt. No source acquisition engine, filesystem engine, package/tool table or command registry is duplicated. Root owns CLI composition and the Sources owner owns acquisition/archive extraction.
