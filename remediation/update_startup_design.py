"""Update editable design and Mermaid sources only; no device/backend actions."""
import pathlib,datetime,json
R=pathlib.Path(__file__).resolve().parent;D=R/'design';p=D/'Android MCP Bridge Design.md';text=p.read_text()
marker='# Startup device-resolved bindings, types and generic Binder execution'
if marker in text:raise ValueError('Preserve existing revision; inspect before another edit')
addition='''
# Startup device-resolved bindings, types and generic Binder execution

At startup the headless service resolves a device-specific view of both bindings and types before admitting device calls. The immutable catalog remains the full inventory: unavailable, unknown, historical and future entries are retained with reasons. A filtered available view is a projection, never a replacement import or a curated catalog. Service reachability, version applicability, verified wire support, OS/backend authority and MCP client grants are separate facts. An available entry never grants invocation.

## Capability capture and version evidence

The capability collector uses only supported metadata operations within an already configured backend's authority. It records platform SDK major/minor, separately observed SDK extension family levels, build/OEM identity and installed module/interface versions where observable. Use SDK_INT_FULL and Build's major/minor accessors on supported releases; SDK_INT alone supplies a major, not an invented minor. A release display string, targetSdkVersion, the host SDK installation and extension level24 cannot stand in for the device's platform37.2. Missing, denied or unsupported observations remain unknown with provenance. No pairing, debugging toggle, privilege escalation or phone operation is performed by this design/reference deliverable. Android's documented full-SDK fields distinguish minor releases; extension availability has a separate runtime query. [Build.VERSION](https://developer.android.com/reference/android/os/Build.VERSION#SDK_INT_FULL), [Build accessors](https://developer.android.com/reference/android/os/Build#getMajorSdkVersion(int)), [SDK Extensions](https://developer.android.com/guide/sdk-extensions).

A DeviceCapabilitySnapshot is bound to the configured connection profile, target, Android user, device/session instance, observation epoch and collection evidence. It retains unknown values instead of converting them to zero or false. Probe failure is not proof of absence. Package visibility, backend access and non-SDK restrictions can limit observations. Record authoritative absence only when the supported observation actually establishes it. Build/OEM strings identify evidence applicability; they do not prove undocumented backports. Do not collect hardware serials or credentials to populate this view.

Where supported by a known adapter and permitted by the existing backend, refine service metadata with endpoint descriptor, reachability and stable AIDL interface version/hash. Use generated/proven metadata operations, bounded timeouts and exact target/profile association; do not guess transaction IDs or probe private/OEM services universally. Stable AIDL explicitly provides version/hash queries, but service discovery or successful metadata calls do not prove authorization to invoke methods. An unrecognized hash leaves the wire contract unknown; an unreachable/denied probe leaves existence uncertain instead of declaring a method removed. [Stable AIDL metadata](https://source.android.com/docs/core/architecture/aidl/stable-aidl#querying-the-interface-version-of-the-remote-object).

## Applicability and coherent type resolution

Each source-backed binding/type variant has a versioned applicability rule and provenance. Introduction is inclusive and removal exclusive; deprecation alone is not removal. Conditions can constrain platform major/minor, a named SDK extension family, module version, exact OEM/build evidence and supported interface version/hash. Alternatives model documented backports; constraints within one alternative must all hold. Unknown conditions cannot become true by default. The source importer must retain lifecycle metadata and historical/future declarations in the full inventory; a current-target SDK projection must not discard the only records needed to resolve an older device. Legacy or unparsed applicability stays unknown until corrected evidence is imported.

The resolver selects a schema variant only when the observed facts prove it and no unresolved, potentially applicable incompatible variant remains. Conflicting variants or unrecognized semantics yield unknown with retained evidence. Availability filtering applies to types, fields/enumerants and callable bindings, not just method names. A binding's required type schemas must resolve in the same snapshot with the expected schema identities. A missing/ambiguous type yields unknown; a version-excluded required type makes the binding unavailable. Mutually recursive types resolve as one coherent graph. Hash canonical finite type graphs/references without recursive expansion, and bind the selected graph digest to dispatch schema/codec identity so a dependency change invalidates approvals even when a method name is unchanged.

The view retains one result per inventory entity with selected schema hash when known, dependencies, state available/unavailable/unknown and machine-readable reasons. Preserve all inventory entries and expose source coverage gaps separately. Unknown/unreachable schemas remain visible, not callable. A known public SDK version may establish documented applicability without proving a private Binder wire layout. No type version or codec is inferred from API level alone.

## Classes and responsibilities

| Component | Responsibility | Boundary |
| --- | --- | --- |
| IDeviceCapabilityCollector / DeviceCapabilitySnapshot | Bounded supported version/module/interface observations and provenance, scoped to one profile/target/epoch | No permission grants, pairing or arbitrary command execution |
| IVersionApplicabilityResolver | Three-state evidence evaluation across platform, extension, module, OEM/build and interface facts | No minor/extension conflation or undocumented backport guesses |
| IResolvedTypeGraphBuilder | Select compatible type variants, check required dependencies and schema hashes, handle recursive graphs | No fabricated missing types, wire tags or layouts |
| ResolvedDeviceView / IDeviceViewStore | Complete immutable result set, atomic activation, generation/epoch CAS and stale-view rejection | No row-by-row partially visible startup state |
| ICanonicalDispatchPlanner | Resolve generated/generic/known-raw calls to the same binding, typed arguments, target and selected graph/schema hashes | No alternate policy identity for an alias |
| IWireContractRegistry / IParcelCodec | Proven endpoint descriptor/version/hash, method code/flags and bounded request/response, handle and type codecs | Declaration order never proves a Binder transaction code |
| IGenericBinderTransactionEngine / IBinderTransport | Execute a verified typed transaction through the selected backend, observe deadlines/death/outcome and decode bounded output | No policy bypass, universal private introspection or automatic replay |
| IClientPolicyEvaluator / IAdmissionGate | Intersect per-client grant, wrapper restrictions, selected view, live backend/OS authority and approval/revision checks | Version availability never changes Deny/Ask/Allow |
| Avalonia view models | Full inventory plus availability/reason filters, resolving/stale indicators and owner remediation | No direct SQL, implicit approvals or app-specific decisions |

![Startup capability capture and atomic device view](assets/startup-resolution.svg)

## Generic Binder invocation and codecs

Generated tools and binding.invoke use the same canonical dispatch planner and binding.invoke policy identity. They select a binding/type graph from the active resolved view, then require a verified wire contract before marshalling. The wire registry pins endpoint/descriptor, relevant interface/OEM version/hash evidence, actual transaction code, flags/one-way semantics, request/response codec schema and handle ownership constraints. Support generated proxies or schema-driven codecs only when the complete contract is proven. Source AIDL alone does not establish generated transaction codes, runtime class resolution, parcel layout or backend authority.

The generic engine validates typed arguments and exact target constraints, bounds parcel sizes, nesting, arrays, file/handle lifetimes and output, then passes the immutable plan through policy/Ask/admission. Admission rechecks catalog, resolved-view and capability epochs alongside policy and backend identity. Marshal only the admitted plan; transact through IBinderTransport; decode and validate output against the pinned graph before release. Binder death, cancellation after dispatch and uncertain outcomes never trigger automatic write replay. These are architecture responsibilities and reference contracts, not a deployed Binder engine in this phase.

Known raw transactions must decode to the same binding/schema and satisfy binding.invoke plus restrictive binder.transact rules. When schema/codec proof is missing, typed invocation stays unsupported; an explicitly authorized raw low-level operation remains a separate broad service authority with closed connection/descriptor/code/flag/handle bounds. A raw grant cannot substitute for a known binding grant. Android's actual authority still decides whether the transaction can execute. No target application must embed instrumentation for generic ADB screen/tap control; optional Avalonia Android Runtime/Protocol stays a separate adapter, not desktop Server/Kestrel.

![Resolved view and policy-bound generic Binder dispatch](assets/device-binding-sequence.svg)

## Atomic activation, invalidation and refresh

Capture facts under a capability epoch; build the entire binding/type result set privately against pinned catalog and overlay generations. Validate complete inventory membership, variant identities and type dependencies before sealing. Activate one immutable snapshot pointer in a single transaction only if catalog, profile/target/device identity, capability epoch and expected view generation still match. A failed build/CAS retains the previous snapshot as historical evidence; it never exposes a partial mixture. Snapshot IDs/ETags and pagination cursors bind the complete view and generation. An available row cannot depend on an unavailable, unknown or mismatched required type in that same view.

Startup, reconnect/device replacement, boot/session or Android-user change, OS/build/OEM update, SDK extension/Mainline/module change, supported interface version/hash or reachability change, backend capability change and catalog/type/overlay publication invalidate affected active views and pending approvals. Mark the view stale immediately; no new device call may use it. Keep the full catalog accessible and distinguish Resolving/Stale from MCP listener health and ADB connection health. Refresh from supported backend events, startup/reconnect and explicit owner requests; this introduces no app-specific monitoring scheduler. Where a platform cannot notify a change, revalidate the relevant observations at call admission rather than treating cached availability as timeless authority. Existing live permission/policy checks remain mandatory and separate.

The executable device resolver/store is an offline reference using fixture observations, with no Android/HTTP service or real probes. The API accepts refresh requests for configured targets but never accepts a remote caller's asserted facts or availability as authority. The existing immutable import/local override/CAS contract remains in force; user edits cannot manufacture observed device evidence. Namespace/package file re-sharding remains a proposal.

## Requirement and acceptance traceability

| ID | Requirement and acceptance | Evidence |
| --- | --- | --- |
| FR-DEV-01 / AC-DEV-01 | Startup resolves bindings and types for the running platform major/minor; unavailable/unknown entities remain in full inventory | DeviceResolution schema and version/minor/removal fixtures |
| FR-DEV-02 / AC-DEV-02 | Extension/module/OEM/interface facts refine availability without API-level guesses or permission conflation | Collector contract; extension alternatives, module/OEM, interface hash/denial fixtures |
| TR-DEV-01 / AC-DEV-03 | One coherent type graph; missing/ambiguous/removed required types cannot yield an available binding; recursive types supported | Resolver dependency, schema mismatch, ambiguity and cycle fixtures |
| TR-DEV-02 / AC-DEV-04 | Atomic complete activation and generation/epoch CAS; no partial view; stale/cross-target snapshots reject | Reference store, executable SQLite constraints and invalidation fixtures |
| FR-BIND-01 / AC-BIND-01 | Generic Binder engine uses proven wire/codec contracts and the shared canonical policy plan; unknown wire stays unsupported | Existing policy/known-raw fixtures plus wire-contract planning specification |
| TR-DEV-03 / AC-DEV-05 | Relevant capability/catalog changes invalidate and refresh the view; availability never grants OS or client authority | Event/invalidation contract and permission-separation fixtures |

Retain every earlier requirement: Avalonia12, SQLite configuration/client mapping and logging, persistent headless MCP, optional boot and truthful AppBar/status, separate ADB health, paired same-device ADB and host loopback forwarding, on-demand screen/tap, optional Android RemoteControl, all-source inventory/provenance, CRUD/local overrides and future compatibility. No deployment, device/security change or source-corpus completeness claim is made by these additions. Fresh hostile validation must assess this revised requirement; round009 predates it.

'''
anchor='# 19 Acceptance tests and delivery sequence'
if anchor not in text:raise ValueError('Design insertion anchor missing')
text=text.replace(anchor,addition+'\n'+anchor)
intro='The bridge is a comprehensive binding gateway and a security boundary.'
text=text.replace(intro,'At startup, resolve the running device\'s version-compatible bindings and types into an atomic view, retaining the complete inventory with unavailable/unknown reasons. Generic Binder invocation requires verified wire contracts and codecs plus the shared policy/admission gate; availability never grants authority.\n\n'+intro,1)
p.write_text(text)
(D/'sources/startup-resolution.mmd').write_text('''flowchart TD
    Start[Headless service startup or capability change] --> Capture[Capability snapshot with profile target and epoch]
    Catalog[Full immutable bindings and types inventory] --> Resolve[Evidence based version applicability]
    Capture --> Resolve
    Resolve --> Types[Coherent versioned type graph]
    Types --> Stage[Stage complete available unavailable unknown view]
    Stage --> CAS[Recheck catalog identity and capability epoch]
    CAS --> Active[Atomically activate sealed view]
    Change[Backend device module or interface change] --> Stale[Invalidate view and approvals]
    Stale --> Capture
    Active --> UI[Avalonia full inventory and reason filters]
    Active --> Plan[Canonical dispatch planner]
    Plan --> Policy[Separate client policy and live OS authority]
''')
(D/'sources/device-binding-sequence.mmd').write_text('''sequenceDiagram
    participant C as MCP client
    participant V as Resolved device view
    participant P as Canonical planner
    participant G as Policy and admission
    participant W as Wire registry and codecs
    participant B as Binder transport
    C->>P: Generated generic or known raw request
    P->>V: Pin current view and type graph
    V-->>P: Variant schema and capability epoch
    P->>W: Require verified descriptor code flags and codecs
    W-->>P: Bounded typed transaction plan
    P->>G: Same canonical binding policy identity
    G->>G: Check grants Ask live authority and revisions
    G->>V: Recheck view and capability epoch
    G->>W: Marshal admitted immutable plan
    W->>B: Verified transaction only
    B-->>W: Result or unknown outcome
    W->>W: Decode validate and bound output
    W-->>C: Permitted result
    Note over V,B: Capability changes invalidate admission no automatic replay
''')
print(json.dumps(dict(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),markdown=str(p),new_sections=marker,mermaid=['sources/startup-resolution.mmd','sources/device-binding-sequence.mmd'],no_device_operations=True)))
