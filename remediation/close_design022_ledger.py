"""Finish the unified022 design ledger; no changes to its frozen input/receipts."""
from pathlib import Path
import json
R=Path(__file__).resolve().parent;P=R/'design-c4';p=P/'docs/Android MCP Bridge Design.md';s=p.read_text()
s=s.replace('endpoint_metadata_json; peer_fingerprint nullable; verification_strength;','endpoint_metadata_json; peer_fingerprint nullable; verification_strength; peer_identity_verification_method CHECK none/adb_tls_pairing;')
s=s.replace('| Approved | Consumed once at admission if every binding still matches; otherwise invalidated |','| Approved | Reserved once at admission-gate entry if every binding still matches; otherwise invalidated |\n| Reserved | Consumed at dispatch/commit; persistence/CAS failure, expiry or revocation makes it Terminal; never reusable |')
s=s.replace('no runtime bytecode generation','no app-generated dex/bytecode; platform java.lang.reflect.Proxy is permitted')
s=s.replace('No runtime dex/bytecode generation or downloaded executable code is selected.','No app-generated dex/bytecode or downloaded executable code is selected; platform java.lang.reflect.Proxy may generate its own interface proxy class.')
s=s.replace('and catalog_readiness in {absent,validating,ready,quarantined,capacity_required}.','catalog_readiness in {absent,validating,ready,quarantined,capacity_required}, and arc_listener in {disabled,binding,ready,error}.')
s=s.replace('with acquisition/authority/transport/codec emitting layers respectively','with acquisition/authority/transport/acquisition emitting layers respectively')
row='| POST /v1/catalog/imports | Idempotent immutable staging and validation, no active view change. |'
s=s.replace(row,'| POST /v1/catalog/import-stages | Owner-authorized admin-window upload with expected hash/size; sealed handle, no active view change. |\n| GET /v1/catalog/import-stages | Sanitized handles owned by this admin/window; expired stages deleted. |\n| POST /v1/catalog/imports | Idempotent immutable import from still-sealed handle, exact owner proof and expected revision; no active view change until publish. |')
s=s.replace('ARC policy identities use a reserved catalog namespace arc-protobuf-v1:method:N','ARC entry-point tool_id values use reserved namespace arc-protobuf-v1:method:N')
s=s.replace('Rules attach to those canonical identities and exact target/root/user/profile pins.','These are tool IDs, not binding_id/entry_id values. Each method also has a SHA256 binding identity derived by the existing canonical source-signature algorithm from the pinned protobuf package, method enum number/name and request/result type signature; schema hashes pin the selected proto/wire shape separately. tool_binding_aliases maps every route to that same binding. Rules intersect exact entry-point tool and schema-bound binding grants plus target/root/user/profile pins.')
s=s.replace('The five class views show','The six class views show')
s+='''

### ARC compatibility evidence and class contract completion

The pinned upstream RemoteControlBridgeClient.SendInputAsync accepts any event list
and sends one unary request; it does not require a press and release in one batch.
The singleton runtime dispatcher retains button state across these requests. Golden
port fixtures therefore include press in requestA, move inB, release inC, along with
the documented local idle/revoke/root-change safety release. A one-batch-only profile
would silently narrow compatibility and is not selected.

WATCH_LOGS preserves the pinned keepalive LogEntry category
Avalonia.RemoteControl.LogStreamKeepAlive. Runtime options default the idle interval
to15seconds; nonpositive interval disables keepalive. The protocol entry carries the
same sequence/time/level/category/message fields as upstream, not a new heartbeat
envelope. It is still subject to authentication, quotas and stream cancellation.
The exact pinned RemoteControlLogBuffer constructor and CreateKeepAliveEntry plus
RemoteControlProtocol constants are golden-fixture sources; log filtering and
dropped_count retain upstream meaning. No undocumented gRPC server is introduced.

McpServer owns protocol dispatch; LoopbackHost owns bounded listener/HTTP parsing.
LoopbackTlsHost implements LoopbackHost. PairingService owns one-use attempts and
the exact-commit pairing path. CatalogAdminHandler checks separate admin kind,
window, owner capability and CAS before the shared repository service. BridgeService
owns those application instances and OutboxDispatcher; the latter owns
CallbackCleanupWorker, JournalNotificationWorker and EpochInvalidationWorker.
Workers consume committed obligation IDs idempotently through SqliteStoreCoordinator;
no worker directly writes a second SQLite connection or bypasses AdmissionGate.
ICallbackAdmissionPlanner maps to InvocationPlanner plus AdmissionGate;
IGroupActivationPlanner maps to FunctionalGroupIndex; ICallbackProxyAdapter maps to
CallbackProxyFactory and its typed adapter result; ICallbackCatalogClassifier is
offline metadata tooling, not a live runtime service or an availability authority.

Last-Event-ID belongs to authenticated MCP SSE transport replay. The default replay
buffer is process-local and loss on restart is explicit; durable application cursors
belong to the callback resource URI/journal. A client may reconnect and perform a
bounded resource read even if the transport event replay window was lost. No SSE
transport event ID becomes an authorization token or an application cursor.

The code-level and dynamic views now also show shutdown and revocation. They describe
proposed runtime behavior; actual coroutine/Android instrumentation is still required.
Ask processing has an owner participant and original monotonic deadline: owner deny,
timeout or failed input quiescence returns a structured denial before dispatch.
'''
p.write_text(s)

p=P/'architecture/code-diagrams.md';c=p.read_text();c=c.replace('  +serviceState\n','  +serviceState\n  +desiredState\n')
c=c.replace('ArcRequestHandler "1" *-- "1" ArcCompatibilityProfile','BridgeService "1" *-- "1" ArcCompatibilityProfile\nArcRequestHandler ..> ArcCompatibilityProfile')
c=c.replace('+watchFramesWithinQuota()','+watchFramesWithinQuota(connectionContext, rootPins)').replace('+watchBoundedEvents(plan)','+watchBoundedEvents(plan, connectionContext)').replace('+handleWatch(request): ResponseFlow','+handleWatch(request, connectionContext): ResponseFlow').replace('+invokeApprovedAction()','+invokeApprovedAction(verifiedPlan)')
c=c.replace('The controller cancels a watch by closing its own watch socket.','The controller cancels a watch by closing its own watch socket.\nZero-length frame is legal; negative/oversize is rejected before allocation.\nUnknown methods (including0) return UNSUPPORTED; Cancel8 remains unsupported.\nFPS field is parsed, not enforced upstream; local quotas are explicit port policy.')
c=c.replace('ArcClientAdapter --> CompatibleExternalArcTarget','ArcClientAdapter --> CompatibleExternalArcTarget')
c=c.replace('class CompatibleArcClient <<external>>','class CompatibleArcClient <<external; C4 Compatible ARC client>>')
c=c.replace('CatalogRepository\nFunctionalGroupIndex ..> AdmissionGate','CatalogRepository\nFunctionalGroupIndex ..> AdmissionGate')
c=c.replace('DeviceViewResolver ..> CatalogRepository','CatalogRepository ..> DeviceViewResolver : committed publication epoch flow\nDeviceViewResolver ..> CatalogRepository')
c=c.replace('McpServer ..> AdmissionGate','McpServer ..> AdmissionGate\nnote bottom of AdmissionGate\nOwner challenge pending: input admission closed, prior input drained.\nARC input/actions and every ADB/raw input path recheck the same gate.\nQuiescence failure means no protected prompt.\nend note',1)
c+='''

## server-adapters-classes — bridge.app transport and committed-work adapters

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Kotlin server host, pairing and committed work — proposed
left to right direction
class BridgeService <<reference: server-core-classes>>
class McpServer <<reference: server-core-classes>>
class CatalogRepository <<reference: server-core-classes>>
class SqliteStoreCoordinator <<reference: server-core-classes>>
class OwnerAuthenticationService <<reference: server-core-classes>>
class ClientAuthenticator <<reference: server-core-classes>>
interface LoopbackHost {
  +bindStartStopAndObserveReadiness()
  +validateHostOriginAndBoundedRequest()
  +routeAuthenticatedSessionAndSse()
}
class LoopbackTlsHost {
  +selectPinnedAndroidKeyStoreIdentity()
}
class PairingService {
  +createScopedOneUseAttempt()
  +validateClientProof()
  +commitExactOwnerReviewedClient()
}
class CatalogAdminHandler {
  +requireAdminKindWindowOwnerAndCas()
  +stageSealedImportOrApplyOverride()
}
class OutboxDispatcher {
  +claimCommittedObligationId()
  +acknowledgeIdempotentDisposition()
}
class CallbackCleanupWorker { +runRecordedVerifiedPlanOnce() }
class JournalNotificationWorker { +coalesceCommittedUriHints() }
class EpochInvalidationWorker { +invalidatePublishedEpochsBeforeNewAdmission() }
LoopbackTlsHost ..|> LoopbackHost
BridgeService "1" *-- "1" LoopbackHost
BridgeService "1" *-- "1" PairingService
BridgeService "1" *-- "1" CatalogAdminHandler
BridgeService "1" *-- "1" OutboxDispatcher
LoopbackHost ..> McpServer : MCP route
LoopbackHost ..> PairingService : optional pair route
LoopbackHost ..> CatalogAdminHandler : disabled by default admin route
PairingService ..> OwnerAuthenticationService
PairingService ..> CatalogRepository
CatalogAdminHandler ..> ClientAuthenticator
CatalogAdminHandler ..> OwnerAuthenticationService
CatalogAdminHandler ..> CatalogRepository
OutboxDispatcher "1" *-- "1" CallbackCleanupWorker
OutboxDispatcher "1" *-- "1" JournalNotificationWorker
OutboxDispatcher "1" *-- "1" EpochInvalidationWorker
OutboxDispatcher ..> SqliteStoreCoordinator
class CallbackBroker <<reference: server-execution-classes>>
class McpCallbackResourceAdapter <<reference: server-execution-classes>>
class DeviceViewResolver <<reference: server-core-classes>>
CallbackCleanupWorker ..> CallbackBroker : recorded cleanup with current authority
JournalNotificationWorker ..> McpCallbackResourceAdapter : committed URI only
EpochInvalidationWorker ..> DeviceViewResolver : publication flow
note bottom of LoopbackHost
Parsing/hosting never decides permission.
Exact credential kind checked before route/session creation.
Android TLS and MCP SDK integration remain implementation gates.
end note
@enduml
```

Host/pair/admin adapters map to transport/auth/catalog in bridge.protocol,
bridge.application and bridge.android; committed workers map to callbacks/device
application services in bridge.application, with persistence in bridge.storage.sqlite.
The platform adapters never implement an alternate authorization system.
'''
p.write_text(c)

p=P/'architecture/workspace.dsl';w=p.read_text()
w=w.replace('        bridge.app.policy -> bridge.app.auth','        bridge.app.policy -> bridge.app.ui "Requests exact bounded Ask challenge after input quiescence; no automatic approval"\n        bridge.app.ui -> bridge.app.policy "Returns exact challenge result or denial; same-process trust is disclosed"\n        bridge.app.auth -> bridge.app.callbacks "Revokes credential-owned subscriptions and delivery"\n        bridge.app.auth -> bridge.app.arc "Revokes inbound streams and input leases"\n        bridge.app.policy -> bridge.app.auth',1)
w=w.replace('            bridge.app.policy -> bridge.app.device "Pin compatible','            bridge.app.policy -> bridge.app.ui "Ask branch: local exact review within original deadline; deny/timeout means no dispatch"\n            bridge.app.ui -> bridge.app.policy "Approve exact challenge or return structured denial; continue only if approved"\n            bridge.app.policy -> bridge.app.device "Pin compatible')
anchor='        deployment bridge "ReferenceAndroid" Deployment'
addition='''        dynamic bridge.app Shutdown "Explicit stop; bounded cleanup before store close" {
            bridge.app.sessionCommands -> bridge.app.lifecycle "Persist desired Stopped; begin orderly stop"
            bridge.app.lifecycle -> bridge.app.transport "Close admission and listener; finish/cancel owned work within deadline"
            bridge.app.lifecycle -> bridge.app.arc "Close ARC listener, watches and logical input leases"
            bridge.app.lifecycle -> bridge.app.callbacks "Suppress delivery; persist only recorded verified cleanup obligations"
            bridge.app.lifecycle -> bridge.app.storage "Drain committed writer work and close coordinator"
            bridge.app.lifecycle -> android "Publish Stopped, stopForeground and service; no automatic restart"
            autoLayout tb
        }
        dynamic bridge.app CredentialRevocation "Exact owner revoke and post-commit epoch invalidation" {
            bridge.app.ui -> bridge.app.ownerCommands "Submit exact credential/revision with fresh owner capability"
            bridge.app.ownerCommands -> bridge.app.catalog "CAS revoke and advance credential/policy epoch in one transaction"
            bridge.app.auth -> bridge.app.catalog "Observe current epoch before next request or session delivery"
            bridge.app.auth -> bridge.app.callbacks "Close delivery; only recorded safe cleanup"
            bridge.app.auth -> bridge.app.arc "Close affected streams and release principal input lease"
            bridge.app.auth -> bridge.app.audit "Record committed revocation outcome without credential bytes"
            autoLayout tb
        }
'''
w=w.replace(anchor,addition+anchor)
w=w.replace('        image bridge.app.arc ArcUiClasses','        image bridge.app ServerAdapterClasses {\n            image ../docs/diagrams/server-adapters-classes.svg\n            title "C4 code-level detail: host, pairing, admin and committed work"\n        }\n        image bridge.app.arc ArcUiClasses')
p.write_text(w)

p=P/'build.gradle.kts';g=p.read_text().replace('"OwnerTransition", "Deployment"','"OwnerTransition", "Shutdown", "CredentialRevocation", "Deployment"').replace('"arc-target-classes")','"arc-target-classes", "server-adapters-classes")');p.write_text(g)
p=P/'README.md';t=p.read_text().replace('35 unique docs SVGs:13 C4 views,5 class views','38 unique docs SVGs:15 C4 views,6 class views');t+='\n\nDeployment lines between host client instances and the APK are logical protocol relationships carried by the explicit owner-established loopback ADB forward, not additional direct network ingress. The on-device agent is a separate-UID local process. Offline tooling/documentation containers represent workstation processes, not APK runtime components; an unlinked documentation instance denotes local build output only. Edit SVG labels through DSL/PlantUML sources and regenerate; textLength is renderer output, not an editor constraint on source.\n';p.write_text(t)
p=R/'catalog/SOURCE-METADATA-CONTRACT.md';t=p.read_text().replace('HTTP semantics follow RFC9110','Status428 is defined by RFC6585 section3; other HTTP semantics follow RFC9110');p.write_text(t)
print(json.dumps({'ledger':'022','current_svgs_expected':38,'class_views':6,'native_c4_views':15,'frozen_reviews_modified':False}))
