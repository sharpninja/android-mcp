# NUKE migration checkpoint

This is an **incomplete migration checkpoint**, not a working replacement for the
whole legacy pipeline. One production C# NUKE application hosts the tools; the
`Tests` project is a test class library, not another generation CLI.

The infrastructure host currently exposes `Validate --operation MockGate`.
Category/operation contracts and independent assertion fixtures exist. Native
AIDL/signature and UI/deck implementations have been added after the first mock
gate; their real parity checks are pending. Catalog, acquisition, remaining
parsers, diagram orchestration, rehydration, publication and review services
remain unfinished. Do not execute the legacy Python or custom Node programs.

## Bootstrap and current gate

Use the exact SDK in [global.json](global.json). NUKE and test dependency pins are
in the project files and `packages.lock.json`; package sources are restricted by
[NuGet.Config](NuGet.Config). No credential or machine-specific host path is
committed. Select an already-installed native .NET host, not a provisioning shim.

From `remediation/nuke-app`, in PowerShell:

```powershell
$dotnetHost = '/absolute/path/to/approved/native/dotnet'
& $dotnetHost restore Tests/AndroidMcp.Build.Tests.csproj --configfile NuGet.Config
& $dotnetHost run --no-restore -- --target Validate --root (Get-Location).Path --operation MockGate --dotnet-host $dotnetHost
```

The first shared mock gate recorded 8,706 passing cases, zero failed and zero
unexecuted. Those are assertion/fake-provider checks, **not production parity**.
The copied publication snapshot passed its fresh expanded mock gate: 8,823 cases, zero failed, skipped or pending. Real bindings were compiled but not executed in that gate. See [native publication verification](../../validation/nuke-publication-checkpoint.json) for exact counts, TRX hash and unresolved dependency warnings. Full TRX
and process logs stay in local test artifacts; the checkpoint carries a concise
receipt and its exact TRX hash.

## Final command shape

The authoritative typed registry will expose `Sources`, `Catalog`, `Design`,
`Validate`, `Review`, and `Publish`, with a singular `--operation` parameter. It
will own legal combinations, parameter validation, dispatch, help, documentation,
tests and Gradle compatibility data. It will not create a target per old script.
The current enum/contracts are the seam for that implementation, not a claim
that every operation works.

Gradle remains a thin facade for required architecture/document targets. Kotlin
remains the Android server/UI language. Source pins, schemas and UI blueprint
remain canonical; generated views do not become competing authorities.

## Remaining migration work

- Implement and validate the one registry/planner and shared hashing, canonical
  JSON, filesystem, process and receipt services.
- Bind existing mock assertions to real catalog/CRUD/policy/resolver/storage and
  source/extractor services; complete the full legacy-operation coverage map.
- Build the pinned native Java support library without Python, Node or system
  library substitution; validate its ABI and lifetime behavior.
- Finish native documentation generation, architecture rendering, browser QA,
  deterministic packaging and thin Gradle delegates.
- Replace all documented rehydration, CI, validation, publication and review
  entrypoints; retain originals locally and publish no caches or corpora.
- Supply fresh full unit/integration TRX and review the completed migration and
  design-contract impacts in the PR. No Google API completeness scoring.

The migration belongs on `feat/nuke-tooling-migration`, with a draft PR to main.
This checkpoint does not authorize a direct push of migration changes to main or
claim the 98/98 independent acceptance threshold has been met.

## Review order

Complete normal build/test/parity verification, then mark the draft PR ready for review. Do not run HV before marking ready. Review and remediation occur in the PR afterward; do not merge without separate authorization. The current bootstrap host exposes only Validate/MockGate and has no provider launch path.
