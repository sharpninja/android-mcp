# Android MCP bridge — lean design and offline tooling

> **NUKE migration in progress:** see [progress and remaining work](MIGRATION-STATUS.md). Do not execute Python tooling or legacy commands. This branch includes an incomplete native NUKE checkpoint; normal migration validation precedes ready-for-review, and HV follows in the PR.

All-Kotlin server/Jetpack Compose design, internal ARC compatibility-port specification, C4/Structurizr and class sources, editable SVG screens/storyboards, a self-contained Marp presentation, catalog contracts, extractors and offline tests. **No Android application or deployed bridge is implemented here.**

The latest copied native checkpoint and exact validation evidence are recorded in [current progress](MIGRATION-STATUS.md) and the [publication receipt](validation/nuke-publication-checkpoint.json). The migration remains incomplete; legacy commands stay dormant.

Historical design evidence remains round023 (88 accuracy /81 completeness, NOT_ACCEPTED) and partial round024. No new HV is authorized before this migration PR is marked ready after completion and normal verification. Review and remediation happen in the PR afterward; no current acceptance is claimed.

- [Current design](<remediation/design-c4/docs/Android MCP Bridge Design.md>)
- [Build the diagrams and offline deck](remediation/design-c4/README.md)
- [Open the local presentation](remediation/design-c4/docs/storyboards.html)
- [Screen/control/storyboard specification](remediation/design-c4/docs/ui-screens-and-storyboards.md)
- [Candidate finding dispositions](remediation/design-c4/docs/review024-dispositions.md) and [validation summary](validation/design024-local-validation.json) and [publication checks](validation/design024-publication.json)
- [Current catalog/reference code](remediation/catalog/README.md)
- [Rehydrate selected source data and regenerate a catalog](REHYDRATE.md)
- [Publication allowlist](deliverables.json) and [cleanup provenance](validation/lean-cleanup.json)
- Completed partial round024 reports: [core](validation/hv-round024-pass1.md) and [architecture](validation/hv-round024-pass2.md); no unified score
- [Completed independent design verdict](validation/hv-round023-design.md) and [status](validation/status.json)
- [Third-party provenance and notices](THIRD_PARTY_NOTICES.md)

## Migration checks

Validation is local-only: run a locked native .NET restore, C# host build and NUKE Validate/Tests gate (MockGate selects only the mocks). No GitHub Actions are configured or planned. See [NUKE usage](remediation/nuke-app/README.md). Mock success does not establish production parity; full rehydration and production commands remain pending. Older tooling instructions, including [REHYDRATE.md](REHYDRATE.md), are retained as migration references and must not be executed until their callers are replaced. See [MIGRATION-STATUS.md](MIGRATION-STATUS.md) for the removal and readiness gates.

## What changed in this forward cleanup

Only the publication checkout was pruned. Original source/remediation workspaces and Git history are preserved. Historical/frozen review inputs and streams, preview captures, old documents, acquisition inventories, parser caches, catalog databases/duplicate exports, split evidence archives and intermediate integration copies are absent from the current tree. They are not being uploaded as a 145GB release.

One compressed **current selected-corpus recipe** and its producer are retained because source revision/hash pins and exact parser code are necessary to regenerate omitted data. This is not an entire AOSP mirror or a claim of global API coverage. The current reference candidate and the release011 producer are explicitly different versions; rebuilding the producer does not silently promote the candidate to production.

`tools/check_deliverables.py` checks every tracked path, including accidental files force-added past `.gitignore`. Existing broad checkpoint commits remain in history; this cleanup is a normal forward commit, not a history rewrite or a claim that older Git objects were erased.
