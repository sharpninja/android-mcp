# Retained gate command spelling correction

The immutable `20261028T2248213616869-ArchiveBatchReuseMockTests.json` and `20261028T2251468891012-ArchiveBatchReuse.json` receipts recorded their exact class-scope names after `-TestFilter` but omitted the `FullyQualifiedName~` prefix in that descriptive field. Their TRX pins, counts, test lists and chronological source hashes remain valid. The actual commands were:

```powershell
pwsh -NoProfile -NonInteractive -File remediation/parallel-work/acquisition/Run-NukeNativeGate.ps1 -TestFilter FullyQualifiedName~ArchiveBatchReuseMockTests
pwsh -NoProfile -NonInteractive -File remediation/parallel-work/acquisition/Run-NukeNativeGate.ps1 -TestFilter FullyQualifiedName~ArchiveBatchReuse
```

The initial sandboxed mock attempt aborted before running tests because VSTest's loopback socket was denied. The successful mock28 and native42 runs used the same commands with the permitted execution escalation. The interrupted pre-reconnect session33600 still has no recovered outcome; it is not test evidence. This correction retains every prior receipt and does not alter its bytes.
