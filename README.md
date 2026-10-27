# Android MCP bridge — lean design and offline tooling

All-Kotlin server/Jetpack Compose design, internal ARC compatibility-port specification, C4/Structurizr and class sources, editable SVG screens/storyboards, a self-contained Marp presentation, catalog contracts, extractors and offline tests. **No Android application or deployed bridge is implemented here.**

The last completed independent design review was round 022: **92 accuracy /89 completeness, NOT_ACCEPTED**. Corrected round 023 remains under the original task's supervision. This checkpoint is published at the user's request independently of HV acceptance; it does not claim 98/98.

- [Current design](<remediation/design-c4/docs/Android MCP Bridge Design.md>)
- [Build the diagrams and offline deck](remediation/design-c4/README.md)
- [Open the local presentation](remediation/design-c4/docs/storyboards.html)
- [Screen/control/storyboard specification](remediation/design-c4/docs/ui-screens-and-storyboards.md)
- [Current catalog/reference code](remediation/catalog/README.md)
- [Rehydrate selected source data and regenerate a catalog](REHYDRATE.md)
- [Publication allowlist](deliverables.json) and [cleanup provenance](validation/lean-cleanup.json)
- [Concise independent verdict](validation/hv-round-022.md) and [status](validation/status.json)
- [Third-party provenance and notices](THIRD_PARTY_NOTICES.md)

## Quick checks

From the checkout root, using an environment with the pinned extraction dependencies:

```sh
python tools/rehydrate.py --help
python tools/rehydrate.py plan --workspace ../android-mcp-data
python -m unittest discover -s tools/tests -v
python -m unittest discover -s remediation/catalog/tests -v
python tools/check_deliverables.py
python tools/check_documentation.py
```

The default rehydration plan makes no network requests. Full acquisition/builds require explicit commands and substantial time/disk. Read [REHYDRATE.md](REHYDRATE.md) before running them.

## What changed in this forward cleanup

Only the publication checkout was pruned. Original source/remediation workspaces and Git history are preserved. Historical/frozen review inputs and streams, preview captures, old documents, acquisition inventories, parser caches, catalog databases/duplicate exports, split evidence archives and intermediate integration copies are absent from the current tree. They are not being uploaded as a 145GB release.

One compressed **current selected-corpus recipe** and its producer are retained because source revision/hash pins and exact parser code are necessary to regenerate omitted data. This is not an entire AOSP mirror or a claim of global API coverage. The current reference candidate and the release011 producer are explicitly different versions; rebuilding the producer does not silently promote the candidate to production.

`tools/check_deliverables.py` checks every tracked path, including accidental files force-added past `.gitignore`. Existing broad checkpoint commits remain in history; this cleanup is a normal forward commit, not a history rewrite or a claim that older Git objects were erased.
