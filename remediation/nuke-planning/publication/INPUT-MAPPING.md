# Publication and rehydration input mapping

This is the approved component boundary, not a claim that every root CLI adapter is implemented. The root registry is the sole CLI parameter authority. Generation, acquisition, parsing and catalog operations are delegated to the shared NUKE operations; this component does not create a second execution engine.

## Publication Check

The caller supplies the complete observed Git-tracked snapshot and the single `deliverables.json` policy. Each snapshot row carries portable relative path, length, and observed regular/symlink/directory/missing kind. Allow and deny use the legacy case-sensitive fnmatch semantics (`*` spans slashes); deny wins. Reject traversal, absolute paths, backslashes, drive/stream colons, duplicate/portable-colliding names, nonregular files and oversize files. Root adapters must collect trustworthy kind observations and the shared artifact store independently prevents reparse traversal. Check is read-only and starts no process.

## Publication Stage

The root maps `git-checkout`, `deliverable-manifest`, `output-root` and optional `source-root` to policy, hash-pinned entries, separate source/destination artifact stores and a shared lease. There is no Git-index operation, commit, push or publication side effect. The bundle is immutable by generation ID. Inputs have source path, destination path, byte length, SHA256 and media type. Preflight every input and destination before copying. Recheck pins at commit; write the generation manifest last. A matching committed generation is verified and reused without writes. A committed generation with a missing or conflicting output fails instead of silently repairing or relabeling it. Uncommitted partial copies survive failure/cancellation for diagnosis and safe identical resume. Other generations and unrelated destination files remain untouched.

## Rehydration Plan and manifest

The root maps legacy behavior into `Catalog/Rehydrate` using typed recipe/output/cache/resume inputs. Pure planning receives the recipe artifact and its lock; no default network, process, source download, archive extraction or data generation occurs. The root selects the current recipe from one authoritative configuration. An explicitly requested release must match the pinned recipe; historical releases are never substituted silently.

The legacy flags are behavior inputs, not executable instructions:

- `release` and `recipe`: preserve requested release and exact raw recipe hash; lock filename/hash/source-row/producer-file counts are verified.
- `workspace` and `output`: root binds separate stores/roots; input originals are retained.
- `limit`: nonnegative, explicit selected unique-path count. Total source rows and unique physical paths remain visible; a smoke subset never becomes a complete claim.
- `resume`: root verifies exact recipe identity before reuse. A plan does not claim to have verified existing cache bytes.
- `strict-cache-bytes`: root extraction/cache services must either preserve historical byte identity or record a distinct derived generation; never relabel historical pins. This planner preserves the original cache/source/parser pins as evidence.
- `delay`: acquisition operation policy, owned by Sources; planning does not sleep.
- `allow-file`: explicit local fixture transport only; HTTPS is the normal transport and URL credentials are rejected.
- `from-dir`: trusted owner-supplied snapshot bundle, owned by Sources acquisition/seed. Required immutable blob hashes and safe bundle paths remain in the plan.
- `doctor`, `seed`, `fetch`, `extract`, `build`, `validate`, `export`: map to shared source/catalog/validation operation keys. Legacy environment requirements are preserved as historical data and never authorize Python/Node execution.

Every source and metadata-cache path is portable/relative; source/cache/parser/producer hashes are checked; aliases sharing one input path may not disagree about bytes/length. Every selected source retains its context via the exact recipe identity. Manifest metadata preserves `expected`, `environment`, `provenance` and `limits` without turning expected counts into observed results. Unknown source sizes are counted separately from known bytes. Mutable upstream URLs require owner-supplied pinned bytes; an unverified archive identity must not be upgraded to verified because the member is pinned. Planning is always `network=false`, `complete=false`, `historical_byte_identity_claimed=false`.

Observed committed recipe evidence (read as data only): release-011 has 45,768 source rows, 16,894 cache rows, 16,916 archive-member rows and 85 pinned producer files. All source rows have SHA256 and size fields; all cache rows have cache SHA256, parser SHA256 and source SHA256 fields. Its preserved limits explicitly state selected-corpus coverage, possible formatting/cache-encoding changes, and unrecoverable historical caches/reviews/migration databases. These are not Android-global completeness claims.

## Help and provenance

Help must be generated from the root's authoritative operation definitions (key, description, required/optional parameters), rather than a second command table. It must distinguish a planned operation from an executable root adapter. No machine-specific host path, private marker, credentials, full raw provider stream, corpora, build cache, or automatic Git publication belongs in these outputs.

The component's own audit turn is `Codex-20261028T023437Z-plugin-session` / `req-20261028T023435Z-publication-component` in the explicitly selected task-3 workspace. Local mock/real receipts live alongside this plan. MCP private cache/profile material remains outside deliverables.
