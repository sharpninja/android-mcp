# Native NUKE migration checkpoint

This is an incomplete migration. [Current status](../../MIGRATION-STATUS.md) and the [copied-gate receipt](../../validation/nuke-publication-checkpoint.json) distinguish verified native cases from remaining legacy parity. One production C# NUKE application hosts all categories; Tests is a test library. Never execute legacy Python or custom Node.

## Bootstrap and local verification

Install the exact approved SDK from global.json. Dependencies and sources are pinned by the project/lock files and NuGet.Config. Select an installed native .NET executable, without a provisioning shim.

From remediation/nuke-app in PowerShell:

```powershell
$dotnetHost = '/absolute/path/to/approved/native/dotnet'
& $dotnetHost restore Tests/AndroidMcp.Build.Tests.csproj --configfile NuGet.Config --locked-mode
& $dotnetHost build AndroidMcp.Build.csproj --no-restore
& $dotnetHost run --no-build --no-restore -- --target Validate --root (Get-Location).Path --operation MockGate --dotnet-host $dotnetHost
```

The complete suite additionally requires a pinned native Java parser manifest and its verified native artifact. Use Sources/NativeParser with an approved compiler identity and output root; its authoritative required/optional parameters are defined once in Core/OperationRegistry.cs. Native source acquisition may access the network; select its pins and compiler deliberately. A prior manifest and artifact can be reused after verifying their recorded hashes. No system parser substitution or Python/Node bootstrap is used.

```powershell
$nativeManifest = '/absolute/path/to/verified/native-source-lock.json'
& $dotnetHost run --no-build --no-restore -- --target Validate --root (Get-Location).Path --operation Tests --java-native-manifest $nativeManifest --dotnet-host $dotnetHost
```

Full TRX and process logs stay under ignored artifacts/. The publication receipt carries hashes and counts, not captured binaries or full logs. MockGate proves assertion discrimination with fakes; the full Tests operation also exercises real bindings. Neither alone establishes full legacy or deployed-runtime parity.

## Commands and boundaries

Core/OperationRegistry.cs is authoritative for legal category/operation pairs, parameters, dependencies and executable adapters. Functional categories become NUKE targets; one --operation parameter selects a task. OperationHelpBuilder produces help from that registry and pinned project metadata; no independent command vocabulary is maintained here.

Kotlin remains the Android server/UI language. Gradle remains a thin orchestration facade. Sources, schemas and the UI blueprint remain authoritative; generated outputs are views. Publication Stage copies an immutable bundle, not a git index, and never commits or pushes.

The migration belongs on the feature branch and existing draft PR to main. Finish migration and normal verification, then mark ready; HV and remediation occur in the PR afterward. No HV before ready, no GitHub Actions, no main push and no merge without separate authorization.
