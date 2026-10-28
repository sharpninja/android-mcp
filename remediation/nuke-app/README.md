# Native NUKE migration checkpoint

This migration is incomplete. [Current progress](../../MIGRATION-STATUS.md), the [copied-gate receipt](../../validation/nuke-publication-checkpoint.json) and [sealed component status](../../validation/nuke-migration-checkpoint.json) distinguish verified scope from remaining parity. Never execute retained Python/custom Node commands.

## Native local verification

Install the exact approved SDK from global.json and select its native executable, without a provisioning shim. NuGet.Config and package lock files pin dependencies. From remediation/nuke-app in PowerShell:

```powershell
$dotnetHost = '/absolute/path/to/approved/native/dotnet'
& $dotnetHost restore Tests/AndroidMcp.Build.Tests.csproj --configfile NuGet.Config --locked-mode
& $dotnetHost build AndroidMcp.Build.csproj --no-restore
& $dotnetHost run --no-build --no-restore -- --target Validate --root (Get-Location).Path --operation MockGate --dotnet-host $dotnetHost
$nativeManifest = '/absolute/path/to/verified/native-source-lock.json'
& $dotnetHost run --no-build --no-restore -- --target Validate --root (Get-Location).Path --operation Tests --java-native-manifest $nativeManifest --dotnet-host $dotnetHost
```

The complete suite requires the pinned Java parser manifest and native artifact. Sources/NativeParser admits the selected compiler and pinned sources; its legal parameters are defined once in Core/OperationRegistry.cs. Reuse existing artifacts only after verifying their recorded hashes. Do not substitute a system parser or bootstrap Python/Node. Full TRX/process outputs stay under ignored artifacts/; concise publication receipts carry their hashes and counts. Normal tests include controlled real bindings, not an external HV launch.

## Read-only publication check

Publish/Check reads tracked paths and filesystem metadata, applies the shared allowlist policy and retains complete local captures. It never stages, commits or pushes Git. Select an admitted native Git identity with the exact observed executable path, SHA256 and version using the shared ToolIdentity schema. Store the machine-local identity descriptor outside tracked files, and use a fresh receipt root.

```powershell
$checkout = '/absolute/path/to/checkout'
$gitIdentity = '/absolute/path/to/local/admitted-git-identity.json'
$receipts = '/absolute/path/to/fresh/local/receipts'
& $dotnetHost run --no-build --no-restore -- --target Publish --root (Get-Location).Path --operation Check --git-checkout $checkout --git-identity $gitIdentity --receipt-root $receipts
```

Core/OperationRegistry.cs and its generated OperationHelpBuilder view are the sole command/parameter vocabulary. Functional categories are targets and --operation selects a task. Publication Stage means an immutable copied bundle, not a Git index. Sources/Fetch defaults to the validated pinned canonical restore profile; no missing immutable snapshot or archive proof silently turns into a network fallback.

Keep the feature branch and existing draft PR. Complete migration and normal verification before marking ready; HV/remediation happen in the PR afterward. No GitHub Actions, pre-ready HV, main push or merge.
