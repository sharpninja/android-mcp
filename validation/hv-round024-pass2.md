I'll inspect every pass-2 diagram SVG against its source, cross-check the class views against the crosswalk and module contract, and record defects, fidelity gaps and cross-pass questions without issuing a score.

All twenty-one assigned SVGs were read in full and reconciled with the three shared sources. The six class SVGs reproduce their PlantUML blocks element for element. Nineteen of the twenty-one projections are faithful to the sources. Two component SVGs drop one relationship description that exists in the DSL, and that description is the only place the on-demand screen.state and input.tap claim is carried. Beyond fidelity, the class views leave several mechanisms that the component and dynamic views assert without a drawn port, realization or owner. Hashes could not be recomputed and no visual render was possible, so evidence was treated as the supplied bytes.

## Coverage and projection fidelity

| Assigned file | Reconciled against | Result |
| --- | --- | --- |
| arc-controller-classes.svg | code-diagrams.md arc-controller block | Every class, member, note and link present; title matches |
| arc-target-classes.svg | arc-target block | Every class, member, note and link present |
| arc-ui-classes.svg | arc-ui block | Every class, member, note and link present |
| server-adapters-classes.svg | server-adapters block | Every class, member, note and link present |
| server-core-classes.svg | server-core block | Every class, member, note and link present |
| server-execution-classes.svg | server-execution block | Every class, member, note and link present |
| structurizr-Context.svg | workspace.dsl Context view | 7 elements, 6 relationships, all labels and technologies match |
| structurizr-Containers.svg | Containers view | 9 elements, 8 relationships match; documentation container correctly excluded |
| structurizr-Deployment.svg | ReferenceAndroid environment | All nodes, instances and 8 relationship lines match |
| structurizr-ServerComponents.svg | ServerComponents include list | 15 elements, 51 relationships drawn; link lnk59 has no label |
| structurizr-ServerCoreComponents.svg | ServerCoreComponents include list | 14 elements, 45 relationships, all labels match |
| structurizr-ExecutionComponents.svg | ExecutionComponents include list | 10 elements, 26 relationships drawn; link lnk30 has no label |
| structurizr-ArcComponents.svg | ArcComponents include list | 15 elements, 34 relationships, all labels match |
| structurizr-Startup.svg | Startup dynamic view | 8 steps match |
| structurizr-CallbackFlow.svg | CallbackFlow dynamic view | 19 steps match; technology labels placed on the correct steps |
| structurizr-InvocationFlow.svg | InvocationFlow dynamic view | 19 steps match |
| structurizr-GroupFlow.svg | GroupFlow dynamic view | 14 steps match |
| structurizr-ArcSession.svg | ArcSession dynamic view | 8 steps match |
| structurizr-OwnerTransition.svg | OwnerTransition dynamic view | 10 steps match |
| structurizr-Shutdown.svg | Shutdown dynamic view | 6 steps match |
| structurizr-CredentialRevocation.svg | CredentialRevocation dynamic view | 9 steps match |

Shared-context checks:

- **workspace.dsl.** Every dynamic step is backed by a declared relationship. Three declared relationships appear in no rendered view: documentation to owner, tooling to catalog, and invoke to arcController.
- **code-diagrams.md.** Every crosswalk row agrees with module-contract.json on both component and project. The evidence block ends at a different byte count than the coverage index declares, see finding P2-17.
- **module-contract.json.** The dependency graph is acyclic. I traced every drawn dependency, composition and realization across all six class SVGs and found no edge that crosses the graph in a forbidden direction.

Byte-count discrepancy for the crosswalk source:

| Source | Declared size of code-diagrams.md |
| --- | --- |
| Coverage index | 36499 |
| Evidence block end offset | 36475 |

## Findings

Severity reflects impact on architectural correctness and implementability. Items marked Material are concrete unspecified mechanisms or contradictions. Items marked Minor are consistency, ownership or labeling defects.

1. **P2-01, Material. Revocation cannot reach CallbackBroker at class level.** `design-c4/architecture/workspace.dsl:69` and CredentialRevocation step 7 require ClientAuthenticator to push closure to CallbackBroker. In server-core-classes.svg the only invalidation port is SessionInvalidator realized by SessionInvalidationFanout, which fans out to PrincipalSessionSink. The realizers drawn are McpSessionRegistry, ArcSessionRegistry and ArcSessionManager only. CallbackBroker realizes nothing and no edge from the fanout or the authenticator reaches it. Only the lazy recheck through AdmissionGate is drawn. The same gap applies to CredentialRevocation step 8, since no drawn class calls the release method of ArcInputLeaseRegistry on revocation.

2. **P2-02, Material. The Ask-consent pathway has component edges but no class realization.** `design-c4/architecture/workspace.dsl:67` and InvocationFlow steps 5 to 8 require AdmissionGate to issue an exact bounded challenge to the Compose UI and receive the decision. AdmissionGate exposes no challenge port. OwnerAuthenticationService is the only challenge issuer and has no edge from AdmissionGate. ComposeManagementViewModel exposes only status observation and owner-command submission. Whether Ask consent is a protected owner command with the biometric prompt or a UI-returned decision trusted by same-process disclosure is unresolved, and the two have different properties under requirement 5.

3. **P2-03, Material. Two central domain ports have no realization anywhere.** CodecRegistry and CallbackProxyFactory appear only as interfaces. BridgeService composes CodecRegistry and CallbackBroker composes CallbackProxyFactory, so owned lifetime is claimed for abstractions with no concrete class, no project owner and no crosswalk row for an implementer. Exact codec selection by schema hash plus wire proof, and stub construction for interfaces, abstract classes and Binder endpoints, are therefore unspecified at class level.

4. **P2-04, Material. Two component SVGs are not faithful projections of the DSL.** `design-c4/architecture/workspace.dsl:88` gives the adb to android relationship the description "Paired same-device ADB; on-demand screen.state/input.tap". In structurizr-ServerComponents.svg link lnk59 and in structurizr-ExecutionComponents.svg link lnk30 the relationship is drawn with no text at all, while every other relationship in both files carries its label. Either the renders predate the description or the export dropped it. This is the only relationship carrying the requirement-3 on-demand claim.

5. **P2-05, Material. The committed-work path has no component-level counterpart.** server-adapters-classes.svg places OutboxDispatcher in component storage and has it compose CallbackCleanupWorker, JournalNotificationWorker and EpochInvalidationWorker, which drive callbacks, transport and device components. The DSL declares no relationship from storage to any of those components. The DSL's callbacks to transport "Signals committed journal change" is realized in the class view through storage, so the code view does not match the C4 component view for this mechanism.

6. **P2-06, Material. The ARC-target convergence relationship is rendered nowhere.** `design-c4/architecture/workspace.dsl:86` declares invoke to arcController "ArcTargetBackend forwards exact admitted canonical ARC operation". ServerComponents and ExecutionComponents include invoke but not arcController. ArcComponents includes arcController but not invoke. The claim that the ARC target path converges on the canonical dispatcher exists only as DSL text.

7. **P2-07, Minor. The module contract contradicts its own invariant and the host route inventory is incomplete.** module-contract.json sets interfaces_are_domain_ports to true, yet LoopbackHost is an interface assigned to bridge.android in both the classes map and the crosswalk. Separately, the server-core note declares a direct-event SSE route under /v1/subscriptions, while the LoopbackHost routes drawn are MCP, optional pair and disabled admin only.

8. **P2-08, Minor. Cross-module instance creation is unspecified.** ArcSessionManager in bridge.application composes zero-or-more ArcClientPort instances whose only realization, ArcClientAdapter, lives in bridge.protocol. The contract forbids application importing protocol, and no factory port is drawn. The controller-side ArcFrameCodec instance likewise has no owner.

9. **P2-09, Minor. Ownership gaps.** SessionInvalidationFanout and ClientCredentialService have no composition owner in any class view, although the legend states that composition denotes owned lifetime and BridgeService otherwise composes every service.

10. **P2-10, Minor. CAS token nomenclature is inconsistent across layers.** The view model submits with expectedRevision, the owner facade takes etag, the gate takes revisions, the repository takes expectedRevision plus an ownerCapability value, and the verifier takes revisions plus challengeRef. Whether a strong ETag equals a revision, and whether the repository receives the capability value or resolves it from the challenge reference, is unspecified.

11. **P2-11, Minor. Shutdown and resume ordering are incomplete.** Shutdown step 4 says cleanup runs "before backend close", but no step closes Binder or ADB backends or AdbConnectionManager. No dynamic view shows the reopen method of RemoteInputQuiescer, so the trigger that resumes input and frame, tree and log exposure after prompt success, denial or timeout is undrawn.

12. **P2-12, Minor. Component descriptions understate crosswalk responsibilities.** Transport omits pairing, admin, hosting and journal-worker roles. Storage omits OutboxDispatcher. Device omits EpochInvalidationWorker. Invoke claims ADB adapters although adb is a separate component. The UI component claims the registered ComposeTargetAdapter root while the crosswalk assigns that adapter to the arc component.

13. **P2-13, Minor. Image-view scoping and labeling.** ArcUiClasses and ArcControllerClasses are attached to single components but draw classes owned by ui, auth, policy, lifecycle, ownerCommands and sessionCommands, while code-diagrams.md declares only the server blocks as cross-component. The ServerAdapterClasses title omits the UML-class-notation label used by the other five. The legend defines composition and dashed dependency only, not the solid directed associations used in arc-controller and arc-ui.

14. **P2-14, Minor. Wording precision on protocol claims.** ArcSession step 1, the ArcTcpTransport note and the ServerComponents technology label describe the TCP transport as authenticated, but authentication is bearer-in-envelope for ARC and HTTP-layer for MCP. The transport to agent relationship is labeled with the MCP technology although it also covers the non-MCP direct-event route. The McpServer read method takes a separate cursor parameter while the dynamic view says the cursor is expanded into the URI.

15. **P2-15, Minor. Root classification and node staleness are under-specified.** The ComposeTargetAdapter note enumerates only management_readonly and ordinary_unprivileged, yet protected roots must still be classified to be excluded. TargetAdapter has no root registration or selection API. Stale detection through resolveNode with a local generation requires node identifiers never to be reused across generations, which no diagram states.

16. **P2-16, Minor. Duplicate responsibilities without an authority statement.** Catalog publication invalidates DeviceViewResolver both through the observePublications Flow and through the asynchronous EpochInvalidationWorker. Sensitive-exposure suspension is claimed by both AdmissionGate and RemoteInputQuiescer.

17. **P2-17, Minor. Evidence coverage integrity.** The evidence block for code-diagrams.md ends short of the size the coverage index declares, while claiming identical full and body hashes. One of the two numbers is wrong, and a short tail of the crosswalk source may be unreviewed.

18. **Cosmetic observations, no severity assigned.** Structurizr SVG title elements contain leaked size markup. On the widest component SVGs, autoLayout packs up to ten relationship labels on one baseline with single-digit pixel gaps, which reads as a run-on line even though no overlap was detected numerically.

`★ Insight ─────────────────────────────────────`
Composing an interface in UML claims that the owner controls the lifetime of some concrete object. When no realization is drawn anywhere, the diagram is really deferring a creation decision to an undrawn factory or dependency-injection graph, which is exactly where cross-module rules get violated in practice. Findings P2-03 and P2-08 are the same smell at two scales.
`─────────────────────────────────────────────────`

`★ Insight ─────────────────────────────────────`
A transactional outbox and a reactive Flow solve different problems: the outbox gives crash-safe, exactly-once-ish side effects after commit, while a Flow gives low-latency in-process notification. Using both for the same invalidation, as in P2-16, is defensible only if one is declared authoritative and the other idempotent, otherwise consumers see the same epoch twice with different timing.
`─────────────────────────────────────────────────`

## Cross-pass questions and confirmed claims

Questions for the passes that own the referenced evidence:

- **Pass 1, protocol pins.** Verify against the pinned remote_control.proto, BridgeFrameCodec.cs and RemoteControlBridgeRequestHandler.cs that Cancel is enum value 8, that the terminal flag field is end_of_stream, that CommandResult has a succeeded field, that the unsupported status name matches the notes, that a zero-length frame decodes as a default request rather than an error, that version validation precedes bearer validation, and that FrameUpdate carries PNG bytes and a sequence field as the controller state assumes.
- **Pass 1, reproducibility.** Determine from reproducibility-006.json and checksums.sha256 whether ServerComponents and ExecutionComponents were regenerated after the description on `design-c4/architecture/workspace.dsl:88` was added, and whether the SVG hashes supplied to pass 2 match the published checksum file.
- **Pass 1, validation tooling.** Confirm whether validate-modules.cjs enforces the interfaces_are_domain_ports invariant and parses drawn PlantUML edges, as code-diagrams.md claims.
- **Pass 1, design document.** Confirm whether the design document specifies the foreground-service type and boot eligibility rules, the Ask-consent mechanism, concrete providers for CodecRegistry and CallbackProxyFactory, an ArcClientPort factory, the ETag to revision mapping, certificate trust distribution for LoopbackTlsHost, the negotiation vehicle for the direct-event capability, root registration and selection, node identifier non-reuse, and the resume trigger after a protected prompt.
- **Pass 1, evidence integrity.** Resolve the code-diagrams.md byte-count discrepancy.
- **Pass 3, UI projections.** Check that the screens and storyboards for Ask consent, the pending-owner notification whose actions cannot approve, the pause and resume around protected prompts, ARC root selection, pairing and the disabled admin route agree with the dynamic views reconciled here.
- **Consolidation.** The design023 ledger and the forty-seven prior roots are pass-1 evidence not supplied to pass 2, so this pass cannot disposition them. The findings above must be mapped against them by the consolidating pass.

Claims verified as internally consistent within the pass-2 artifacts, with no contrary evidence found:

- **ARC wire claims.** Signed four-byte big-endian length with the stated maximum, envelope version 1.0, transport name arc-protobuf-v1, no separate handshake, one request per socket with repeated watch responses and a terminal end flag, Cancel declared but unsupported with real cancellation by closing the watch socket, OK envelope validated together with command success, FPS parsed but not enforced upstream with local quotas disclosed as port policy, plaintext loopback with no fake TLS, mouse, wheel, key and text only with Avalonia key names.
- **MCP claims.** Single final JSON-RPC result ending the POST stream, exact-URI subscribe with URI-only updates, finite journal pages read through the resource API, transport Last-Event-ID distinct from the application cursor, explicitly negotiated direct-event route with journal fallback, best-effort list_changed without host eviction guarantees, session resolution bound to the authenticated principal, origin validation at the host.
- **Lifecycle and health.** Starting notification before slow work, Running exposed only after the listener is ready, separate MCP, ADB, device-view, catalog and ARC health fields, direct-boot mirror for the boot opt-in, orphaned registrations marked rather than re-registered on restart, explicit stop with no automatic restart.
- **Policy and concurrency.** Canonical admission with dispatch permits that recheck epochs, synchronous epoch publication before barrier release, single serialized SQLite writer with bounded read snapshots, lease-claimed committed obligations with unknown-crash disposition, exclusive input lease spanning unary sockets, atomic CAS activation of staged device views and group views.
- **Structure.** All eight dynamic views use only declared relationships, every crosswalk row agrees with the module contract, and no drawn edge violates the acyclic project graph.

Recap: every assigned diagram was read in full and reconciled; six class SVGs match their sources exactly and nineteen of twenty-one projections are faithful. Six material findings concern undrawn mechanisms for revocation to callbacks, Ask consent, codec and proxy providers, the outbox path, the ARC-target convergence edge, and a stale or lossy render of the on-demand ADB relationship. Eleven minor findings cover ownership, naming, scoping and wording. No score is issued in this pass; the cross-pass questions above must be answered before consolidation.

END_DESIGN024_pass-2