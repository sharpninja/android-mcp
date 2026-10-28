workspace "Android MCP Bridge" "All-Kotlin service and Compose UI; internal ARC compatibility port; design only" {
    !identifiers hierarchical
    !impliedRelationships false
    model {
        owner = person "Local owner" "Authenticates protected management commands"
        agent = softwareSystem "MCP client" "Consuming agent owns business decisions and selected context"
        arcClient = softwareSystem "Compatible ARC client" "Existing arc-protobuf-v1 remote-control client"
        arcTarget = softwareSystem "Compatible external ARC target" "Opted-in Avalonia or Kotlin target retaining ARC wire"
        android = softwareSystem "Android OS and target apps" "Final OS authority, Binder services, adbd and uninstrumented targets"
        sources = softwareSystem "Versioned public sources" "SDK, AOSP, permissions, AIDL and protocol evidence; unknown coverage retained"
        documentationSystem = softwareSystem "Local workstation tooling" "One native C# NUKE application, outside the Android runtime system" {
            documentation = container "Android MCP build tooling" "One category/operation registry and private stage DAG; local source/catalog/design/validation/package modules; never an APK component" "C# / .NET / NUKE" {
                acquisition = component "Source acquisition" "Pinned source and tool acquisition through shared transport, stores and leases" "C#"
                catalog = component "Catalog tooling" "Source extraction, schema validation and portable catalog generation" "C#"
                design = component "Documentation projection" "Canonical Markdown to editable SVG, Structurizr/PlantUML and self-contained offline HTML" "C#"
                validation = component "Shared validation" "Canonical model/module/document contracts, invoked by public validation and generation prerequisites" "C#"
                packaging = component "Portable packaging" "Deterministic source/configuration/artifact packaging through shared receipt and publication policies" "C#"
                planning = component "Operation registry and stage planner" "Single typed category/operation authority; Gradle compatibility aliases select one union of private stages" "C# / NUKE"
            }
        }
        bridge = softwareSystem "Android MCP bridge" "Full versioned inventory, including unknown/unavailable entries; separate authority and contextual discovery" {
            app = container "Android bridge APK" "One UID and one process in this reference design; Kotlin service owns authoritative state" "Kotlin / Jetpack Compose" {
                lifecycle = component "BridgeService" "Foreground lifecycle, boot eligibility and truthful status" "Kotlin Android Service"
                transport = component "McpServer" "Pinned MCP sessions/resources, host/pair/admin and journal notification adapters; separately negotiated direct events" "Kotlin"
                auth = component "ClientAuthenticator" "Credential, principal and session ownership" "Kotlin / Keystore"
                policy = component "AdmissionGate" "Canonical policy, approvals, epochs and dispatch boundary" "Kotlin"
                catalog = component "CatalogRepository" "Immutable imports, local overlays, credentials/configuration, CAS, full inventory" "Kotlin / SQLite"
                device = component "DeviceViewResolver" "Observed version applicability, coherent binding/type activation and idempotent epoch notification worker" "Kotlin"
                groups = component "FunctionalGroupIndex" "Bounded job discovery and atomic session activation" "Kotlin"
                invoke = component "InvocationPlanner" "Typed generic/raw dispatch, codec selection, Binder/SDK and admitted ADB/ARC adapter routing" "Kotlin"
                callbacks = component "CallbackBroker" "Registration, bounded journal, local reply and cleanup obligations" "Kotlin"
                arc = component "ArcEngine" "Proposed Kotlin port preserving pinned ARC wire/profile; no app implementation claimed" "Kotlin / protobuf"
                arcController = component "ArcClientAdapter" "Controller-side session/profile and compatible external-target connection" "Kotlin / protobuf"
                ui = component "Compose management UI" "Compose screen/view-model projections; selected roots use the ArcEngine-owned adapter; protected roots excluded" "Kotlin / Compose"
                ownerCommands = component "OwnerCommandFacade" "Protected owner commands; nonce/revision-bound fresh authentication" "Kotlin"
                sessionCommands = component "SessionCommandFacade" "Bounded unexported session commands; server-derived principal and canonical policy" "Kotlin"
                adb = component "AdbPhoneBackend" "Separate paired ADB connection manager and on-demand phone operations" "Kotlin"
                storage = component "SqliteStoreCoordinator" "One serialized writer and bounded reads; platform OutboxDispatcher dispatches committed worker obligations" "Kotlin / SQLite"
                audit = component "AuditLogger" "Mandatory audit and configurable per-client payload logging" "Kotlin"
            }
            db = container "App-private catalog/configuration" "Catalog snapshots, client/principal mappings, grants/logging configuration, groups/views, journal and audit; secret keys remain in Keystore" "SQLite" {
                tags "Database"
            }
        }
        owner -> bridge "Manages authorized local bridge settings"
        agent -> bridge "Discovers and invokes authorized Android capabilities" "MCP 2025-11-25"
        arcClient -> bridge "Controls approved selected root" "ARC transport arc-protobuf-v1; envelope 1.0"
        bridge -> arcTarget "Controls explicitly selected compatible target" "ARC raw loopback TCP; bearer authentication"
        bridge -> android "Uses actual SDK/Binder/paired ADB authority; no universal access"
        sources -> bridge "Supplies pinned offline inventory/protocol inputs"
        owner -> bridge.app "Uses local Compose management and protected approvals"
        agent -> bridge.app "Bounded discovery/invocation/resource reads" "Authenticated loopback MCP Streamable HTTP 2025-11-25"
        arcClient -> bridge.app "One request/socket or bounded watch" "arc-protobuf-v1; envelope 1.0; raw loopback TCP with bearer-in-envelope authentication"
        bridge.app -> arcTarget "First request/response validates pinned profile; no handshake" "ARC raw TCP; signed 4-byte BE; envelope 1.0; loopback"
        bridge.app -> android "SDK/Binder and separately paired ADB calls, probes and death signals"
        bridge.app -> bridge.db "Reads catalog/policy/groups/views; writes overlays/journal/audit via one serialized writer connection" "App-private SQLite"
        documentationSystem.documentation -> bridge.app "Produces portable catalog release; owner selects SAF document; importer verifies hashes/schema and stages atomic activation" "Owner-mediated validated import by SAF or disabled-by-default authenticated admin route; no deployment"
        documentationSystem.documentation -> owner "Produces editable Markdown, SVGs, local HTML and portable ZIP" "Local files only"
        owner -> bridge.app.ui "Manages local settings and reviews protected approvals"
        agent -> bridge.app.transport "Discovers jobs, invokes bindings and reads callback journal" "MCP 2025-11-25 Streamable HTTP over authenticated loopback TCP/owner adb forward"
        arcClient -> bridge.app.arc "Inspects approved target tree/frames and requests guarded input" "arc-protobuf-v1 loopback TCP"
        sources -> documentationSystem.documentation "Supplies immutable pinned input evidence"
        documentationSystem.documentation -> bridge.app.catalog "Supplies validated versioned catalog release"
        bridge.app.lifecycle -> bridge.app.transport "Starts and stops listener according to actual readiness"
        bridge.app.lifecycle -> bridge.app.catalog "Loads trusted catalog and configuration"
        bridge.app.lifecycle -> bridge.app.device "Requests startup resolution and refresh on supported backend/device capability changes"
        bridge.app.lifecycle -> bridge.app.arc "Owns enabled ARC listener lifecycle"
        bridge.app.lifecycle -> android "Requests eligible foreground promotion and truthful notification"
        bridge.app.lifecycle -> bridge.app.callbacks "Reconciles orphaned registrations and routes epoch invalidation"
        bridge.app.transport -> bridge.app.auth "Authenticates every request and reconnect"
        bridge.app.transport -> bridge.app.policy "Submits exact proposed plan for canonical admission"
        bridge.app.transport -> bridge.app.groups "Requests bounded discovery or activation"
        bridge.app.policy -> bridge.app.ui "Requests exact bounded Ask challenge after finite-operation quiescence; no automatic approval"
        bridge.app.ui -> bridge.app.policy "Returns exact challenge result or denial; same-process trust is disclosed"
        bridge.app.auth -> bridge.app.callbacks "Revokes credential-owned subscriptions and delivery"
        bridge.app.auth -> bridge.app.arc "Revokes inbound streams and input leases"
        bridge.app.policy -> bridge.app.auth "Pins current principal and credential epoch"
        bridge.app.policy -> bridge.app.device "Checks selected availability and type closure"
        bridge.app.policy -> bridge.app.catalog "Checks immutable contract and current overlay pins"
        bridge.app.transport -> bridge.app.invoke "Plans bounded operation then dispatches canonical Admission"
        bridge.app.policy -> bridge.app.transport "Returns admission bound to exact plan and current pins"
        bridge.app.invoke -> bridge.app.transport "Returns finite outcome without replaying uncertain effects"
        bridge.app.transport -> agent "Returns finite MCP results/URI-only hints or negotiated non-MCP direct-event SSE" "MCP 2025-11-25; direct events are a separate negotiated HTTP route"
        bridge.app.policy -> bridge.app.audit "Records canonical admission decision"
        bridge.app.invoke -> bridge.app.audit "Records dispatch and bounded outcome, including unknown effect"
        bridge.app.device -> bridge.app.catalog "Reads streamed inventory; stages and atomically activates resolved view"
        bridge.app.device -> android "Uses supported version/module/interface/reachability probes"
        bridge.app.groups -> bridge.app.catalog "Searches full index and resolves coherent dependency closure"
        bridge.app.groups -> bridge.app.policy "Queries current grant status and rechecks activation; never grants authority"
        bridge.app.groups -> bridge.app.device "Pins compatible membership and coherent required type closure"
        bridge.app.invoke -> android "Performs authorized SDK/Binder operation"
        bridge.app.invoke -> bridge.app.arcController "ArcTargetBackend forwards exact admitted canonical ARC operation"
        bridge.app.invoke -> bridge.app.adb "Dispatches admitted bounded ADB plan"
        bridge.app.adb -> android "Paired same-device ADB; on-demand screen state and coordinate tap"
        android -> bridge.app.invoke "Returns registration or invocation outcome"
        bridge.app.invoke -> bridge.app.callbacks "Registers typed callback proxy and cleanup plan"
        android -> bridge.app.callbacks "Calls verified callback endpoint; authority remains separate"
        bridge.app.callbacks -> bridge.app.storage "Appends ordered bounded events and cleanup state through one writer"
        bridge.app.callbacks -> bridge.app.transport "Signals committed journal change for coalesced URI-only hints"
        bridge.app.transport -> bridge.app.callbacks "callback.status/cancel dispatch, exact-URI subscribe and finite owned journal reads"
        bridge.app.callbacks -> bridge.app.policy "Rechecks event/reply/cleanup authority and epochs"
        bridge.app.catalog -> bridge.app.storage "Validated catalog/config transactions and bounded read snapshots"
        bridge.app.catalog -> bridge.app.policy "Returns committed typed epochs within protected commit barrier"
        bridge.app.audit -> bridge.app.storage "Appends audit/logs and reads redacted snapshots through store coordinator"
        bridge.app.ui -> bridge.app.ownerCommands "Submits typed CAS commands and observes immutable state"
        bridge.app.ownerCommands -> bridge.app.policy "Requires exact fresh owner capability for protected transitions"
        bridge.app.ownerCommands -> bridge.app.catalog "Commits validated owner catalog/configuration changes"
        bridge.app.arc -> bridge.app.policy "Uses same admission and protected-owner boundary"
        bridge.app.arc -> bridge.app.ui "Operates only selected unprotected ComposeTargetAdapter root"
        bridge.app.arc -> bridge.app.audit "Records principal-scoped protocol outcomes"

        bridge.app.ui -> bridge.app.sessionCommands "Submits bounded session command; cannot assert owner identity"
        bridge.app.sessionCommands -> bridge.app.policy "Derives trusted context and admits exact target operation"
        bridge.app.sessionCommands -> bridge.app.arcController "Forwards admitted explicit target/session command"
        bridge.app.arcController -> arcTarget "Validates pinned version/id/status on first request/response; no handshake" "arc-protobuf-v1 raw loopback TCP; envelope 1.0"
        bridge.app.arcController -> bridge.app.policy "Checks client target grants before forwarding"
        bridge.app.callbacks -> android "Executes only the recorded verified cleanup plan"
        bridge.app.auth -> bridge.app.catalog "Reads credential/client mappings and current epochs"
        bridge.app.auth -> bridge.app.audit "Records bounded authentication/session/revocation events"
        bridge.app.ownerCommands -> bridge.app.lifecycle "Reads health/boot eligibility; protected configuration uses repository"
        bridge.app.ownerCommands -> bridge.app.callbacks "Inspects metadata and explicitly cancels owned registrations"
        bridge.app.ownerCommands -> bridge.app.device "Requests observed capability refresh; no asserted facts"
        bridge.app.catalog -> bridge.app.device "Invalidates on committed catalog or override publication"
        android -> bridge.app.lifecycle "Delivers eligible boot/unlock/lifecycle events"
        bridge.app.groups -> bridge.app.transport "Returns atomic view epoch and best-effort list_changed"
        bridge.app.invoke -> bridge.app.policy "Acquires/releases dispatch permit under shared epoch/quiescence barrier"
        bridge.app.policy -> bridge.app.lifecycle "Requests truthful pending-owner notification; local open action never approval"
        bridge.app.auth -> bridge.app.transport "Invalidates sessions and transport delivery after revocation"
        bridge.app.policy -> bridge.app.auth "Publishes committed credential/policy epochs under admission barrier"
        bridge.app.policy -> bridge.app.ownerCommands "Returns protected commit outcome after synchronous epoch publication"
        bridge.app.adb -> bridge.app.device "Supported ADB loss/identity/reachability change invalidates affected view"
        bridge.app.invoke -> bridge.app.device "Backend capability/interface metadata events trigger supported view refresh"
        bridge.app.arc -> arcClient "Sends one result or bounded watch responses with matching ID/version"
        bridge.app.ownerCommands -> bridge.app.ui "Returns protected command result or explicit failure"

        bridge.app.storage -> bridge.app.callbacks "Dispatches committed verified cleanup worker obligation"
        bridge.app.storage -> bridge.app.transport "Dispatches coalesced committed URI-hint notification worker"
        bridge.app.storage -> bridge.app.device "Dispatches idempotent committed epoch notification worker"
        bridge.app.lifecycle -> bridge.app.invoke "Closes admitted SDK/Binder backend resources within stop deadline"
        bridge.app.lifecycle -> bridge.app.adb "Closes ADB collectors and owned connection resources"
        bridge.app.storage -> bridge.db "Owns the single writer connection and bounded snapshot readers"
        bridge.app.lifecycle -> bridge.app.storage "Owns and closes coordinator before service shutdown"
        bridge.app.ownerCommands -> bridge.app.audit "Exports exact redacted snapshot after fresh owner authorization"
        bridge.app.sessionCommands -> bridge.app.audit "Queries bounded local-owner log snapshots; no direct SQL"
        bridge.app.sessionCommands -> bridge.app.lifecycle "Ordinary local stop/status; stopped-service start uses an Activity foreground-service start intent"
        bridge.app.arc -> bridge.app.auth "ArcAuthenticator delegates expected_kind=arc to shared credential/client registry"
        deploymentEnvironment "ReferenceAndroid" {
            phone = deploymentNode "Android phone" "Reference deployment; not installed by this task" "Android 15+ (API 35+ reference floor)" {
                sandbox = deploymentNode "One app UID sandbox" "Single reference Kotlin application process" "Android app process" {
                    runtime = containerInstance bridge.app
                    containerInstance bridge.db
                }
                deploymentNode "Optional instrumented target app" "Different UID; explicitly opted-in ARC target, not needed for generic ADB" "Separate UID opted-in target; generic ADB needs no embedding" {
                    softwareSystemInstance arcTarget
                }
                localAgent = softwareSystemInstance agent
                adbd = infrastructureNode "Paired adbd" "Wireless debugging identity and endpoint are owner-paired; actual OS limits apply" "ADB authenticated connect channel"
            }
            host = deploymentNode "Owner-operated remote host" "Logical client protocol lines use an explicit owner-established loopback forward" "Host loopback; logical protocol lines use owner adb forward" {
                softwareSystemInstance agent
                softwareSystemInstance arcClient
                forwarder = infrastructureNode "ADB loopback forwarding" "adb forward --no-rebind tcp:0 to the selected phone loopback listener; allocated host port is recorded" "Owner-managed ADB transport"
            }
            deploymentNode "Build workstation" "Local offline tools and renderer; no automatic phone deployment" "Existing pinned toolchain" {
                containerInstance documentationSystem.documentation
            }
            phone.sandbox.runtime -> phone.adbd "Same-device paired ADB connection; separate backend health and authority"
            host.forwarder -> phone.sandbox.runtime "Forward only selected device loopback MCP/ARC listener; ARC plaintext is not made TLS by ADB"
        }
    }
    views {
        properties {
            "plantuml.skinparams" "backgroundColor=white,defaultFontName=DejaVu Sans"
            "plantuml.sequenceDiagram" "true"
        }
        systemContext bridge Context "C4 system context" {
            include *
            autoLayout lr
        }
        container bridge Containers "C4 containers: Kotlin runtime and external workstation tooling" {
            include *
            autoLayout lr
        }
        component bridge.app ServerComponents "C4 Kotlin server components" {
            include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.device bridge.app.groups bridge.app.invoke bridge.app.adb bridge.app.callbacks bridge.app.audit bridge.app.storage bridge.db agent android
            autoLayout tb
        }
        component bridge.app ArcComponents "C4 internal ARC and local UI boundary" {
            include bridge.app.lifecycle bridge.app.arc bridge.app.arcController bridge.app.ui bridge.app.ownerCommands bridge.app.sessionCommands bridge.app.policy bridge.app.auth bridge.app.audit bridge.app.storage bridge.db arcClient arcTarget owner android
            autoLayout tb
        }
        component bridge.app ServerCoreComponents "C4 detail: transport, identity and canonical policy" {
            include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.device bridge.app.callbacks bridge.app.groups bridge.app.ownerCommands bridge.app.sessionCommands bridge.app.audit bridge.app.storage bridge.db agent
            autoLayout tb
        }
        component bridge.app ExecutionComponents "C4 detail: device view, invocation and callbacks" {
            include bridge.app.policy bridge.app.device bridge.app.invoke bridge.app.arcController bridge.app.adb bridge.app.callbacks bridge.app.transport bridge.app.catalog bridge.app.storage bridge.db android
            autoLayout tb
        }
        dynamic bridge.app Startup "Startup: trusted inventory and atomic device view" {
            bridge.app.lifecycle -> android "Eligible foreground start: show Starting notification before slow work"
            bridge.app.lifecycle -> bridge.app.catalog "Load validated immutable catalog and config"
            bridge.app.lifecycle -> bridge.app.callbacks "Mark prior OS registrations orphaned; do not blindly re-register"
            bridge.app.lifecycle -> bridge.app.device "Resolve observed compatible binding and type graph"
            bridge.app.device -> android "Read supported capability facts, retaining unknowns"
            bridge.app.device -> bridge.app.catalog "Seal and atomically activate complete view"
            bridge.app.lifecycle -> bridge.app.transport "Expose Running only after ready listener"
            bridge.app.lifecycle -> android "Update notification from actual readiness; keep backend health separate"
            autoLayout tb
        }
        dynamic bridge.app CallbackFlow "Finite callback registration; then separate resource subscription" {
            agent -> bridge.app.transport "POST tools/call: invoke with typed callback bindings"
            bridge.app.transport -> bridge.app.auth "Authenticate principal and scope"
            bridge.app.transport -> bridge.app.invoke "Plan exact registration, cleanup and callback types"
            bridge.app.transport -> bridge.app.policy "Admit exact plan with pinned authority and quotas"
            bridge.app.policy -> bridge.app.transport "Return Admission; no device effect yet"
            bridge.app.transport -> bridge.app.invoke "dispatch(admission) once"
            bridge.app.invoke -> bridge.app.callbacks "Create provisional proxy, journal and cleanup obligation"
            bridge.app.invoke -> android "Register verified SDK/Binder listener with exact argument mapping"
            android -> bridge.app.invoke "Return registration result or explicit unknown outcome"
            bridge.app.invoke -> bridge.app.transport "Return finite registration outcome and subscription handle"
            bridge.app.transport -> agent "Final tool result: handle + journal URI; POST SSE ends"
            agent -> bridge.app.transport "resources/subscribe: exact events URI"
            bridge.app.transport -> agent "Return finite resources/subscribe acknowledgement"
            android -> bridge.app.callbacks "Deliver callback; synchronous reply is bounded and local"
            bridge.app.callbacks -> bridge.app.storage "Accept and commit ordered journal event within quotas"
            bridge.app.callbacks -> bridge.app.transport "Coalesce URI update hint after commit"
            bridge.app.transport -> agent "GET SSE: subscribed URI-only resource update"
            agent -> bridge.app.transport "resources/read cursor-expanded URI for finite page"
            bridge.app.transport -> agent "Finite journal page; separate application cursor"
            autoLayout tb
        }
        dynamic bridge.app InvocationFlow "Authenticated invocation and canonical admission" {
            agent -> bridge.app.transport "Submit bounded tools/call"
            bridge.app.transport -> bridge.app.auth "Authenticate session owner"
            bridge.app.transport -> bridge.app.invoke "Plan exact binding, arguments and backend scope"
            bridge.app.transport -> bridge.app.policy "Check per-client grant, Ask consent, OS/backend authority and current epochs"
            bridge.app.policy -> bridge.app.lifecycle "Ask branch: notify pending local review when UI absent; original deadline unchanged"
            bridge.app.policy -> bridge.app.ui "Ask branch: local exact review within original deadline; deny/timeout means no dispatch"
            owner -> bridge.app.ui "Ask branch: approve exact local challenge or deny within original deadline"
            bridge.app.ui -> bridge.app.policy "Approve exact challenge or return structured denial; continue only if approved"
            bridge.app.policy -> bridge.app.device "Pin compatible binding and coherent types"
            bridge.app.policy -> bridge.app.catalog "Recheck current document and contract pins"
            bridge.app.policy -> bridge.app.audit "Fail-closed mandatory admission audit before dispatch"
            bridge.app.policy -> bridge.app.transport "Return exact Admission and policy/contract pins"
            bridge.app.transport -> bridge.app.invoke "Dispatch Admission once; no replay of uncertain effects"
            bridge.app.invoke -> bridge.app.policy "beginDispatch: atomically recheck epochs/authority and register in-flight permit; reject if quiescing"
            bridge.app.invoke -> android "Invoke permitted backend only under current dispatch permit"
            bridge.app.invoke -> bridge.app.policy "Release dispatch permit after bounded backend work or record unknown outcome"
            bridge.app.invoke -> bridge.app.audit "Record admitted dispatch and observed/unknown outcome; audit failure blocks further dispatch"
            bridge.app.invoke -> bridge.app.transport "Return bounded invocation result or structured denial/unknown outcome"
            bridge.app.transport -> agent "Return single final JSON-RPC result/error; POST SSE normally terminates"
            autoLayout tb
        }
        dynamic bridge.app GroupFlow "Job discovery and atomic interest activation" {
            agent -> bridge.app.transport "Discover groups for a job and context budget"
            bridge.app.transport -> bridge.app.auth "Authenticate request"
            bridge.app.transport -> bridge.app.groups "Search summaries only"
            bridge.app.groups -> bridge.app.catalog "Read full searchable index and dependency closure"
            bridge.app.groups -> bridge.app.policy "Query per-client permission status; discovery grants nothing"
            bridge.app.groups -> bridge.app.transport "Return bounded ranked summaries with pinned cursor"
            bridge.app.transport -> agent "Return ranked bounded summaries and discovery cursor"
            agent -> bridge.app.transport "Request activation with selected IDs, pins and current group-view ETag"
            bridge.app.transport -> bridge.app.groups "Begin requested atomic activation with expected pins"
            bridge.app.groups -> bridge.app.device "Validate availability and required type/dependency closure"
            bridge.app.groups -> bridge.app.catalog "CAS commit coherent activation or return stale/conflict"
            bridge.app.groups -> bridge.app.transport "Return committed group view or explicit conflict"
            bridge.app.transport -> agent "Return committed group epoch/hash; portable generic-invoke fallback remains"
            bridge.app.transport -> agent "For opted-in dynamic aliases, best-effort tools/list_changed; host context eviction not guaranteed"
            autoLayout tb
        }
        dynamic bridge.app ArcSession "ARC one request per socket; compatible stream lifetime" {
            arcClient -> bridge.app.arc "Open raw loopback TCP with bearer-in-envelope authentication; one length-prefixed request"
            bridge.app.arc -> bridge.app.auth "Validate version first, then authenticate expected_kind=arc, then validate method before target access"
            bridge.app.arc -> bridge.app.policy "Admit authenticated principal and exact reserved ARC method identity"
            bridge.app.arc -> bridge.app.ui "Access only selected unprotected root after admission"
            bridge.app.arc -> arcClient "Unary result OR repeated watch responses with same ID/version; bounded payload"
            bridge.app.arc -> arcClient "Normal-completion branch: terminal end_of_stream=true then close"
            arcClient -> bridge.app.arc "Alternative before normal completion: cancel by closing watch socket; Cancel 8 unsupported"
            bridge.app.arc -> bridge.app.audit "Close per-request streams; input lease spans unary sockets; record sanitized outcome"
            autoLayout tb
        }
        dynamic bridge.app OwnerTransition "Fresh owner capability bound to exact CAS command" {
            owner -> bridge.app.ui "Choose protected command and inspect exact scope/revisions"
            bridge.app.ui -> bridge.app.ownerCommands "Request nonce and protected authentication challenge"
            bridge.app.ownerCommands -> bridge.app.ui "Service closes/drains all finite bridge dispatch and pauses frame/tree/log exposure before protected OS prompt"
            owner -> bridge.app.ui "Complete protected OS biometric/device credential prompt or deny/cancel"
            bridge.app.ui -> bridge.app.ownerCommands "Deliver platform-auth completion bound to service challenge; no JSON owner assertion"
            bridge.app.ownerCommands -> bridge.app.policy "Validate fresh owner capability and current command/revisions"
            bridge.app.policy -> bridge.app.catalog "Verify-and-consume capability at serialized CAS commit; reject mismatch"
            bridge.app.catalog -> bridge.app.policy "Return committed epoch facts before admission resumes"
            bridge.app.policy -> bridge.app.auth "Publish affected credential/policy epochs synchronously"
            bridge.app.ownerCommands -> bridge.app.policy "Reopen only after last challenge reaches terminal disposition; success, denial, expiry and cancellation share finalizer"
            bridge.app.ownerCommands -> bridge.app.ui "Return committed revision or denial/conflict; no remote self-approval"
            autoLayout tb
        }
        dynamic bridge.app Shutdown "Explicit stop; bounded cleanup before store close" {
            bridge.app.sessionCommands -> bridge.app.lifecycle "Persist desired Stopped; begin orderly stop"
            bridge.app.lifecycle -> bridge.app.transport "Close admission and listener; finish/cancel owned work within deadline"
            bridge.app.lifecycle -> bridge.app.arc "Close ARC listener, watches and logical input leases"
            bridge.app.lifecycle -> bridge.app.callbacks "Suppress delivery; attempt recorded verified unregister within stop deadline before backend close; persist unresolved disposition"
            bridge.app.lifecycle -> bridge.app.invoke "Close admitted SDK/Binder backend resources within remaining stop deadline"
            bridge.app.lifecycle -> bridge.app.adb "Close ADB collectors and owned connection resources; preserve unknown disposition"
            bridge.app.lifecycle -> bridge.app.storage "Drain committed writer work and close coordinator"
            bridge.app.lifecycle -> android "Publish Stopped, stopForeground and service; no automatic restart"
            autoLayout tb
        }
        dynamic bridge.app CredentialRevocation "Exact owner revoke and post-commit epoch invalidation" {
            bridge.app.ui -> bridge.app.ownerCommands "Submit exact credential/revision with fresh owner capability"
            bridge.app.ownerCommands -> bridge.app.policy "Enter exact protected commit under shared admission/epoch barrier"
            bridge.app.policy -> bridge.app.catalog "Verify-and-consume owner proof; CAS revoke and advance epochs in one transaction"
            bridge.app.catalog -> bridge.app.policy "Return committed revisions/epochs, or rollback failure"
            bridge.app.policy -> bridge.app.auth "Publish committed epoch now; push all affected session/stream invalidation before release"
            bridge.app.auth -> bridge.app.transport "Invalidate affected MCP sessions/delivery immediately at epoch barrier"
            bridge.app.auth -> bridge.app.callbacks "Close delivery; only recorded safe cleanup"
            bridge.app.auth -> bridge.app.arc "Close affected streams and release principal input lease"
            bridge.app.auth -> bridge.app.audit "Record committed revocation outcome without credential bytes"
            autoLayout tb
        }
        deployment * "ReferenceAndroid" Deployment "Reference deployment; not installed by this task" {
            include *
            autoLayout tb
        }
        image bridge.app ServerCoreClasses {
            image ../docs/diagrams/server-core-classes.svg
            title "C4 code-level detail: Kotlin server ownership (UML class notation)"
        }
        image bridge.app ServerExecutionClasses {
            image ../docs/diagrams/server-execution-classes.svg
            title "C4 code-level detail: invocation, callbacks and journal (UML class notation)"
        }
        image bridge.app ServerAdapterClasses {
            image ../docs/diagrams/server-adapters-classes.svg
            title "C4 code-level detail: host, pairing, admin and committed work (UML class notation)"
        }
        image bridge.app ArcUiClasses {
            image ../docs/diagrams/arc-ui-classes.svg
            title "C4 code-level detail: internal ARC engine and Compose adapter (UML class notation)"
        }
        image bridge.app ArcControllerClasses {
            image ../docs/diagrams/arc-controller-classes.svg
            title "C4 code-level detail: controller Compose UI and ARC client (UML class notation)"
        }
        image bridge.app ArcTargetClasses {
            image ../docs/diagrams/arc-target-classes.svg
            title "C4 code-level detail: Compose tree, frame, input and property adapters (UML class notation)"
        }
        styles {
            element "Element" {
                color #17212b
                background #edf3f8
                stroke #34526b
                fontSize 24
            }
            element "Person" {
                shape Person
            }
            element "Database" {
                shape Cylinder
            }
            relationship "Relationship" {
                color #34526b
                fontSize 20
            }
        }
    }
}
