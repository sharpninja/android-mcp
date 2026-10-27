workspace "Android MCP Bridge" "All-Kotlin service and Compose UI; internal ARC compatibility port; design only" {
    !identifiers hierarchical
    model {
        owner = person "Local owner" "Authenticates protected management commands"
        agent = softwareSystem "MCP client" "Consuming agent owns business decisions and selected context"
        arcClient = softwareSystem "Compatible ARC client" "Existing arc-protobuf-v1 remote-control client"
        arcTarget = softwareSystem "Compatible external ARC target" "Opted-in Avalonia or Kotlin target retaining ARC wire"
        android = softwareSystem "Android OS and target apps" "Final OS authority, Binder services, adbd and uninstrumented targets"
        sources = softwareSystem "Versioned public sources" "SDK, AOSP, permissions, AIDL and protocol evidence; unknown coverage retained"
        bridge = softwareSystem "Android MCP bridge" "Complete binding inventory with separate authority and contextual discovery" {
            app = container "Android bridge APK" "One UID and initially one process; Kotlin service owns authoritative state" "Kotlin / Jetpack Compose" {
                lifecycle = component "BridgeService" "Foreground lifecycle, boot eligibility and truthful status" "Kotlin Android Service"
                transport = component "McpServer" "Pinned MCP2025-11-25 transport, sessions and bounded resource reads" "Kotlin"
                auth = component "ClientAuthenticator" "Credential, principal and session ownership" "Kotlin / Keystore"
                policy = component "AdmissionGate" "Canonical policy, approvals, epochs and dispatch boundary" "Kotlin"
                catalog = component "CatalogRepository" "Immutable imports, local overlays, CAS, full inventory" "Kotlin / SQLite"
                device = component "DeviceViewResolver" "Observed version applicability and coherent binding/type activation" "Kotlin"
                groups = component "FunctionalGroupIndex" "Bounded job discovery and atomic session activation" "Kotlin"
                invoke = component "InvocationPlanner" "Typed generic/raw dispatch, codecs, Binder and ADB adapters" "Kotlin"
                callbacks = component "CallbackBroker" "Registration, bounded journal, local reply and cleanup obligations" "Kotlin"
                arc = component "ArcEngine" "Internal Kotlin port preserving pinned ARC wire/profile; not implemented" "Kotlin / protobuf"
                arcController = component "ArcClientAdapter" "Controller-side session/profile and compatible external-target connection" "Kotlin / protobuf"
                ui = component "Compose management UI" "Screen definitions, grants, group/catalog/log/subscription management" "Kotlin / Compose"
                ownerCommands = component "OwnerCommandFacade" "Bounded unexported local Binder commands and owner authentication" "Kotlin"
                audit = component "AuditLogger" "Mandatory audit and configurable per-client payload logging" "Kotlin"
            }
            db = container "App-private catalog/configuration" "Catalog snapshots, policy, groups, views, event journal and audit" "SQLite" {
                tags "Database"
            }
            tooling = container "Offline catalog tooling" "Source acquisition/extraction/schema validation and reference fixtures; no phone operations" "Python / Gradle docs"
        }
        owner -> bridge.app.ui "Manages local settings and reviews protected approvals"
        agent -> bridge.app.transport "Discovers jobs, invokes bindings and reads callback journal" "Authenticated MCP"
        arcClient -> bridge.app.arc "Inspects approved target tree/frames and requests guarded input" "arc-protobuf-v1 loopback TCP"
        sources -> bridge.tooling "Supplies immutable pinned input evidence"
        bridge.tooling -> bridge.app.catalog "Supplies validated versioned catalog release"
        bridge.app.lifecycle -> bridge.app.transport "Starts and stops listener according to actual readiness"
        bridge.app.lifecycle -> bridge.app.catalog "Loads trusted catalog and configuration"
        bridge.app.lifecycle -> bridge.app.device "Requests startup capability resolution"
        bridge.app.lifecycle -> bridge.app.arc "Owns enabled ARC listener lifecycle"
        bridge.app.transport -> bridge.app.auth "Authenticates every request and reconnect"
        bridge.app.transport -> bridge.app.policy "Submits exact admitted operation plan"
        bridge.app.transport -> bridge.app.groups "Requests bounded discovery or activation"
        bridge.app.policy -> bridge.app.auth "Pins current principal and credential epoch"
        bridge.app.policy -> bridge.app.device "Checks selected availability and type closure"
        bridge.app.policy -> bridge.app.catalog "Checks immutable contract and current overlay pins"
        bridge.app.policy -> bridge.app.invoke "Admits verified canonical dispatch"
        bridge.app.policy -> bridge.app.audit "Records admission and bounded outcome"
        bridge.app.device -> bridge.app.catalog "Streams full inventory and persists atomic resolved view"
        bridge.app.device -> android "Uses supported version/module/interface/reachability probes"
        bridge.app.groups -> bridge.app.catalog "Searches full index and resolves coherent dependency closure"
        bridge.app.groups -> bridge.app.policy "Reports grants and rechecks activation epochs without granting authority"
        bridge.app.invoke -> android "Performs authorized SDK/Binder/paired ADB operation"
        bridge.app.invoke -> bridge.app.callbacks "Registers typed callback proxy and cleanup plan"
        android -> bridge.app.callbacks "Calls verified callback endpoint; authority remains separate"
        bridge.app.callbacks -> bridge.db "Appends ordered bounded events and cleanup state"
        bridge.app.callbacks -> bridge.app.transport "Signals URI-only resource update and bounded reads"
        bridge.app.callbacks -> bridge.app.policy "Rechecks event/reply/cleanup authority and epochs"
        bridge.app.catalog -> bridge.db "Owns transactional writes and read-only snapshots"
        bridge.app.audit -> bridge.db "Persists mandatory audit and configured call records"
        bridge.app.ui -> bridge.app.ownerCommands "Submits typed CAS commands and observes immutable state"
        bridge.app.ownerCommands -> bridge.app.policy "Requires exact fresh owner capability for protected transitions"
        bridge.app.ownerCommands -> bridge.app.catalog "Commits validated owner catalog/configuration changes"
        bridge.app.arc -> bridge.app.policy "Uses same admission and protected-owner boundary"
        bridge.app.arc -> bridge.app.ui "Operates only selected unprotected ComposeTargetAdapter root"
        bridge.app.arc -> bridge.app.audit "Records principal-scoped protocol outcomes"
        bridge.app.arc -> android "Uses separate authorized ADB backend for generic phone control"
        bridge.app.ui -> bridge.app.arcController "Controls explicitly selected remote session through local service"
        bridge.app.arcController -> arcTarget "Connects and validates exact retained ARC profile"
        bridge.app.arcController -> bridge.app.policy "Checks client target grants before forwarding"
        deploymentEnvironment "ReferenceAndroid" {
            deploymentNode "Android phone" "Reference deployment, not a deployed app" "Android35+" {
                deploymentNode "One app UID and process" "No privilege boundary between UI and service" "Kotlin Android APK" {
                    containerInstance bridge.app
                    containerInstance bridge.db
                }
            }
            deploymentNode "Build workstation" "Offline tooling and local documentation renderer" "Existing local toolchain" {
                containerInstance bridge.tooling
            }
        }
    }
    views {
        properties {
            "plantuml.skinparams" "backgroundColor=white,defaultFontName=DejaVu Sans"
        }
        systemContext bridge Context "C4 system context" {
            include *
            autoLayout lr
        }
        container bridge Containers "C4 containers: runtime and offline tooling" {
            include *
            autoLayout lr
        }
        component bridge.app ServerComponents "C4 Kotlin server components" {
            include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.device bridge.app.groups bridge.app.invoke bridge.app.callbacks bridge.app.audit bridge.db agent android
            autoLayout tb
        }
        component bridge.app ArcComponents "C4 internal ARC and local UI boundary" {
            include bridge.app.arc bridge.app.arcController bridge.app.ui bridge.app.ownerCommands bridge.app.policy bridge.app.audit bridge.db arcClient arcTarget owner android
            autoLayout tb
        }
        component bridge.app ServerCoreComponents "C4 detail: transport, identity and canonical policy" {
            include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.groups bridge.app.ownerCommands bridge.app.audit bridge.db agent
            autoLayout tb
        }
        component bridge.app ExecutionComponents "C4 detail: device view, invocation and callbacks" {
            include bridge.app.policy bridge.app.device bridge.app.invoke bridge.app.callbacks bridge.app.transport bridge.app.catalog bridge.db android
            autoLayout tb
        }
        dynamic bridge.app Startup "Startup: trusted inventory and atomic device view" {
            bridge.app.lifecycle -> bridge.app.catalog "Load validated immutable catalog and config"
            bridge.app.lifecycle -> bridge.app.device "Resolve observed compatible binding and type graph"
            bridge.app.device -> android "Read supported capability facts, retaining unknowns"
            bridge.app.device -> bridge.app.catalog "Seal and atomically activate complete view"
            bridge.app.lifecycle -> bridge.app.transport "Expose Running only after ready listener"
            autoLayout tb
        }
        dynamic bridge.app CallbackFlow "Finite registration and bounded callback journal" {
            agent -> bridge.app.transport "Invoke with typed callback bindings"
            bridge.app.transport -> bridge.app.auth "Authenticate principal and scope"
            bridge.app.transport -> bridge.app.policy "Check exact registration and cleanup contracts"
            bridge.app.policy -> bridge.app.invoke "Admit registration"
            bridge.app.invoke -> bridge.app.callbacks "Create scoped registration and journal handle"
            android -> bridge.app.callbacks "Deliver callback; bounded local reply only"
            bridge.app.callbacks -> bridge.db "Ordered bounded append"
            bridge.app.callbacks -> bridge.app.transport "URI update hint; resource reads supply finite pages"
            autoLayout tb
        }
        dynamic bridge.app InvocationFlow "Authenticated invocation and canonical admission" {
            agent -> bridge.app.transport "Submit bounded tools/call"
            bridge.app.transport -> bridge.app.auth "Authenticate session owner"
            bridge.app.transport -> bridge.app.policy "Submit exact schema and scope plan"
            bridge.app.policy -> bridge.app.device "Pin compatible binding and coherent types"
            bridge.app.policy -> bridge.app.catalog "Recheck current document and contract pins"
            bridge.app.policy -> bridge.app.invoke "Admit once, no replay of uncertain effects"
            bridge.app.invoke -> android "Invoke permitted backend"
            bridge.app.policy -> bridge.app.audit "Record bounded outcome"
            autoLayout tb
        }
        dynamic bridge.app GroupFlow "Job discovery and atomic interest activation" {
            agent -> bridge.app.transport "Discover groups for a job and context budget"
            bridge.app.transport -> bridge.app.auth "Authenticate request"
            bridge.app.transport -> bridge.app.groups "Search summaries only"
            bridge.app.groups -> bridge.app.catalog "Read full searchable index and dependency closure"
            bridge.app.groups -> bridge.app.policy "Report permission status; no grant change"
            bridge.app.transport -> bridge.app.groups "CAS activate chosen coherent set or return conflict"
            autoLayout tb
        }
        deployment bridge "ReferenceAndroid" Deployment "Reference deployment; not installed by this task" {
            include *
            autoLayout lr
        }
        image bridge.app.transport ServerCoreClasses {
            image ../docs/diagrams/server-core-classes.svg
            title "C4 code-level detail: Kotlin server ownership (UML class notation)"
        }
        image bridge.app.invoke ServerExecutionClasses {
            image ../docs/diagrams/server-execution-classes.svg
            title "C4 code-level detail: invocation, callbacks and journal (UML class notation)"
        }
        image bridge.app.arc ArcUiClasses {
            image ../docs/diagrams/arc-ui-classes.svg
            title "C4 code-level detail: internal ARC engine and Compose adapter (UML class notation)"
        }
        image bridge.app.arcController ArcControllerClasses {
            image ../docs/diagrams/arc-controller-classes.svg
            title "C4 code-level detail: controller Compose UI and ARC client (UML class notation)"
        }
        image bridge.app.arc ArcTargetClasses {
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
