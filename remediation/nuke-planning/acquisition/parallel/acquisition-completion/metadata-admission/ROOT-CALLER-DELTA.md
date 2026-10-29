# Thin Root composition

Keep Sources/Snapshot in the same registry/application. Compose its optional ISnapshotMetadataProofReader only from a caller-declared list of StoredArtifact pins through the existing IPinnedArtifactReader. Do not hash a newly discovered receipt and call that independent proof. The pinned input-list envelope may use Root's existing pinned workflow request reader; it is not another rehydration command or parser.

Default the external-proof input store to the same raw/output root used by the old inventory-parent caller. An explicit proof-root is a separate root-bound store binding, with the existing ArtifactPaths/native reader policy. The immutable recipe project root is a different purpose and must not be guessed as the data proof root. No missing proof file triggers a download.

The service invokes this dependency only for explicit download, selected missing rows, CanonicalRestore, original Restore mode, and the sole planner's exact bounded-metadata reference. Dry-run, exact source reuse and explicit Generic do not call it. Native preflight admits all selected dependencies before batch effects. The original per-proof receipt/platform/XML/package order is retained inside admission; the stronger established native all-input preflight remains explicit.

Record admitted receipt/XML StoredArtifact identities, source path/hash, row index and recipe SHA in SourceSnapshotResult.MetadataProofs. Omit this optional property when unused, retaining existing Snapshot result wires. Label the evidence local-input-integrity-only and OfficialSourceAuthority=false; no whole archive hash or availability claim. The actual shared range engine then independently enforces ETag/If-Match, HTTP206, length, identity encoding, selected CRC and final member SHA/Git/size.

Root owns actual CLI parameter/envelope admission and its CLI tests. Acquisition owns the reader, typed package-choice extension of the existing XML validator, and caller composition. Publication owns the per-row metadata reference projection. Prior sealed3increments remain unchanged and independently reviewable.
