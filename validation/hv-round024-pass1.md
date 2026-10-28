Pass-1 evidence read is complete: every assigned file was inspected in full, the ARC and contract claims were re-verified against the supplied bytes, and the prior 47 roots were re-checked. This pass records new and residual findings plus cross-pass questions and gives no overall score.

## Coverage and verified baseline

**Files read in full.** All pass-1 assignments were read from the supplied exact evidence, and the body hashes equal the full-file hashes for every entry.

| Group | Files |
| --- | --- |
| Pinned ARC upstream sources | 11 |
| Catalog contracts, schemas and OpenAPI | 18 |
| Architecture sources, receipts, build and tooling | 33 |
| Design document, protocol contract, checksums, dispositions | 4 |
| Evidence ledgers, prior review and test files | 11 |

**ARC claims verified line by line.** Each design statement below was checked against the pinned C# and proto bytes.

| Design claim | Upstream evidence | Result |
| --- | --- | --- |
| Version mismatch returns status 5 with the exact message, before bearer checks | Handler version branches in both HandleAsync and HandleResponsesAsync | Verified |
| Zero-length frame decodes to a default envelope and fails version validation | Codec reads an empty payload and parses it; handler rejects the empty version | Verified |
| Negative, oversize and sub-1 maxima rejected before allocation | Codec validates length before allocating the payload buffer | Verified |
| Complete-frame decoder rejects trailing bytes; stream reader consumes exact frames | Decode length-equality check; ReadAsync reads exact prefix and body | Verified |
| Empty request_id is echoed, never validated | Handler never inspects request_id | Verified |
| Cancel 8, method 0 and unknown methods return status 5 | Switch default branch | Verified |
| Watch methods sent to the unary path return status 5 | HandleAsync streaming branch | Verified |
| Parse failure gives status 2, cancellation gives 6, status 4 is never emitted upstream | Catch blocks and ToBridgeStatus mapping | Verified |
| One request per socket, stream items false, terminal true with empty payload | Listener reads one request; handler Success, StreamItem, Failure | Verified |
| Client checks id, then version, then status, then the terminal flag | Client ValidateResponse ordering | Verified |
| Client cancels by token, which disposes the socket | Client scoped TcpClient disposal | Verified |
| Maximum frame of 8388608 bytes | Codec default constant | Verified |
| Bearer prefix case-insensitive, fixed-time compare, "Authentication is required." | Authenticator | Verified |
| FPS field parsed and discarded | Handler parses WatchFramesRequest without use | Verified |
| Singleton dispatcher keeps button state across unary requests | DI registration at line 61; dispatcher fields | Verified |
| Any release clears left-button state; unknown key becomes Key.None and is still raised | Dispatcher release and ParseKey branches | Verified |
| Text events raise TextInputEventArgs with the supplied text | Dispatcher Text branch | Verified |
| Keepalive category, 15 s default, nonpositive disables, zero sequence and dropped count | Protocol constants, options, CreateKeepAlive | Verified |
| Frame caps of 10 fps and 4,000,000 pixels equal upstream defaults | Options FrameStreamInterval and MaxFramePixelCount | Verified |
| RemoteControlProtocol.cs line 31 defines the transport marker | Line count | Verified |
| All message, field, enum numbers in arc-protocol-contract.json and arc-upstream-facts.json | Proto | Verified |
| Press of a right or middle button also clears left-pressed state | Dispatcher PointerPress assignment | True upstream, absent from the design's condition matrix |
| Wire events carry no modifiers; upstream passes KeyModifiers.None always | Dispatcher constructors | True upstream, design claims "modifier state" mapping, see P1-31 |

**Other baseline checks that passed.** The Structurizr dynamic views each step over a declared relationship, which matters because implied relationships are disabled. The module graph in the receipts matches the class views and is acyclic under the contract's rules. The design's route table matches OpenAPI, the dv2, cat1 and meta1 token formats agree across the contracts, and the X-Owner-Challenge parameter is present on every mutation. The toolchain, renderer and wrapper pins agree across the lock files, the build script and the properties file. The checksum manifest agrees with the coverage index for every file I compared. Receipt arithmetic is consistent: 221 vector files, 108 slides, 39 design tests split 28 and 11. Hash values and test executions are receipt claims; I recomputed nothing.

`★ Insight ─────────────────────────────────────`
- The one wire-level accuracy defect in round 023 came from a correction written against reviewer prose instead of the pinned handler. This pass therefore re-derived every status code from the catch blocks and switch branches rather than from the design's own matrix.
- Upstream facts that are true but unmentioned, such as a right-button press clearing left-pressed state, matter for golden fixtures. A port that only tests the listed matrix will pass while diverging on the unlisted case.
`─────────────────────────────────────────────────`

## Findings

**New and residual defects in pass-1 artifacts.** Severities are per finding only; synthesis owns the score.

| ID | Severity | Req | Exact evidence | Defect | Minimal correction |
| --- | --- | --- | --- | --- | --- |
| P1-01 | Low | R12 | AC-UI-02 says "all 62 slides are present"; ui-projection.json records 107 steps; the browser receipt expects 108 slides | Stale acceptance count in the design | Cite the generated count or the receipt |
| P1-02 | Medium-low | R12 | Section 9 table is "generated from docs/ui-asset-manifest.json" and lists 74 action labels; ui-projection.json records 88 controls; controls the design adds for round 023, such as SCR01 refresh, SCR03.C10 admin window, SCR03 activate import, SCR04 member editor, SCR07 profile setup and SCR10.C06 retry, are absent | A table claimed as generated does not match its source and no task regenerates it | Generate it in the build or drop the claim and sync; pass 3 to confirm IDs |
| P1-03 | Low | R10 | Prose names CallbackBroker.onCalleeDied; the class view member is onRegistrationEndpointDied; prose also names queryRedactedLogs and exportLogs members the views lack | Member names diverge from the authoritative view | Use the view's names in prose |
| P1-04 | Medium-low | R8, R12 | Alias rule is `b.<first32hex>.<first24hex>`; the tools/call example uses the name contacts.list and the text calls it "an explicitly negotiated active dynamic alias"; alias_name is free text in SQLite | Two naming rules; wrapper names in dynamic mode are undefined | State that reviewed wrappers keep human names with collision rules, or change the example |
| P1-05 | Medium-low | R1, R8 | Sections 8 and 12 present device.battery.read as a real convenience tool; native-identities.json, called normative, has eight records without it; only partner.status.read is labeled illustrative | A convenience operation has no canonical identity, so portable clients cannot address it | Add the record or label it illustrative |
| P1-06 | Low | R3 | screen.state fields are captureId, capturedAt, coordinateSpaceVersion in two sections and capture_id, captured_at, coordinate_version in the image-content paragraph; ScreenStateResult@1 has no schema | One tool with two spellings and no pinned schema | One spelling plus the result schema |
| P1-07 | Medium-low | R5 | OpenAPI suppressEntry DELETE has no request body; API-CONTRACT says the DELETE reason travels in the JSON body and a missing reason returns 422; the design says every owner CRUD mutation persists its reason | Entry suppression over HTTP cannot carry a reason, so contract and design contradict OpenAPI | Add a body to entries DELETE or scope the rule to metadata resources |
| P1-08 | Low | R5 | OpenAPI 201 responses for replaceCallbackDescriptor and replaceFunctionalGroup describe the ETag as "Current strong cat1 ETag"; the 200 responses say meta1 | Wrong token family on creation | Describe 201 as meta1 |
| P1-09 | Low | R5 | Item GETs for descriptors, groups, both histories and source-metadata declare 409, 412, 422 and 428 with no precondition parameter; history GETs are "protected owner only" yet tagged catalog:read | A18 residual plus a capability tag that contradicts prose | Trim the codes; tag owner-only routes |
| P1-10 | Low | R5, R12 | The design makes SOURCE-METADATA-CONTRACT.md normative for the meta-json-v1 profile; the pinned API-CONTRACT copy links to it; source-pins.json pins only two files; design-c4/contracts lacks it; verifyDesignDocs scans docs only | The portable package has a broken normative link and an unpinned contract | Pin and copy it, or inline the profile |
| P1-11 | Low | R10 | LoopbackHost is an interface owned by bridge.android; the preamble says interface ports live in bridge.domain; module-contract sets interfaces_are_domain_ports true | Stated rule violated by one interface | Move it to domain or restate the rule |
| P1-12 | Medium-low | R2, R7, R10 | DSL edges sessionCommands to lifecycle "local stop/status", ownerCommands to lifecycle "reads health", auth to callbacks "close delivery; recorded safe cleanup", auth to arc "release principal input lease"; in the views the facades sit in application and cannot import BridgeService, no lifecycle port exists, CallbackBroker is not a PrincipalSessionSink and has no revocation member, and ArcInputLeaseRegistry has no path from the fanout | Component edges with no class mechanism under the DAG; "immediately ... queues recorded cleanup" on revoke is lazy in the model | Add a domain lifecycle-command port, a broker sink or onPrincipalRevoked, and a lease-release sink; show them in CredentialRevocation |
| P1-13 | Medium-low | R2, R4 | Text: beginDispatch applies to every ARC plan and the quiescer drains dispatched input permits; views: inbound ARC runs ArcRuntime to ArcAuthorizationGate.admitTargetOperation to AdmissionGate with no permit members | Inbound ARC input cannot be drained or counted in flight | Route inbound ARC actions through beginDispatch and endDispatch |
| P1-14 | Medium-low | R2, R5 | "drains already dispatched input permits" and "unclassified side-effecting raw/ADB requests are denied" versus Paused "suspended admission during an owner challenge" and "Previously admitted but undispatched plans fail beginDispatch"; Paused also means migration pause | Barrier scope stated two ways; no schema field or rule classifies a catalog binding as input or side-effecting; Paused is doubly defined | Pick one scope; if input-only, define the classification source; split the Paused values |
| P1-15 | Medium-low | R2, R3 | MANAGEMENT_ROOT_INPUT_DENIED applies to input "aimed at this bridge's management package"; the two-observation rule exists only for target-scoped taps; whole-display taps have "best-effort guards" | No check is defined for the one scope where aim is unknown | Require a foreground observation at beginDispatch for whole-display taps or state the residual |
| P1-16 | Low | R5, R6 | "Epoch types are" lists seven types; the family table has ten rows | Two enumerations | Reference the table only |
| P1-17 | Very low | R10 | Image view ServerAdapterClasses title lacks "(UML class notation)"; root identity terms remain rootEpoch, generation, remoteRootGeneration and rootPins | A16 residuals | Add the suffix; one term |
| P1-18 | Low | R4 | Design states marker fields, the Directory.Build.props MIT metadata, and that click, focus and property denial return OK with succeeded false | The marker class, build props, action invoker and property service are not in the supplied pinned set; only the input dispatcher denial path is in evidence | Mark these unverified in the fixture plan or supply the files |
| P1-19 | Low | R4 | "missing protocol is handled only by the documented legacy gRPC selection policy" | That policy is documented nowhere | Define it or remove the reference |
| P1-20 | Low | R9 | "the rechecked pinned tools page ... explicitly lists out-of-range/input-validation errors as tool execution errors" | No excerpt is supplied; earlier published wording listed invalid arguments under protocol errors | Quote the pinned text or label the split a design choice |
| P1-21 | Low | R7 | Contract: candidate roles need a reason, proven roles and cleanup need evidence; schema allows null unknown_reason for candidate, empty evidence arrays, and cleanup proven without evidence | Contract rules not enforced by schema; the semantic validator is unnamed | Add conditionals or name the validator |
| P1-22 | Low | R5, R7 | lifecycle_units, prebuilt_adapters, cancellations, host_forward_profiles and entry_history_reasons appear as prose fragments; an "exhaustive versioned code registry" is asserted | These lack the key and constraint rows other tables have; no registry artifact exists | Add rows and a registry artifact |
| P1-23 | Low | R4, R5 | "A token is newly generated for each bind"; reserved principal local-owner-controller "created only by the unexported local Activity binding"; "ARC sockets16 per service" | Effect of per-bind tokens on epochs and approvals, storage of the reserved principal, and whether the cap covers outbound sockets are unstated | State them |
| P1-24 | Low | R6, R10 | Vocabulary crosswalk: DeviceViewResolver owns the IDeviceCapabilityCollector strategy; views: BridgeService composes DeviceCapabilityCollector, which depends on the resolver | Two ownership statements | Align the crosswalk |
| P1-25 | Low | R2 | ClientCredentialService stores verifiers through CredentialVault; "Only its repositories open the writable configuration database"; KeystoreCredentialVault has no coordinator or repository dependency | Credential writes bypass the modeled single writer | Route verifier writes through the repository |
| P1-26 | Low | R11, R12 | packageDesignDocs copies build/receipts into validation/; those receipts hold absolute host paths; README says reviewer receipts are omitted; bootstrap populates .tools, not the wrapper dists cache that the offline invocation needs | Package hygiene and bootstrap completeness gaps | Exclude or sanitize receipts; document the wrapper cache step |
| P1-27 | Low | R2 | ComposeManagementViewModel depends only on OwnerCommandFacade; start, stop and status are SessionCommandFacade commands | Dashboard command path undrawn | Add the dependency |
| P1-28 | Low | R2, R5 | The pair route waits up to 30 s for an owner prompt; API-CONTRACT says HTTP does not await or launch a biometric prompt | Two owner-prompt models on one listener with no stated rationale | State the exemption and its quiescence handling |
| P1-29 | Very low | R4 | "Avalonia target mode" row in the integration evidence table | Legacy naming for external ARC targets | Rename |
| P1-30 | Low | R12 | Requirement 12 says the historical DOCX is retained; no DOCX appears in the coverage index or package | Retention unverifiable from supplied evidence | State where it is retained |
| P1-31 | Low | R4 | "The complete mapping covers ... modifier state"; RemoteInputEvent has no modifier field; upstream passes KeyModifiers.None on every event | Modifier source undefined for the port | State that modifiers are tracked locally from key events or dropped |

**Design consequence, not a defect.** A 60 s approval lifetime, a 30 s sequential owner prompt and a 16-deep Ask queue mean most queued Asks beyond the first expire; the design states denial on expiry but not this effective depth.

`★ Insight ─────────────────────────────────────`
- Once a module DAG forbids application code from importing Android classes, every C4 edge that points "downward" needs either a domain port or a reverse Flow. P1-12 and P1-13 are places where the DSL kept an edge that the class model can no longer realize.
- The sink pattern already solves this for sessions: SessionInvalidationFanout holds PrincipalSessionSink adapters. Adding CallbackBroker and the lease registry as sinks closes the revoke gaps without new imports.
`─────────────────────────────────────────────────`

**Status of the 47 prior roots in pass-1 artifacts.**

| Roots | Status |
| --- | --- |
| A3, A5, A8, A9, A10, A11, A14, A15, A17, A19, C4, C9, C10, C12, C13, C14, C15, C23, C24, C26 | Corrected in the supplied sources with no pass-1 residual |
| A1 | Corrected; residual P1-13 |
| A2 | Corrected; residuals P1-11, P1-12 |
| A4 | Corrected in text and DSL; screen-side confirmation belongs to pass 3 |
| A6 | Corrected; residual P1-16 |
| A7 | Corrected; residuals P1-12, P1-27 |
| A12 | Corrected; unverifiable denial claim noted in P1-18 |
| A16 | Corrected; residuals P1-03, P1-17 |
| A18 | Corrected; residuals P1-07, P1-08, P1-09 |
| A20 | Corrected in text; manifest equality is a pass-3 check |
| C3 | Corrected; residual P1-12 |
| C11 | Corrected; residual P1-05 |
| C25 | Corrected; residual P1-06 |
| A13, C1, C2, C5, C6, C7, C8, C16, C17, C18, C19, C20, C21, C22, C27 | Owned by pass 3; the design-side text and build guards they cite are present, and P1-02 bears on C5, C18, C19, C20 and C21 |

## Cross-pass questions, uncertainties and limits

**Questions for pass 3.**
- Confirm the current control count and IDs so P1-02 can be charged exactly; the receipt says 88, the design table lists 74.
- Thirteen storyboard steps embed one byte-identical SCR09.S0 panel, hash 65cf5a17. Does any storyboard depict the exact challenge each flow claims to inspect?
- Which state, if any, renders service Paused, and which canonical adb_backend value the key-rejected ADB state uses.
- Confirm 108 slides against the design's claim of 62.
- Confirm the manifest equals the blueprint JSON block, as the test claims, and that the Marp source references the generated steps.

**Questions for pass 2.**
- Do the Context and Containers SVGs show the documentation system, given it has no relationship to the bridge and implied relationships are off?
- Does the rendered ServerAdapterClasses view carry the missing suffix noted in P1-17?
- Do all eight dynamic views render as sequence diagrams, and is any edge beyond the 64-unit padding?

**Uncertainties I did not resolve.**
- No hash was recomputed; native identity hashes, the checksum manifest and the wrapper sha are receipt and index claims that agree internally.
- No Gradle, Docker or test run was reproduced.
- Android claims about Android 15 boot-start restrictions, registered-only delivery of the unlock broadcast, the full SDK version fields, the wireless-debugging service types and the biometric authenticator flags match my recollection and were not re-browsed.
- MCP claims about 400, 404 and 405 responses, the 2025-03-26 default and single-stream notification delivery match my recollection and were not re-browsed; the tools error wording in P1-20 is the one item where my recollection differs.
- The ARC marker class, build properties, action invoker, property mutation service and runtime are absent from the supplied set, so P1-18 is an evidence gap rather than a contradiction.

**Scope limits.** Google surface coverage, extractor correctness and dataset population were not assessed. Plans, test contracts and receipts were treated as claims; none were taken as proof of implementation.

END_DESIGN024_pass-1