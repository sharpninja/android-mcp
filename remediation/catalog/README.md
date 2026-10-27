# Catalog reference implementation

This directory contains the maintained catalog schema, JSON contracts, extractor/runtime projection helpers and regression fixtures. It describes a partial selected Android corpus, not an implemented Android application or a claim of global API coverage.

Run the reference regression suite from the repository root after installing the pinned extraction requirements:

```sh
python -m unittest discover -s remediation/catalog/tests
```

The immutable release011 reproduction producer is separate: see [rehydration instructions](../../REHYDRATE.md). Generated databases, raw source blobs, historical candidate copies and full review inputs remain outside this lean repository. The two small DDL fixtures under `../evidence` are migration test inputs, not copies of historical databases.

See the [design](<../design-c4/docs/Android MCP Bridge Design.md>), [publication status](../../validation/status.json), and [third-party provenance](../../THIRD_PARTY_NOTICES.md).
