# Root integration proposal: local Review/Assess

This is a private, normally proved local receipt-integrity audit caller. It is independent of full Prepare and cannot invoke a reviewer/provider/MCP. Root applies only the following additions to its current source, preserving Sources/Snapshot, AllOrNoneParameterGroups and every dispatch arm. Do not replace Root Build.cs or OperationRegistry.cs with private015-derived copies.

Observed Root context at 2026-10-29T03:06:37:

- Core/OperationRegistry.cs SHA256 b31d9276887b31e7bfb51a640e381b1f0ddc3bc68799af657fd235fe96331836.
- Build.cs SHA256 3f242bc564152a389257918d377eb065ae670699ce1d04de569f81ebefcc7e63.

Append ReviewReceiptAudit to OperationExecutor after existing SourcesSnapshot. Add this definition immediately before the existing Review/Prepare definition:

```csharp
Add(Category.Review,Operation.Assess,["project-root","output-root"],[],OperationExecutor.ReviewReceiptAudit,
    new("discover-local-review-receipts",[]),new("verify-local-review-receipts",["discover-local-review-receipts"]));
```

Add this dispatch arm without changing the existing Prepare pending behavior:

```csharp
case OperationExecutor.ReviewReceiptAudit: AssessLocalReviewReceipts().GetAwaiter().GetResult();break;
```

Copy the handoff's five exact files, using their canonical prior pins. No new parameter, category or operation enum is needed. Existing project-root and output-root are required; all other parameters are rejected by the shared planner. The thin Build.ReviewAssess.cs is compiled and exercised in the private app by the eight ordinary CLI cases, not by an internal process bypass. Each test child uses its own NUKE runtime root to avoid the enclosing test target's build.log lock.

For an actual selected project the caller is:

```text
dotnet exec AndroidMcp.Build.dll --root <separate-nuke-runtime> --target Review --operation Assess --project-root <selected-project> --output-root <new-audit-output>
```

The persisted output is output-root/review-receipt-audit.json. Selecting project/evidence as output-root retains the original logical location. It prints the exact persisted full audit JSON within ordinary NUKE output. A pre-existing output remains unchanged and causes OUTPUT_CONFLICT. Source receipt files are retained, never overwritten/deleted. No actual project receipt scan was performed for this proof.

Root already owns the unchanged shared ArtifactWorkspaceContracts.cs and NativeArtifactWorkspace.cs. These were copied into the private app only to bind its existing shared metadata authority. Their source pins are recorded in the handoff; they are not new Review code and must not be imported over newer Root code.

Full Prepare still requires the production OfficialPluginReviewAuditTransport/RequiredPluginReviewAudit and genuine runtime/own-turn prerequisite authority described in the retained PREPARE-PRODUCTION-BOUNDARY-018.md. No local audit JSON establishes a trusted source, acceptance, readiness, current model capability, HV or safe deletion.
