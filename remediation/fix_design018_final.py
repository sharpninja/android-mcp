"""Apply final018 design corrections in the working project only; never frozen input."""
from pathlib import Path
import json

R = Path(__file__).resolve().parent
P = R / 'design-c4'
path = P / 'docs/Android MCP Bridge Design.md'
s = path.read_text()
changes = []
def replace(old, new, finding):
    global s
    if old not in s:
        raise ValueError('Missing correction anchor ' + finding + ': ' + old[:100])
    s = s.replace(old, new)
    changes.append(dict(finding=finding, old=old, new=new))

replace('one Kotlin Android APK, application process and UID initially', 'one Kotlin Android APK, application process and UID', 'F-04')
replace('known Binder AIDL interfaces and optional Avalonia.RemoteControl cover narrower integrations', 'known Binder AIDL interfaces and the required internal Kotlin ARC compatibility engine cover narrower integrations', 'F-04')
replace('No target application must embed instrumentation for generic ADB screen/tap control; optional Avalonia Android Runtime/Protocol stays a separate adapter, not desktop Server/Kestrel.', 'No target application must embed instrumentation for generic ADB screen/tap control. The required internal Kotlin ArcEngine and ComposeTargetAdapter retain ARC; ArcClientAdapter supports compatible external instrumented targets separately. No desktop Server/Kestrel or managed runtime is embedded.', 'F-04')
replace('and Avalonia runtime is optional.', 'and the internal Kotlin ARC port is required; compatible external ARC targets are optional.', 'F-04')
replace('Editable source follows.', 'Editable sources: [C4 workspace](../architecture/workspace.dsl) and [scoped class definitions](../architecture/code-diagrams.md).', 'F-24')
replace('![Resolved view and policy-bound generic Binder dispatch](diagrams/structurizr-Startup.svg)', '![Resolved view and policy-bound generic Binder dispatch](diagrams/structurizr-InvocationFlow.svg)', 'F-24')
replace('OpenAPI specification version is1.4.0', 'OpenAPI specification version is1.6.0', 'F-04')
replace('materialization_strategy CHECK the four closed values above', 'materialization_strategy CHECK none/unknown/interface_dynamic_proxy/binder_generic_stub/prebuilt_adapter/unsupported_abstract_class/unsupported_class_subclass; role=proven_non_callback requires none, role=unknown requires unknown, and none is forbidden for callback candidates/proven callbacks; unknown and unsupported strategies require a reason', 'F-03')
replace('Ask the owner to unlock or authenticate locally for sensitive approvals if the chosen security profile requires it.', 'Every Ask decision uses the protected owner flow and fresh strong biometric/device-credential authentication for its exact immutable command. Missing authentication or UI denies; no weaker approval profile is selected.', 'F-05')
replace('Ask policy may require the same mechanism by profile, with timeout/missing UI denying.', 'Every Ask decision requires the same mechanism; timeout, absent authentication or missing UI denies.', 'F-05')
replace('a contract, capability or authority change revalidates or rejects it with stale_contract/revoked/epoch_changed.', 'admitted but not dispatched calls revalidate changed contract/capability/authority pins and may fail stale_contract/revoked/epoch_changed. Already dispatched unary calls complete against their pinned plan, with epoch_changed annotated; a revoked publication right suppresses their payload. Authority removal requests bounded cancellation and reports unknown effect if completion is unproven. Live handles never retarget: publication/read rechecks current authority, and invalidated registration/cleanup contracts terminate delivery and queue only the recorded safe cleanup.', 'F-08')
replace('Deleting a referenced group mapping is rejected while an active view pins it or creates a new revision used only after explicit activation; existing evidence remains retrievable by revision.', 'An owner delete always creates a new tombstone revision after exact owner/CAS validation. Active group-interest views retain their pinned historical revision until explicit set_active or epoch invalidation; this cannot preserve dispatch authority after a policy/catalog invalidation. The next activation resolving a deleted dependency fails unknown_dependency. A remote session cannot block the owner mutation. Existing evidence remains retrievable by revision.', 'F-15')
replace('callback.status input is {subscription_id?:string, renew?:boolean=false, expected_revision?:positive_integer, requested_lease_seconds?:integer}.', 'callback.status input is {subscription_id?:string, renew?:boolean=false, expected_revision?:positive_integer, requested_lease_seconds?:integer, cursor?:string, limit?:integer=50}. limit is1..100. renew=true requires subscription_id, expected_revision and requested_lease_seconds in1..300; list mode cannot renew.', 'F-07')
replace('PUT/DELETE are the mutation family; no PATCH alias is defined.', 'POST (entry creation/import/pair), PUT and DELETE form the mutation family, each with its specific idempotency/precondition contract; no PATCH alias is defined.', 'F-15')
replace('A one-use server nonce plus agent nonce, fingerprint and exact proposed label hash are length-prefixed into HMAC-SHA256 under the random bootstrap secret with domain pair-v1. The secret itself is never sent. Nonces expire after60seconds, are single-use and rate-limited; the response returns the new bearer once over the pinned TLS channel. No secret/proof/credential goes to logs. Approval binds the proposed principal and fingerprint; an expired/conflicting window fails without creating an active client.', 'The owner first authenticates to open a pairing window of at most10minutes, then creates an individual60second attempt. Its local QR/channel payload is {attempt_id, endpoint, server_certificate_sha256, bootstrap_secret, server_nonce, expires_at}; secret and server_nonce are independently random32bytes, base64url without padding. The agent pins the exact certificate SHA-256 before POST, chooses a random32byte agent_nonce and UTF-8 label (at most128bytes, no normalization), and submits {attempt_id, agent_nonce, label, proof}. Let LP(x)=uint32-big-endian byte_length followed by bytes. proof=HMAC-SHA256(bootstrap_secret, LP(UTF8(pair-v1)) || LP(UTF8(attempt_id)) || LP(server_nonce) || LP(agent_nonce) || LP(raw32byte certificate SHA-256) || LP(SHA256(UTF8(label)))). The secret is never sent over the pairing route. After constant-time verification, atomically reserve that attempt and show the exact proposed-label hash and server certificate fingerprint in the owner prompt. This second fresh owner prompt completes within30seconds and before attempt expiry; POST waits at most30seconds. Deny/expiry returns PAIR_DENIED/PAIR_EXPIRED, consumes the attempt and creates no client. Success commits one disabled-by-default-grants principal and returns its bearer once over pinned TLS. Lost success response requires a new pairing attempt; never replay or log the bearer. Cancel/reuse/conflicting attempts fail without another active client.', 'F-06')
replace('Paired HTTPS needs a verified TLS-capable Android engine/tunnel adapter; CIO support alone is not evidence of the required server TLS profile. Until that gate passes, do not ship a sensitive cleartext fallback.', 'The named TLS hosting candidate is a bounded Kotlin LoopbackTlsHost using Android javax.net.ssl.SSLServerSocket/SSLContext with an app-owned certificate key from AndroidKeyStore. It terminates TLS and supplies a bounded HTTP/1.1 request/SSE adapter to McpServer, with keepalive, parsing, header and slow-client limits from LoopbackHost. This candidate is separate from CIO; never assume CIO provides server TLS. The implementation spike must prove AndroidKeyStore KeyManager compatibility, client certificate pinning and the exact MCP transport before enabling paired_tls or /pair. No cleartext sensitive fallback is allowed. [Android SSLServerSocket](https://developer.android.com/reference/javax/net/ssl/SSLServerSocket) documents the API, not this unimplemented host.', 'F-06')
replace('BOOT_COMPLETED/ACTION_USER_UNLOCKED reconciles the mirror', 'BOOT_COMPLETED (manifest receiver) or ACTION_USER_UNLOCKED (runtime-registered receiver only in the already-running direct-boot process) reconciles the mirror', 'F-25')
replace('close/fsync', 'flush/fsync and close', 'F-15')

# Replace an unimplemented optional transport placeholder in place, not by blanket precedence.
lines = s.splitlines()
direct_indices = [i for i,line in enumerate(lines) if 'direct' in line.lower() and 'stream' in line.lower() and ('optional' in line.lower() or 'negotiat' in line.lower())]
changes.append(dict(finding='F-17', reviewed_direct_stream_paragraphs=[i+1 for i in direct_indices]))

addition = '''

## Closed lifecycle, transport and owner-boundary contracts

The following contracts fill specific018 omissions; they do not grant permission or claim an Android implementation. Their classes and interaction views are linked below.

### Callback transition and cleanup contract (FR-CB-03/04)

`cleanup_disposition` is one of `not_required`, `confirmed`, `pending`, `cleanup_unknown`; it is independent of delivery state. Delivery is suppressed before any terminal transition. Each transition increments revision atomically with journal/outbox obligations. Event acceptance while registering is bounded and provisional; publish only after a confirmed registration result. A synchronous callback uses its already-validated local response policy even during registration.

| State/event | Next state / reason | Cleanup disposition and action |
| --- | --- | --- |
| registering + confirmed backend result | active | pending only if a later unregister is required; activate finite handle |
| registering + proven failure with no registration | closed / registration_failed | not_required |
| registering + registration_timeout, disconnect or uncertain result | outcome_unknown / registration_timeout | cleanup_unknown; retain exact provisional cleanup pins, never blindly re-register |
| active + target_died (DeathRecipient/service-connection death) | closed / target_died | not_required only if the exact registered endpoint is proven dead; otherwise pending, then cleanup_unknown on failed cleanup |
| active + lease_expired, callback.cancel, credential revocation, contract invalidation | cancelling | pending; suppress payload immediately, enqueue the exact safe unregister |
| cancelling + confirmed unregister or proven endpoint death | closed | confirmed or not_required, respectively |
| cancelling + bounded cleanup timeout/uncertain side effect | outcome_unknown | cleanup_unknown; no automatic replay of uncertain writes |
| any prior live OS registration + process restart | orphaned | cleanup_unknown; never claim old Binder objects are live or restore consumed approval |
| orphaned + supported cleanup confirmation or exact protected-owner acknowledgement | closed | confirmed or cleanup_unknown respectively; acknowledgement is not proof of unregister |
| active + MCP disconnect | active until lease/policy ends | journal persists within quotas; GET SSE disconnect alone is not callback cancellation |

CallbackBroker.onCalleeDied(token) checks exact endpoint/generation before applying target_died. It does not classify every service disconnect as proof that a remote object died. AC-CB-03/04 fixtures must cover timeout-before/after registration, early callbacks, death, lease expiry, revoke/read races, restart and cleanup uncertainty. SCR05.S4 surfaces these terminal reasons and cleanup confidence.

### Negotiated direct event delivery (FR-CB-05)

The standard finite callback handle/resource-journal path is always available to supported baseline clients. Direct events are a separately authenticated HTTP SSE application extension, not JSON-RPC notifications. Server InitializeResult advertises `capabilities.experimental["android.bridge.directEvents.v1"]={version:1,transport:"authenticated_sse",max_event_bytes:65536,replay:"journal_cursor"}` only when enabled. A client opts in through InitializeRequest.capabilities.experimental with that same key/version/transport; the server confirms the intersection in its result. Absent/mismatched negotiation means journal-only mode, explicitly returned as `{delivery_mode:"resource_journal",direct_stream:null}`; initialization itself remains usable.

After confirmed registration, the finite tool result may additionally return a concrete URL under `/v1/subscriptions/{subscription_id}/events` and delivery_mode `direct_sse`. This is a distinct route on the same pinned-TLS listener, never GET /mcp. GET requires the same kind=mcp bearer, active negotiated MCP session header, exact owned subscription, current epochs/lease/read consent and Origin/Host checks. No credential in a URL. Administrative credentials and foreign/unknown subscription IDs fail without existence disclosure. Client opt-in cannot widen policy. The no-TLS reduced profile and clients without negotiation use the baseline journal instead.

DirectStreamNegotiator owns session negotiation; DirectEventEmitter reads already committed EventJournal events and emits SSE `event: callback`, `id: <opaque application journal cursor>`, `data: <one bounded event envelope>`. The cursor is scoped to subscription/stream_epoch/reader policy; here Last-Event-ID is explicitly an application cursor on this custom route, distinct from transport IDs on /mcp. Initial GET may instead provide one `cursor` query value; conflicting cursor and Last-Event-ID are400. Retention expiry returns410 with sanitized oldest cursor and journal fallback instructions before opening SSE. There is no cross-subscription/global order, exactly-once claim or unbounded final tools/call response.

Use at most one direct stream per subscription/session and two per client, with per-stream queue<=64events/256KiB inside existing client/global quotas. Duplicate open returns409. Read from the committed journal; stream disconnect does not delete journal or cancel registration. A slow reader is closed with a best-effort bounded overflow control event containing no lost payload; durable journal retention/gap policy remains authoritative, and reconnection deduplicates by subscription_id+stream_epoch+sequence. Payload over max_event_bytes uses the already authorized bounded-resource reference, never truncates typed data. Revocation/lease expiry suppresses further payload and closes the stream; callback.cancel owns unregister. No stream opens without successful explicit negotiation, and failure returns structured DIRECT_STREAM_UNAVAILABLE plus baseline events_uri. AC-CB-05 tests must verify absent/mismatched negotiation, bearer/session confusion, foreign IDs, duplicate streams, cursor expiry, overflow, reconnect dedup and revoke between enqueue/write.

### Health and local owner capability

One `HealthSnapshot` owns service_state in {WaitingForUnlock,Starting,Paused,StartRequiresOwnerAction,Running,Stopping,Stopped,Error}, desired_state in {running,stopped}, mcp_listener in {absent,binding,ready,error}, adb_backend in {unpaired,offline,connecting,connected,locked,revoked,unsupported}, device_view in {absent,resolving,ready,stale,staging_failed}, and catalog_readiness in {absent,validating,ready,quarantined,capacity_required}. Notification and every screen's non-action STATUS chip use the same snapshot revision. Running means listener initialized; it does not mean ADB/device_view ready. Starting precedes slow work, failure exposes an owner action on SCR10, and desired_state never fabricates actual state. These are display labels for the closed stored enum values, not additional hidden states.

OwnerAuthenticationService is also the OwnerCapabilityIssuer role: after successful protected BiometricPrompt it creates a single-use in-process capability bound to nonce, exact canonical command hash, expected revisions, foreground Activity identity and monotonic expiry<=30seconds. No CryptoObject signature is claimed: the reference uses one trusted process and an unexported facade, so capability storage and consumption remain service-owned memory. OwnerCommandFacade verifies and consumes it at the same serialized commit boundary; CatalogRepository.commitOverride rejects changed command/revision and cannot be called as an alternate UI bypass. Protected windows use FLAG_SECURE and exclude overlay/bridge remote paths. All Ask operations require this flow. Foreign privileged controllers already authorized by adbd remain outside the trust boundary; inability to isolate bridge-dispatched work blocks approval rather than claiming it is safe. Screen text says bridge-dispatched input is paused, never all device input.

### ADB connection and reason-code boundaries

AdbConnectionManager owns one paired same-device ADB identity/session per selected profile. The reference implementation choice is a Kotlin coordinator with a bundled app-private native AOSP ADB pairing/connect client behind a bounded JNI adapter; ABI, build/version/license and Android packaging compatibility are explicit implementation gates, not an installed binary or Kotlin reimplementation claim. It exposes only reviewed fixed operations, not arbitrary shell via a screen grant. Locked means the app cannot unwrap its paired key before user unlock; unlock permits Connecting, never silently repairs or pairs. `verification_strength` CHECK unverified/paired_peer_verified; `identity_verification_method` CHECK none/adb_tls_pairing; connected requires the latter two positive values. Revoke clears the usable key and epoch. Wireless debugging unavailable/offline Wi-Fi returns an explicit environment reason, with no settings change.

Reason envelopes separate `layer` (inventory/applicability/acquisition/authority/codec/transport/policy) from `code` (stable uppercase symbolic enum); arbitrary colon/dot concatenations are not wire codes. Existing specific version codes such as PLATFORM_APPLICABILITY_UNPROVEN remain in that enum. Legacy `backend_unreachable:non_sdk` maps to {layer:acquisition,code:NON_SDK_BLOCKED}; `backend_unreachable:hidl` maps to {layer:acquisition,code:HIDL_BACKEND_UNAVAILABLE}; OS_UNAVAILABLE is authority, not a version claim. Add PACKAGE_NOT_VISIBLE, BIND_PERMISSION_MISSING, ADB_WIFI_UNAVAILABLE and NOT_ON_BRIDGE_CLASSPATH with acquisition/authority/transport/codec emitting layers respectively. The exhaustive versioned code registry preserves old codes and their meanings; unknown future codes remain visible with layer and reason text and deny invocation, never parse a free-text suffix as authority. App package visibility may hide an installed target; this is unknown, not proof of absence. The bridge does not request QUERY_ALL_PACKAGES. Reviewed <queries> manifest declarations are build-time only; an owner may select among supported declared targets but cannot dynamically edit its manifest. Unsupported package visibility stays unknown with PACKAGE_NOT_VISIBLE. Public bindService guarded by a missing permission reports BIND_PERMISSION_MISSING; a catalog SDK member absent from the bridge classloader reports NOT_ON_BRIDGE_CLASSPATH. No permission grant manufactures reachability. [Package visibility](https://developer.android.com/training/package-visibility/declaring) and [ADB](https://developer.android.com/tools/adb) are platform inputs, not universal availability guarantees.

### Hashes, catalog administration and import staging

The administrative entry ETag is exactly the existing cat1 ETag; dv2, group-view and meta1 ETags belong to their separately named representations. Immutable source and SQLite file SHA-256 values hash original bytes. Catalog effective-document hashes use the checked Python canonical profile already implemented by catalog tooling; the exact meta-json-v1 UTF-8/sorted-codepoint/finite-number profile and literal byte vectors in SOURCE-METADATA-CONTRACT.md define cross-language reproduction, including 1 versus1.0 and negative zero. No implicit RFC8785 or Unicode normalization is claimed. Entry source-signature IDs retain the catalog's versioned language-specific signature namespaces: source identities are never silently replaced by a JVM descriptor. A separately proven erased JVM descriptor maps to that identity for dispatch, with collision/ambiguity rejected before activation. Kotlin must reproduce the pinned profile through golden bytes/hashes, not its default JSON serializer; fixtures cover Unicode, escapes, integers, floating exponents, API metadata aliases and conflicting signatures.

An owner enters independently obtained expected SHA-256 and size (or chooses an explicitly pretrusted signed source manifest) before creating an import stage. POST /v1/catalog/import-stages accepts those values and a bounded multipart file stream under the admin window and owner capability. The service copies to a private file, seals its bytes, validates schema/capacity, and returns an opaque staged handle; GET /v1/catalog/import-stages lists only that admin principal/window's sanitized handles. A stage expires at min(created+15minutes, management-window end), or on credential/window revocation; expiry deletes unactivated staging through bounded cleanup. Final import POST consumes the still-sealed handle with fresh exact owner authorization, expected catalog revision and Idempotency-Key. Local SAF follows the same application service without HTTP/admin credentials. Hold a transient SAF read grant only during copying/hash verification, then release it; no persistent URI permission is required. The allowlisted SQLite schema/quarantine contract above applies before any imported content query.

### MCP transport edge cases

An unknown/expired Mcp-Session-Id returns HTTP404 and the client must initialize a new session; a missing required post-initialize session header returns400. Unsupported MCP-Protocol-Version returns400. A missing version header follows the pinned specification's2025-03-26 default for legacy requests; it does not silently negotiate2025-11-25, and incompatible methods fail the protocol gate. The server implements GET SSE; if explicitly disabled it returns405 and resource journal polling remains possible. The client authenticates every connection. A bounded per-session128notification/256KiB queue coalesces URI updates, drops oldest hints under pressure and records a gap counter; payload loss is governed by journal policy, not hint delivery. Each notification is sent to one connection, not broadcast across streams.

Malformed JSON-RPC structure/unknown method/invalid method parameters follow JSON-RPC errors. For tools/call, a structurally valid call with invalid tool arguments follows MCP tool-error semantics (`isError=true`), preserving the pinned tool guidance rather than treating every application validation error as -32602. Transport disconnect alone does not cancel admitted work. A disconnected unary response is retained only as sanitized execution/audit outcome and not replayed on GET SSE; clients use explicit owned status/idempotency recovery. The server's bounded execution deadline is defined by the selected tool schema/caps, not a fictional standard MCP deadline header. Write adapter input may define idempotency_key; duplicates return prior known outcome or DUPLICATE_OPERATION/unknown without re-executing uncertain effects. Subscription IDs use lowercase [a-z0-9-]{1,64}; the exact advertised URI is canonical. Foreign/unknown resources both return resource-not-found without existence distinction. Base events_uri returns the concrete next-read URI; resources/templates/list publishes the separate URI template. [Pinned transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports).

### Remaining bounded platform details and traceability

An exact schema_hash mismatch makes a client binding grant inert and visibly re-grant-required. No automatic rebind is chosen even for graph-only changes; owner preview creates a new exact rule. Capability generation increments only on semantic observation change, not Activity resume/probe time. Entry-point tools are the enumerated bootstrap/wrapper operations; they only restrict canonical grants. Approval state includes Reserved between Approved and Consumed; a crash before confirmed dispatch maps execution to unknown. Fingerprints show at least12hex characters of SHA256(UTF8(credential_id)), with full hash in details; this display is not server TLS identity. Mandatory admission audit failure denies dispatch; optional logs use the serialized writer outside the decision gate and are evicted first.

screen.state displayId is the logical DisplayManager ID on the reference Android15/API35+ floor. `coordinateSpaceVersion` is `display-pixels-v1` for uncropped physical-pixel output or `image-transform-v1` with a complete affine transform to that space. The reviewed platform adapter must prove logical-to-physical display mapping for screencap versus input using the same display snapshot, or report DISPLAY_MAPPING_UNPROVEN; no assumption that both flags accept the same ID. Target bounds come from the same authorized screen.state observation's fixed reviewed window/display query and verified target identity; unavailable/racing evidence rejects target-scoped tap. Whole-display authority is a separate explicit scope, never fallback. Retain at most8capture references/client with least-recent eviction and30second expiry, bounded by the global capture budget. Brightness read observation is {kind:integer,rawValue,withinDocumentedRange} or {kind:absent|unparseable,anomaly,rawDiagnostic?}; raw diagnostics are bounded/sanitized and never a fabricated value.

group_dependencies has an acyclic publication constraint; lifecycle_units pins atomic registration/unregister/type membership; prebuilt_adapters stores ID/version/SHA256/supported methods; cancellations has UNIQUE(client_id,cancellation_id) and sanitized idempotent outcome. connection_profiles rejects profile_id LIKE 'platform:%'; the reserved platform:local route is not a user profile. executions.backend_kind uses the same canonical self_adb/arc_target/native domain as its actual execution backend, never ingress-only external_adb. Legacy avalonia_runtime/executor avalonia rows are migrated explicitly to arc_target after contract verification, not silently interpreted as a managed runtime. runtime fgs_type records the observed last start type and eligibility outcome. Outbox consumers are the callback cleanup worker, committed-journal notification worker and epoch invalidation worker, all idempotent on obligation IDs.

Memory budgets are proposed/unmeasured: Java heap<=min(192MiB,memoryClass), no largeHeap; native allocations<=128MiB; total measured service PSS<=384MiB including graphics attributable to it. The lower page/scratch caps remain sub-budgets. Cache seal is HMAC-SHA256 with a non-exportable AndroidKeyStore key without user-authentication requirement so eligible headless startup can verify it; credential/grant reveal retains fresh owner authentication. Proxy Object methods (equals/hashCode/toString) execute locally and never become callback events. Binder callback stubs implement reserved interface version/hash meta transactions from their pinned contract; missing proof reports unsupported, not guessed values.

FR-DS-01 / TR-DS-01 / AC-DS-01 trace negotiated DirectStreamNegotiator/emitter, fallback, cursor and revocation tests. FR-OWN-02 / TR-OWN-02 / AC-OWN-02 trace OwnerAuthenticationService capability consume/CAS, foreign-input boundary and protected SVG states. FR-LC-02 / TR-LC-02 / AC-LC-02 trace HealthSnapshot, ForegroundStatusNotifier, boot/unlock and callback transition table. FR-ARC-03 / TR-ARC-03 / AC-ARC-03 trace the ArcSession dynamic view, codec/default/cancel tests and exact upstream interoperability gate. These are acceptance plans until executed on Android.

![ARC one-request and stream lifetime](diagrams/structurizr-ArcSession.svg)

![Protected owner command and commit](diagrams/structurizr-OwnerTransition.svg)
'''
s += addition
path.write_text(s)
(R / 'evidence/design018-final-prose-changes.json').write_text(json.dumps(changes, indent=2)+'\n')

model = P / 'architecture/workspace.dsl'
t = model.read_text()
anchor = '        bridge.app.arcController -> bridge.app.policy "Checks client target grants before forwarding"'
t = t.replace(anchor, anchor + '''
        bridge.app.callbacks -> android "Executes only the recorded verified cleanup plan"
        bridge.app.auth -> bridge.app.catalog "Reads credential/client mappings and current epochs"
        bridge.app.auth -> bridge.app.audit "Records bounded authentication/session/revocation events"
        bridge.app.ownerCommands -> bridge.app.lifecycle "Reads health and requests protected desired state"
        bridge.app.ownerCommands -> bridge.app.callbacks "Inspects metadata and explicitly cancels owned registrations"
        bridge.app.ownerCommands -> bridge.app.device "Requests observed capability refresh; no asserted facts"
        bridge.app.catalog -> bridge.app.device "Invalidates on committed catalog or override publication"
        android -> bridge.app.lifecycle "Delivers eligible boot/unlock/lifecycle events"
        bridge.app.groups -> bridge.app.transport "Returns atomic view epoch and best-effort list_changed"
        bridge.app.arc -> arcClient "Sends one result or bounded watch responses with matching ID/version"
        bridge.app.ownerCommands -> bridge.app.ui "Returns protected command result or explicit failure"
''')
t = t.replace('        deployment bridge "ReferenceAndroid" Deployment', '''        dynamic bridge.app ArcSession "ARC one request per socket; compatible stream lifetime" {
            arcClient -> bridge.app.arc "Open authenticated raw loopback TCP; one length-prefixed request"
            bridge.app.arc -> bridge.app.policy "Validate principal/version/method and exact canonical operation"
            bridge.app.arc -> bridge.app.ui "Access only selected unprotected root after admission"
            bridge.app.arc -> arcClient "Unary result OR repeated watch responses with same ID/version; bounded payload"
            bridge.app.arc -> arcClient "On normal completion: terminal end_of_stream=true then close"
            arcClient -> bridge.app.arc "Cancel watch by closing its socket; Cancel8 RPC remains unsupported"
            bridge.app.arc -> bridge.app.audit "Release per-connection input/streams; record sanitized outcome"
            autoLayout tb
        }
        dynamic bridge.app OwnerTransition "Fresh owner capability bound to exact CAS command" {
            owner -> bridge.app.ui "Choose protected command and inspect exact scope/revisions"
            bridge.app.ui -> bridge.app.ownerCommands "Request nonce and protected authentication challenge"
            bridge.app.ownerCommands -> bridge.app.policy "Validate fresh owner capability and current command/revisions"
            bridge.app.ownerCommands -> bridge.app.catalog "Verify-and-consume capability at serialized CAS commit; reject mismatch"
            bridge.app.ownerCommands -> bridge.app.ui "Return committed revision or denial/conflict; no remote self-approval"
            autoLayout tb
        }
        deployment bridge "ReferenceAndroid" Deployment''')
t = t.replace('"Authenticated MCP"', '"MCP2025-11-25 Streamable HTTP over authenticated loopback TCP/owner adb forward"')
t = t.replace('"Same retained ARC protocol"', '"ARC raw TCP; signed4byteBE; envelope1.0; loopback"')
t = t.replace('"Reads/imports catalog and atomically persists config, views, journal and audit"', '"Reads catalog/policy/groups/views; writes overlays/journal/audit via one serialized writer connection"')
t = t.replace('"Owns transactional writes and read-only snapshots"', '"Owns table transactions through the single serialized writer; read-only snapshots"')
t = t.replace('"Screen definitions, grants, group/catalog/log/subscription management"', '"Screen definitions and selected unprotected ComposeTargetAdapter root; management/protected roots excluded"')
model.write_text(t)

code = P / 'architecture/code-diagrams.md'
t = code.read_text().replace('+readResource(uri, cursor, limits)', '+readResource(principal, uri, cursor, limits)').replace('+readBoundedPage(principal, cursor)', '+readBoundedPage(principal, uri, cursor, limits)')
t = t.replace('  +recoverOrphanedRegistrations()', '  +recoverOrphanedRegistrations()\n  +onCalleeDied(token)')
t = t.replace('class McpSessionRegistry {', '''class DirectStreamNegotiator {
  +negotiate(clientCaps, serverCaps): AgreedProfile
  +requireOwnedSession(principal, session)
}
class DirectEventEmitter {
  +openOwnedCommittedCursor(request)
  +closeOnOverflowRevocationOrLease()
}
McpServer ..> DirectStreamNegotiator
DirectStreamNegotiator ..> DirectEventEmitter
class HealthSnapshot {
  +serviceState
  +mcpListener
  +adbBackend
  +deviceView
  +catalogReadiness
}
class ForegroundStatusNotifier {
  +renderSameSnapshot(health)
}
class BootCompletedReceiver {
  +recordWaitingForUnlock()
  +requestEligibleStart()
}
BridgeService ..> HealthSnapshot
BridgeService "1" *-- "1" ForegroundStatusNotifier
BootCompletedReceiver ..> BridgeService
class McpSessionRegistry {''')
t = t.replace('  +commitOverride(command, expectedRevision)', '  +commitOverride(command, expectedRevision, ownerCapability)')
t = t.replace('OwnerCommandFacade ..> OwnerAuthenticationService', 'OwnerCommandFacade ..> OwnerAuthenticationService : verify and consume exact command capability\nSqliteCatalogRepository ..> OwnerAuthenticationService : same serialized CAS boundary')
t = t.replace('interface EventJournal {', 'class DirectEventEmitter <<reference: server-core-classes>>\nDirectEventEmitter ..> EventJournal : committed events only; same quotas and read authority\ninterface EventJournal {')
t = t.replace('  +state: RegistrationState', '  +state: RegistrationState\n  +cleanupDisposition: CleanupDisposition')
code.write_text(t)

build = P / 'build.gradle.kts'
t = build.read_text().replace('"GroupFlow", "Deployment")', '"GroupFlow", "ArcSession", "OwnerTransition", "Deployment")')
t = t.replace('inputs.files(fileTree("docs"), "architecture/workspace.dsl", "architecture/code-diagrams.md")', 'inputs.files(fileTree("docs"), "README.md", "renderer-lock.json", "architecture/workspace.dsl", "architecture/code-diagrams.md", "storyboards/storyboard.md")')
t = t.replace('fileTree("tooling"), "renderer-lock.json")', 'fileTree("tooling"), "README.md", "build.gradle.kts", "settings.gradle.kts", "gradle.properties", "gradlew", "gradlew.bat", "toolchain-lock.json", "renderer-lock.json")')
build.write_text(t)

readme = P / 'README.md'
t = readme.read_text().replace('Other existing Java21/Node environments can use the same tasks with STRUCTURIZR_CLI and NODE_BIN set to verified local tooling.', 'The strict renderer-lock.json profile checks the exact JDK21.0.8, Node26.8.2, renderer jar hashes and committed/cached Marp lock. An environment mismatch fails rather than claiming byte reproducibility. Alternate runtimes require an explicitly reviewed new lock/profile.')
t += '\nCanonical UI source is ui-svg/*.svg; generateUiAssets verifies every manifest ID and copies identical bytes to docs/screens. Current package has34 unique docs SVGs:13 C4 views,5 class views,11 screens and5 storyboards. No Android application is generated.\n'
readme.write_text(t)

for svg in (P / 'ui-svg').glob('SCR*.svg'):
    t = svg.read_text()
    # Shared non-action AppBar chip, kept clear of page title and annotation panel.
    chip = '<g data-status-id="'+svg.stem+'.STATUS"><rect x="1060" y="18" width="180" height="42" rx="8" fill="#e4f2e8"/><text x="1072" y="44" font-size="12" fill="#153d25">MCP Running · snapshot r8</text></g>'
    t = t.replace('</g></svg>', chip+'</g></svg>')
    svg.write_text(t)

manifest = P / 'docs/ui-asset-manifest.json'
d = json.loads(manifest.read_text())
d['shared_status'] = dict(id_pattern='SCRxx.STATUS', action=False, source='HealthSnapshot', fields=['service_state','mcp_listener','adb_backend','device_view','catalog_readiness'], protected_surface='read-only; no implicit navigation/approval')
manifest.write_text(json.dumps(d,indent=2)+'\n')
print('Applied targeted working corrections; frozen018 untouched')
