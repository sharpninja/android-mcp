"""Align C4 summaries/deployment and request/outcome flows with the written contract."""
import pathlib,json,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';p=P/'architecture/workspace.dsl';s=p.read_text()
s=s.replace('    !identifiers hierarchical','    !identifiers hierarchical\n    !impliedRelationships false')
s=s.replace('Complete binding inventory with separate authority and contextual discovery','Full versioned inventory, including unknown/unavailable entries; separate authority and contextual discovery')
s=s.replace('One UID and initially one process; Kotlin service owns authoritative state','One UID and one process in this reference design; Kotlin service owns authoritative state')
s=s.replace('Catalog snapshots, policy, groups, views, event journal and audit','Catalog snapshots, client/principal mappings, grants/logging configuration, groups/views, journal and audit; secret keys remain in Keystore')
s=s.replace('"Python / Gradle docs"','"Python"')
anchor='            tooling = container "Offline catalog tooling" "Source acquisition/extraction/schema validation and reference fixtures; no phone operations" "Python"'
if s.count(anchor)!=1:raise ValueError('Tooling model anchor changed')
s=s.replace(anchor,anchor+'\n            documentation = container "Documentation build" "Local model validation and Markdown/SVG/storyboard packaging; no deployed app" "Gradle / Structurizr / Marp"')
anchor='        owner -> bridge.app.ui "Manages local settings and reviews protected approvals"'
summary='''        owner -> bridge "Manages authorized local bridge settings"
        agent -> bridge "Discovers and invokes authorized Android capabilities" "MCP2025-11-25"
        arcClient -> bridge "Controls approved selected root" "ARC transport arc-protobuf-v1; envelope1.0"
        bridge -> arcTarget "Controls explicitly selected compatible target" "ARC raw loopback TCP; bearer authentication"
        bridge -> android "Uses actual SDK/Binder/paired ADB authority; no universal access"
        sources -> bridge "Supplies pinned offline inventory/protocol inputs"
        owner -> bridge.app "Uses local Compose management and protected approvals"
        agent -> bridge.app "Bounded discovery/invocation/resource reads" "Authenticated loopback MCP Streamable HTTP2025-11-25"
        arcClient -> bridge.app "One request/socket or bounded watch" "arc-protobuf-v1; envelope1.0; authenticated raw loopback TCP"
        bridge.app -> arcTarget "First request/response validates pinned profile; no handshake" "Same retained ARC protocol"
        bridge.app -> android "SDK/Binder and separately paired ADB calls, probes and death signals"
        bridge.app -> bridge.db "Reads/imports catalog and atomically persists config, views, journal and audit" "App-private SQLite"
        bridge.tooling -> bridge.app "Produces portable catalog release; owner selects SAF document; importer verifies hashes/schema and stages atomic activation" "Owner-mediated file import, not network deployment"
        bridge.documentation -> owner "Produces editable Markdown, SVGs, local HTML and portable ZIP" "Local files only"
'''
s=s.replace(anchor,summary+anchor)
s=s.replace('bridge.app.transport -> bridge.app.policy "Submits exact admitted operation plan"','bridge.app.transport -> bridge.app.policy "Submits exact proposed plan for canonical admission"')
s=s.replace('bridge.app.policy -> bridge.app.audit "Records admission and bounded outcome"','bridge.app.policy -> bridge.app.audit "Records canonical admission decision"\n        bridge.app.invoke -> bridge.app.audit "Records dispatch and bounded outcome, including unknown effect"')
s=s.replace('bridge.app.device -> bridge.app.catalog "Streams full inventory and persists atomic resolved view"','bridge.app.device -> bridge.app.catalog "Reads streamed inventory; stages and atomically activates resolved view"')
s=s.replace('bridge.app.groups -> bridge.app.policy "Reports grants and rechecks activation epochs without granting authority"','bridge.app.groups -> bridge.app.policy "Queries current grant status and rechecks activation; never grants authority"\n        bridge.app.groups -> bridge.app.device "Pins compatible membership and coherent required type closure"')
s=s.replace('bridge.app.callbacks -> bridge.app.transport "Signals URI-only resource update and bounded reads"','bridge.app.callbacks -> bridge.app.transport "Signals committed journal change for coalesced URI-only hints"\n        bridge.app.transport -> bridge.app.callbacks "Reads owned bounded status/pages and manages exact-URI subscriptions"')
s=s.replace('bridge.app.arcController -> arcTarget "Connects and validates exact retained ARC profile"','bridge.app.arcController -> arcTarget "Validates pinned version/id/status on first request/response; no handshake" "arc-protobuf-v1 raw loopback TCP; envelope1.0"')
start=s.index('        deploymentEnvironment "ReferenceAndroid" {');end=s.index('\n    }\n    views {',start)
s=s[:start]+'''        deploymentEnvironment "ReferenceAndroid" {
            deploymentNode "Android phone" "Reference deployment; not installed by this task" "Android15+ (API35+ reference floor)" {
                deploymentNode "One app UID sandbox" "Single reference Kotlin application process" "Android app process" {
                    runtime = containerInstance bridge.app
                    containerInstance bridge.db
                }
                adbd = infrastructureNode "Paired adbd" "Wireless debugging identity and endpoint are owner-paired; actual OS limits apply" "ADB authenticated connect channel"
            }
            deploymentNode "Owner-operated remote host" "Logical client protocol lines use an explicit owner-established loopback forward" "Host loopback" {
                softwareSystemInstance agent
                softwareSystemInstance arcClient
                forwarder = infrastructureNode "ADB loopback forwarding" "adb forward --no-rebind tcp:0 to the selected phone loopback listener; allocated host port is recorded" "Owner-managed ADB transport"
            }
            deploymentNode "Build workstation" "Local offline tools and renderer; no automatic phone deployment" "Existing pinned toolchain" {
                containerInstance bridge.tooling
                containerInstance bridge.documentation
            }
            runtime -> adbd "Same-device paired ADB connection; separate backend health and authority"
            forwarder -> runtime "Forward only selected device loopback MCP/ARC listener; ARC plaintext is not made TLS by ADB"
        }''' + s[end:]
s=s.replace('bridge.app.groups bridge.app.invoke bridge.app.callbacks bridge.app.audit','bridge.app.groups bridge.app.invoke bridge.app.adb bridge.app.callbacks bridge.app.audit')
s=s.replace('            bridge.app.transport -> bridge.app.policy "Submit exact schema and scope plan"','''            bridge.app.transport -> bridge.app.invoke "Plan exact binding, arguments and backend scope"
            bridge.app.transport -> bridge.app.policy "Check per-client grant, Ask consent, OS/backend authority and current epochs"''')
s=s.replace('            bridge.app.policy -> bridge.app.audit "Record bounded outcome"','''            bridge.app.invoke -> bridge.app.audit "Record admitted dispatch and observed/unknown outcome"
            bridge.app.invoke -> bridge.app.transport "Return bounded invocation result or structured denial/unknown outcome"
            bridge.app.transport -> agent "Return single final JSON-RPC result/error; POST SSE normally terminates"''')
s=s.replace('            bridge.app.groups -> bridge.app.policy "Report permission status; no grant change"','''            bridge.app.groups -> bridge.app.policy "Query per-client permission status; discovery grants nothing"
            bridge.app.transport -> agent "Return ranked bounded summaries and discovery cursor"
            agent -> bridge.app.transport "Request activation with selected IDs, pins and current group-view ETag"
            bridge.app.groups -> bridge.app.device "Pin availability and required type/dependency closure"''')
s=s.replace('            bridge.app.transport -> bridge.app.groups "CAS activate chosen coherent set or return conflict"','''            bridge.app.transport -> bridge.app.groups "CAS activate coherent set or return stale/conflict"
            bridge.app.transport -> agent "Return committed group epoch/hash; portable generic-invoke fallback remains"
            bridge.app.transport -> agent "For opted-in dynamic aliases, best-effort tools/list_changed; host context eviction not guaranteed"''')
p.write_text(s)
p=P/'docs/Android MCP Bridge Design.md';s=p.read_text();marker='### Unified diagram call paths and lifetimes'
if marker not in s:
 s+='''\n\n### Unified diagram call paths and lifetimes

The C4 and code-level views use one call path: McpServer asks InvocationPlanner
for a bounded exact plan, AdmissionGate returns an Admission bound to that plan
and current pins, and McpServer calls dispatch(admission). The gate is a canonical
decision service, not a second invocation orchestrator. InvocationPlanner records
actual dispatch/outcome with AuditLogger; an admission decision alone never proves
an effect. McpServer's resource adapter serves owned finite journal reads and
exact-URI subscriptions separately from the finite registration tools/call.

OwnerCommandFacade handles protected changes through the service-owned
OwnerAuthenticationService, using the already specified nonce, exact command,
revision, current foreground activity and single-use30second BiometricPrompt
result. SessionCommandFacade is the bounded unexported route for ordinary session
commands and status. It derives the authenticated principal/context on the service
side and repeats canonical policy; neither an in-process UI reference nor a JSON
principal grants owner authority. Commands that reveal credentials, change grants
or otherwise cross a protected boundary still require OwnerCommandFacade. The
remote controller never receives a capability to approve its own management grant.

BridgeService owns one transport/session registry, policy/authentication services,
repositories, planner, callback broker, groups index and audit sink. ARC has one
registry containing zero or more ArcConnectionSessions. Each connection owns its
bounded input state and frame/log streams; closing it releases its pressed state.
UiDispatcher and selected-root adapters are service-scoped. Cross-view class
stubs name the authoritative view rather than declaring conflicting alternative
APIs; member lists are selected responsibilities, with inherited interface
operations retained. The sender endpoint/token mapping, lease/consent bounds,
death handling and current epochs from the callback chapter remain normative.

The ARC server does not receive a cancellation command for another watch socket.
An incoming Cancel8 gets the retained UNSUPPORTED result; the compatible client
cancels a watch by closing that watch socket. The server closes on peer disconnect,
revocation or bounded shutdown. A terminal error frame is best-effort within its
write deadline; a blocked peer is closed without waiting indefinitely. ARC local
generation/epoch metadata is not silently added to the pinned protobuf wire.

The separate AdbPhoneBackend/AdbConnectionManager owns paired-key protection,
verified endpoint discovery, authenticated ADB connections, reconnect/unsupported
reasons and independent backend health. ArcEngine does not own generic phone
control. BinderHandleProvider selects only a verified supported access profile;
WireContractVerifier binds exact source/generated codec proof to current backend
and interface observations. A descriptor name, API level or caller-provided
`verified` flag is insufficient; unsupported acquisition or codec remains explicit.

Container relationships are explicit summaries rather than the first implied
component edge: the APK reads/writes catalog/configuration/journal/audit storage
and performs authorized SDK/Binder/ADB operations. The reference deployment
shows API35 as Android15, paired same-device adbd, and owner-managed host
loopback forwarding. The workstation-to-phone catalog line is an owner-mediated
SAF file import with validation/staging/atomic activation, not an undeclared remote
deployment service. Documentation rendering is a separate offline build container.
See [Structurizr implied relationships](https://docs.structurizr.com/dsl/cookbook/implied-relationships/)
and [deployment language](https://docs.structurizr.com/dsl/language#deploymentnode).
'''
p.write_text(s)
(R/'evidence/design018-boundary-working-ledger.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source='018pass03',working_fixes=['P3-F01','P3-F02','P3-F03','P3-F04','P3-F05','P3-F06','P3-F07','P3-F08','P3-F09','P3-F11','P3-F12','P3-F13','P3-F15','P3-F16','P3-F17','P3-F19','P3-F20'],frozen018_unchanged=True,validation_pending=True),indent=2)+'\n')
print('Updated explicit C4 boundaries, deployment and unified call paths')
