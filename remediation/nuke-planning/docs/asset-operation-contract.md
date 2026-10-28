# Next increment: immutable native Assets operation

Planning only during checkpoint 002. No Assets operation app types or implementation have been written. The exact required input names are `docs/ui-blueprint.md` and **`docs/Android MCP Bridge Design.md`**. `docs/android-mcp-design.md` does not exist and must not become a silent alias.

## Service boundary

`DocumentationAssetOperation` will use separate bound source and destination `IArtifactStore` instances, shared `IArtifactLeaseProvider`, `IDocumentationProjector`, and shared `IContentHash`. It will not implement filesystem access, hashing, process execution or a new CLI. Root wiring owns canonical store roots and passes an output-root lease identity derived from the same normalized destination root used to construct the store. Run/generation identity comes from the root operation host, not session metadata or a fabricated audit ID.

The destination is a new immutable generation. The source remains retained and unchanged. A directly shared source/destination store is rejected before work; all projected destination paths are also probed before writing, which rejects a destination bound to the canonical project because the generated design path already exists there. The host must validate canonical root identity and supported separation rules rather than trusting different object references as proof of different filesystem roots.

## Required ordering

1. Acquire the lease keyed by canonical output root, not by a caller-chosen generation ID alone.
2. Read both exact canonical inputs. Missing/malformed input prevents every write. Retain the input bytes and independent shared SHA256/length metadata.
3. Invoke the already real-proven pure projector. Reject duplicate/case-colliding output paths and use the shared store's portable-path/reparse validation for every path. The reserved commit-manifest path may not be supplied by the projector.
4. Probe every planned artifact path and the commit-manifest path before the first write. Existing bytes, even identical bytes, mean the requested generation is not fresh and require a new output root. A permissions/path error is not treated as a missing file. Report collision/preflight errors without partially writing outputs.
5. Write projected artifacts in ordinal path order. Verify every returned `StoredArtifact` path, SHA256, length and media type against the requested bytes. Stop on any failure and retain partial uncommitted files; no deletion or rollback of retained originals.
6. Re-read both source inputs and compare against their admitted hashes before committing. If a source changed, reject the generation and leave it uncommitted.
7. Write `generation-manifest.json` last. The manifest contains schema/kind, root-provided generation ID, the two source metadata records and all projected artifact records. It does not hash itself or include machine paths. Exclude timestamps from reproducible manifest bytes; root `OperationReceipt` owns operational timestamps.
8. Return a successful typed generation result only after the manifest's stored receipt also matches. Release the output lease on success, failure and cancellation.

A failed partial write is never accepted merely because a filename exists. Existing generations must be independently verified from manifest bytes and every referenced artifact before any reuse. The initial Assets operation rejects an occupied generation instead of quietly converting it into an update operation.

`IArtifactStore` currently supports known-path reads, not directory enumeration. Its preflight can cover every planned output and reserved manifest path. If the product contract additionally requires proving that the entire destination directory is empty, root must provide that capability once in the shared store/preflight layer; the documentation service must not add its own filesystem scan.

## Mock-first test specification

All cases use recording fake stores, a recording fake lease and a scripted projector. The same assertions later bind the real operation to those fakes. Correct mock scripts must call the actual fake dependencies in the specified order; precomputed pass booleans or invented traces are insufficient. Negative controls deliberately return incorrect receipts and interaction sequences and must be rejected inside the passing mock harness.

- Success: both exact input reads, complete output preflight before any write, deterministic artifact order, second input reads, manifest-last, matching source/output metadata, retained input bytes and released lease.
- Missing blueprint; missing design; both missing: no projector or output writes; exact missing path(s) reported.
- Projector rejects malformed source: no destination writes and lease released.
- One collision at the final sorted output path; commit manifest collision; multiple collisions: zero writes despite earlier successful probes, originals retained.
- Duplicate projected path; case-colliding projected path; reserved manifest output; shared-store binding; shared-layer unsafe/reparse path rejection: fail before the first write.
- A target read fails with a real I/O/permission error: reject preflight, do not classify it as absence.
- A write fails after one successful artifact: retain uncommitted files, do not write manifest and do not return success.
- A store returns a wrong path, digest, length or media type: detect each independently and do not commit the manifest.
- Canonical input changes during the write sequence: detect the re-read mismatch, preserve source and partial outputs, omit commit manifest.
- Cancellation before lease, during preflight, during artifact writes and before manifest: no false success, lease released when acquired, no manifest commit after cancellation.
- Native filesystem integration: root constructs real `FileArtifactStore` bindings over retained project input and a unique destination, invokes the operation through `Design --operation Assets`, verifies all 222 projected artifacts plus the last manifest, reopens every generated file/hash and proves canonical input hashes unchanged. A rerun into the occupied generation is rejected without byte changes.

The central registry will wire `Design/Assets` to this operation after the mock gate and same-assertion real gate. No GitHub Actions workflow or second operational entrypoint is introduced.
