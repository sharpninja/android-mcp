# Original metadata reason parity

The existing `source_metadata_store.py:_append` rejects non-string and
whitespace-only reasons using `reason.strip()`. The native `MetadataCatalog`
used `.NET IsNullOrWhiteSpace` before this correction. Source inspection identified a narrow
compatibility difference: U+001C through U+001F count as whitespace in the
original definition but not in the ordinary .NET predicate.

This is an existing migration obligation, not a new capability or validation
policy. Preserve the original rejection and the exact untrimmed reason bytes
when non-whitespace text is present. All public mutations already converge on
the same private `MetadataCatalog.Append`; correct that one predicate after its
mock gate, without changing generic CLI whitespace handling or introducing
another string/SQL/JSON authority.

The staged 12-case test set reuses the existing metadata command provider,
independent original group document, canonical expected reply model and all
12 field/effect negative controls at every step. It covers each of the four
control separators, mixed controls, ordinary/Unicode whitespace, empty input,
zero-width space, normal text and retained control-wrapped text. Rejections must
leave no resource or revision; accepted values must preserve the original
reason. The SAME cases bind the existing native SQLite provider after the
24-test mock gate passes. Then run the old metadata CRUD/leaf/aggregate cases
and the current full suite before refreshing any source pin or original-method
claim. No original assertion or historical fixture is weakened.

Evidence sources:

- Local original: `remediation/catalog/tooling/source_metadata_store.py`,
  `_append` and its existing `reason.strip()` condition, read as text only.
- [CPython 3.13 whitespace switch](https://raw.githubusercontent.com/python/cpython/v3.13.0/Objects/unicodetype_db.h),
  `_PyUnicode_IsWhitespace`, including U+001C through U+001F.
- [.NET whitespace categories](https://learn.microsoft.com/en-us/dotnet/api/system.char.iswhitespace).

The 24-case mock gate passed before the working predicate correction. The SAME
12 native cases and existing CRUD/leaf/aggregate/capture regressions passed in
the 205-case affected gate. Exact source-delta and chronology evidence is in
`metadata-reason-native-proof.json`. Current unfiltered full-suite/source-pin
reconciliation is still pending; this does not close the whole producer.
Metadata revision-width and other complete producer-method obligations remain
separate. No Python, custom Node, Actions or HV runs.
