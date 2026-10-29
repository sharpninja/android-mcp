# Shared artifact root ancestry correction

This is the already identified shared-store confinement gap in the NUKE migration, mapped to TEST-BUILD-002. It changes no Android behavior or public protocol.

Observed source: `Core/ArtifactPaths.cs` checks the selected root and components below it, but does not check ancestors above the root. A root such as `alias/child/output` can therefore traverse a symbolic link at `alias` without the root itself having the reparse attribute. Both the immutable artifact store and generation leases consume this authority.

The correction will inspect the full ancestor chain before any directory creation, file read/write or lease-file creation. It will preserve the ordinary existing/new-root behavior, existing relative-path validation and immutable byte receipts. Dangling links must also be rejected rather than hidden by `File.Exists`/`Directory.Exists` returning false. This is an explicit path policy; it is not a claim of resistance to hostile concurrent filesystem mutation between managed path admission and open.

Mock-first scope is `Tests/Core/ArtifactRootBoundaryMockTests.cs`: eight independent root-topology scenarios bind existing IArtifactStore/IArtifactLeaseProvider seams. Correct responses must satisfy all read/write/lease assertions; each unsafe admission and each normal receipt field has a separate negative control. No production path correction has been written at this point.

After the green mock gate, native bindings will construct test-owned temporary ordinary/link/dangling/ancestor topologies, exercise the SAME assertions and verify that the outside sentinel and tree are unchanged. The shared ArtifactPaths implementation will be corrected, and leases will revalidate after creating their own parent directory, matching the immutable store's existing recheck. No separate path policy or implementation will be added per consumer. Run focused bindings plus existing store/lease cases, then the full currently implemented suite before the next checkpoint. Do not report Windows symbolic-link privilege or cross-host proof unless actually observed.
