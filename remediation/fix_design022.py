"""Reconcile reproduced022 design defects; immutable reviews remain untouched."""
from pathlib import Path
import re,json,hashlib
R=Path(__file__).resolve().parent;P=R/'design-c4';p=P/'docs/Android MCP Bridge Design.md';s=p.read_text()
replacements={
 'kind CHECK mcp/admin;':'kind CHECK mcp/admin/arc;',
 'tools.executor_kind CHECK native/adb/arc_target':'tools.executor_kind CHECK native/self_adb/arc_target',
 'fingerprint 6F2A':'fingerprint 6F2A91B40C78',
 'Five ordered storyboards cover':'Six ordered storyboards cover',
 'all13slides are present':'all 25 slides are present',
 '`manual_bearer` is the interoperable static-header path':'`manual_bearer_setup` is the interoperable static-header path',
 '`paired_tls` is the optional helper path':'`pairing_helper_v1` is the optional helper path over the paired_tls transport profile',
 'ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS':'CLASS_NO_RUNTIME_SUBCLASS (sub_reason=abstract/concrete/final as applicable)',
 'Both device-wide ADB input and Avalonia injected input':'Both device-wide ADB input and ARC-injected input',
 'specialized Avalonia runtime designs':'ARC compatibility designs',
 'registration, replacement and cleanup binding IDs':'registration and cleanup binding IDs',
 'role and confidence; argument direction':'role and provenance-qualified evidence; argument direction',
 'dependencies and optional related groups':'dependencies',
 'signing_identity nullable; identity_verification_method;':'signing_identity nullable; identity_verification_method CHECK none/package_manager_signing;',
 '`identity_verification_method` CHECK none/adb_tls_pairing':'`connection_profiles.peer_identity_verification_method` CHECK none/adb_tls_pairing',
 'bounded input state and frame/log streams; closing it releases its pressed state.':'bounded frame/log streams; closing it releases only those per-request resources, not a logical controller input lease.',
 'Tree, frame and input pin one root generation and transform.':'Tree/node operations pin a root generation. SEND_INPUT has no wire generation field: the server snapshots the current root/transform at admission and serializes the batch against it; stale-frame rejection is not claimed for coordinate input.',
 'it rejects nonfinite/out-of-root values, stale node/frame targets and unsupported key/property actions.':'it rejects nonfinite/out-of-root values, stale node-ID targets and unsupported key/property actions. Raw SEND_INPUT targets the current root; the retained wire cannot prove which frame the client intended.',
}
for a,b in replacements.items():s=s.replace(a,b)
# Disambiguate both uncertainty histories in the actual transition contract.
s=s.replace('| outcome_unknown +','| outcome_unknown (uncertain_phase=registration) +')
s=s.replace('cleanup outcome. Index(client_id,state,lease_expires_at).','cleanup outcome; uncertain_phase CHECK registration/cleanup nullable, required exactly when state=outcome_unknown. Index(client_id,state,lease_expires_at).')
s=s.replace('| cancelling + bounded cleanup timeout/uncertain side effect | outcome_unknown |','| cancelling + bounded cleanup timeout/uncertain side effect | outcome_unknown (uncertain_phase=cleanup) |')
s=s.replace('| registering + registration_timeout, disconnect or uncertain result | outcome_unknown / registration_timeout |','| registering + registration_timeout, disconnect or uncertain result | outcome_unknown (uncertain_phase=registration) / registration_timeout |')
# Insert into the same authoritative table, not a disconnected alternate machine.
needle='| outcome_unknown (uncertain_phase=registration) + another timeout/uncertain observation | remain outcome_unknown, delivery closed; bounded obligation/status survives until reconciliation/retention, never an unbounded retry loop |'
s=s.replace(needle,needle+'\n| outcome_unknown (uncertain_phase=cleanup) + confirmed unregister success | closed, cleanup_disposition=confirmed; retain terminal cursor/status |\n| outcome_unknown (uncertain_phase=cleanup) + confirmed unregister failure or further uncertainty | remain outcome_unknown, cleanup_unknown; no guessed retry or registration resurrection |\n| outcome_unknown (uncertain_phase=cleanup) + confirmed endpoint death | closed, not_required; endpoint/generation evidence retained |')
s=s.replace('verification_strength; peer_fingerprint','verification_strength; peer_identity_verification_method CHECK none/adb_tls_pairing; peer_fingerprint')
# The exact table can use different separators; the normative profile rule below
# explicitly defines its owner in either case and rejects conflated target checks.
s+='''

## Runtime design consistency: identities, lifecycle and ARC input

The live credentials table domain is mcp/admin/arc. MCP, admin and ARC authenticators
require their exact kind before session creation; routes never reinterpret a token.
The target-app identity method is none or package_manager_signing and verifies the
selected package/user/signing lineage. The connection_profiles field
peer_identity_verification_method is none or adb_tls_pairing; only a paired verified
peer can be Connected. These are separate facts, not names for the same column.
The earlier offline bridge-config.sql is a legacy fixture superseded by these live
CHECK domains and the ordered migration; it is not current Android runtime DDL.

ARC policy identities use a reserved catalog namespace arc-protobuf-v1:method:N
for all enum values 1..10, with the immutable pinned proto hash, method name and
typed request/result hash. Value0/unknown methods are unsupported, never granted.
The full method set is capabilities1, snapshot2, tree3, click4, focus5, property6,
logs7, cancel8, frames9, input10; cancel8 remains unavailable regardless of grant.
Rules attach to those canonical identities and exact target/root/user/profile pins.
MCP ArcTargetBackend and local controller forwarding use these same rule identities,
schema pins, audit and AdmissionGate; wrapper tools cannot widen them. Native
inbound ARC uses the token's registry principal. The ordinary local controller uses
reserved principal local-owner-controller, created only by the unexported local
Activity binding; its owner-configured target/action allowlist defaults deny.
This principal cannot invoke protected owner mutations or manufacture an approval.
Capabilities can expose denied/unsupported operations truthfully but never enable them.

ArcTargetBackend implements BackendAdapter, delegates to ArcSessionManager and the
same ArcClientAdapter profile, and is the explicit MCP-to-compatible-ARC execution
path. A host forwarding profile remains ingress only. Forwarding and execution
identity are never interchangeable.

Pinned upstream runtime DI registers RemoteControlInputDispatcher as a singleton
(AvaloniaRemoteControlRuntimeServiceCollectionExtensions.cs:61), with pointer and
left-button state retained on that dispatcher (Input/RemoteControlInputDispatcher.cs).
The Kotlin compatibility port therefore does not release a press merely because a
one-request unary socket closes. ArcInputLeaseRegistry owns exclusive selected-root
control leases keyed by authenticated principal, credential epoch and root epoch;
the key is internal and adds no protobuf field. Input batches for that lease are
serialized on UiDispatcher, and press/move/release may span unary requests. Other
principals receive a bounded busy/denied CommandResult, never shared pressed state.
An explicitly configured idle deadline (default5seconds since the last accepted
input), explicit local controller disconnect, credential revocation, root replacement,
protected-owner challenge and service shutdown release pressed state and end the
lease. A remote TCP disconnect cannot distinguish idle client from unary completion;
the idle bound is a documented port safety policy, not an upstream wire feature.
An outbound Kotlin controller sends best-effort releases before disconnecting;
network failure falls back to the target's lease bound. WATCH socket close cancels
that stream only. No Cancel8 implementation or wire lease ID is invented.

SEND_INPUT has no frame/generation field. Its coordinates apply to the current
selected root and density captured atomically for that admitted batch. Tree node
IDs reject stale generations; coordinate input cannot reject a stale client frame.
The client can compare observed frame sequence/root geometry before sending but
this is advisory; a concurrent target change returns a failure when detected before
dispatch. Successful input does not claim proof of the client's intended frame.

OwnerAuthenticationService owns RemoteInputQuiescer. Before exposing a protected
prompt it closes ARC/ADB input admission, waits for already-dispatched bridge input
to finish and releases ARC leases. If bounded quiescence cannot be proven, no prompt
is shown and the challenge fails. ARC click/focus/property/text and generic/raw paths
that can inject input are gated too; unclassified side-effecting raw/ADB requests
are denied while a challenge is pending. Independent foreign controllers remain
outside this guarantee. Local owner touch and OS authentication remain available.
Concurrent Ask requests enter a bounded owner queue; one fresh exact challenge is
visible at a time, remaining callers keep their original monotonic deadlines.
Notification says approval pending and opens the local review, never approves it.
Overflow, dismissal and deadline expiry return denial without consuming another
request's approval. Removing the final pending challenge reopens ordinary admission.

HealthSnapshot includes arc_listener=disabled/binding/ready/error separately from
mcp_listener and adb_backend. The notification and AppBar render the same revision:
service state plus MCP state, ADB state, device-view freshness, catalog readiness and
ARC listener state; concise chip opens full details. Error/Stopped never says Running.
The stopped-service start path is an explicit eligible foreground-service start
intent from the local Activity; the in-process facade handles stop/status once live.

Approval Reserved is entered atomically from Approved at admission-gate entry,
Consumed at the dispatch/commit boundary, or Terminal on expiry, revocation, CAS
failure or persistence failure; it cannot be reused. Local SAF import stages expire
15minutes after copy begins, including copy/validation time, or earlier owner cancel;
HTTP stages additionally expire with their management window. Neither path resumes.
cursor_expired, unsupported_synchronous_reply, buffer_overflow and durability_failed
are lifecycle reason values; normalized error envelopes use their uppercase aliases.
For invalid ARC envelope version or empty request ID, a bounded ERROR response uses
server version1.0 and echoes the received ID (empty if absent), then closes. Clients
reject mismatched IDs/versions even before interpreting the error. Golden fixtures
cover both errors. No sensitive target access precedes authentication/validation.

android.groups.describe accepts group_id, exact revision/hash, optional opaque
cursor and max_bytes bounded by the configured client limit. It returns that pinned
group's bounded manifest page, canonical member/type/cleanup references, next_cursor,
serialized_bytes and an explicit oversize-item reason. It grants no authority and
does not spill schemas into discovery summaries. During Resolving, authenticated
groups.discover/describe, bindings.search/list/get and callback.status/cancel may
return inventory/status with unavailable/unknown reasons; set_active and binding.invoke
reject stale/not-ready views. There is no undeclared policy/status bootstrap tool.
The contacts.list example assumes an explicitly negotiated active dynamic alias;
portable clients use binding.invoke with the canonical binding ID.

CallbackDescriptors and FunctionalGroups v1 store only fields in their pinned schemas;
replacement relationships or group recommendations without schema fields are search
evidence, never claimed persisted v1 properties. SqliteCatalogRepository implements
commitSessionView(session,desiredSet,expectedEtag) independently of owner override
mutation; service-authenticated session ownership is mandatory. Catalog publication
Flow invalidates DeviceViewResolver and group pins before new dispatch admission.
'''
# Replace old input-lifetime claims everywhere rather than claim append wins.
s=s.replace('closing it releases its pressed state','closing it releases its stream resources')
s=s.replace('No design-only score is substituted for catalog/source completeness.','Design-only acceptance excludes catalog/source population completeness, per the latest user scope.')
s=s.replace('no design-only score is substituted for catalog/source completeness.','Design-only acceptance excludes catalog/source population completeness, per the latest user scope.')
p.write_text(s)

p=P/'architecture/code-diagrams.md';c=p.read_text()
c=c.replace('Each block identifies its scoped C4 component.','Server blocks are app-scoped cross-component code views; the crosswalk below maps owning components/projects. Other blocks identify their scoped C4 components.')
c=c.replace('+openNotificationStream(session)','+openNotificationStream(session, lastEventId)\n  +validateOrigin(request)')
c=c.replace('  +catalogReadiness\n','  +catalogReadiness\n  +arcListener\n')
c=c.replace('  +activateSealedView(pins)\n','  +activateSealedView(pins)\n  +commitSessionView(session, desiredSet, expectedEtag)\n  +observePublications(): Flow\n')
c=c.replace('  +activateCAS(expectedView, stagedView)\n','  +activateCAS(expectedView, stagedView)\n  +onPublication(epoch)\n')
c=c.replace('  +verifyAndConsume(capability, command, revisions)\n','  +verifyAndConsume(capability, command, revisions)\n  +quiesceRemoteInputWhilePending(challenge)\n')
c=c.replace('class SessionCommandFacade {','class RemoteInputQuiescer {\n  +closeInputAdmissionAndDrain(deadline)\n  +reopenAfterLastChallenge()\n}\nOwnerAuthenticationService "1" *-- "1" RemoteInputQuiescer\nRemoteInputQuiescer ..> AdmissionGate : denies every bridge-controlled input path\nclass SessionCommandFacade {',1)
c=c.replace('class SdkBindingBackend {\n  +preflight(plan): SupportedOrReason\n  +dispatch(admission): BoundedResult','class SdkBindingBackend {\n  +preflight(plan): SupportedOrReason\n  +invoke(verifiedPlan): Outcome\n  +observeCapabilityEpoch()')
c=c.replace('BinderBackend ..> BinderHandleProvider','BinderBackend "1" *-- "1" BinderHandleProvider').replace('BinderBackend ..> WireContractVerifier','BinderBackend "1" *-- "1" WireContractVerifier')
c=c.replace('CallbackBroker ..> LocalReplyPolicy','CallbackBroker "1" *-- "1" LocalReplyPolicy')
c=c.replace('  +cleanupDisposition: CleanupDisposition','  +uncertainPhase: RegistrationOrCleanup\n  +cleanupDisposition: CleanupDisposition')
c=c.replace('class CallbackBroker {','class CallbackProxyFactory {\n  +forInterface(descriptor)\n  +forClass(descriptor): PrebuiltStubOrReason\n  +forBinderEndpoint(descriptor): VerifiedStubOrReason\n}\nclass CallbackBroker {',1)
c=c.replace('CallbackBroker "1" *-- "0..*" Subscription','CallbackBroker "1" *-- "0..*" Subscription\nCallbackBroker "1" *-- "1" CallbackProxyFactory\nclass AdmissionGate <<reference: server-core-classes>>\nCallbackBroker ..> AdmissionGate : recheck event/reply/cleanup authority')
c=c.replace('InvocationPlanner ..> BackendAdapter','class ArcTargetBackend {\n  +invoke(verifiedPlan): Outcome\n  +observeCapabilityEpoch()\n}\nclass ArcSessionManager <<reference: arc-controller-classes>>\nArcTargetBackend ..|> BackendAdapter\nArcTargetBackend ..> ArcSessionManager\nInvocationPlanner ..> BackendAdapter',1)
c=c.replace('BridgeService "1" *-- "1" SdkBindingBackend','BridgeService "1" *-- "1" SdkBindingBackend\nBridgeService "1" *-- "1" ArcTargetBackend')
c=c.replace('class CompatibleExternalRuntime','class CompatibleExternalArcTarget').replace('--> CompatibleExternalRuntime','--> CompatibleExternalArcTarget')
c=c.replace('class CompatibleArcClient','class CompatibleArcClient').replace('  +parseRequest(bytes): BridgeRequest','  +serializeRequest(request): ByteArray\n  +parseResponse(bytes): BridgeResponse\n  +parseRequest(bytes): BridgeRequest')
c=c.replace('+validateIdVersionStatusAndCommandResult()','+validateCommandResult()')
c=c.replace('ArcClientAdapter ..> ArcFrameCodec','class ArcCompatibilityProfile <<reference: arc-ui-classes>>\nArcClientAdapter ..> ArcCompatibilityProfile : validate ID/version/status\nArcClientAdapter ..> ArcFrameCodec',1)
c=c.replace('  +closeAndReleaseOwnedInput()','  +closeOwnedStreams()')
c=c.replace('  +sendInput(rootGeneration, events)','  +sendInput(principalContext, currentRoot, events)')
c=c.replace('ArcConnectionSession "1" *-- "0..1" ArcInputDispatcher','class ArcInputLeaseRegistry {\n  +acquireExclusive(principal, rootEpoch)\n  +releaseOnIdleRevokeRootChangeOrOwnerChallenge()\n}\nclass BridgeService <<reference: server-core-classes>>\nBridgeService "1" *-- "1" ArcInputLeaseRegistry\nArcInputLeaseRegistry "1" *-- "0..1" ArcInputDispatcher')
c=c.replace('+releasePressedStateOnClose()','+releasePressedStateOnLeaseEnd()')
c=c.replace('Per-connection input state/streams; close releases owned pressed state.','Per-connection frame/log streams close with socket.\nInput lease spans unary sockets; no wire lease ID or frame generation.')
c=c.replace('ComposeTargetAdapter ..> ArcInputDispatcher','ComposeTargetAdapter ..> ArcInputLeaseRegistry : principal plus current root; serialized cross-request input')
# A single service-owned target adapter/dispatcher; authoritative declaration is arc-ui.
head,tail=c.split('## arc-target-classes',1)
tail=tail.replace('class UiDispatcher {\n  +executeBoundedOnMainThread(action)\n}','class UiDispatcher <<reference: arc-ui-classes>>')
c=head+'## arc-target-classes'+tail
c=c.replace('BridgeService "1" *-- "1" ArcRuntime','BridgeService "1" *-- "1" ArcTcpTransport\nBridgeService "1" *-- "1" ArcRuntime\nBridgeService "1" *-- "1" ComposeTargetAdapter\nBridgeService "1" *-- "1" UiDispatcher\nArcTcpTransport "1" *-- "1" ArcFrameCodec\nArcTcpTransport "1" *-- "1" ArcRequestHandler\nArcRequestHandler "1" *-- "1" ArcCompatibilityProfile\nArcRequestHandler "1" *-- "1" ArcAuthorizationGate\nArcRequestHandler "1" *-- "1" ArcAuthenticator')
c=c.replace('ComposeTargetAdapter ..> ArcFrameCapture','ComposeTargetAdapter "1" *-- "1" ArcFrameCapture').replace('ComposeTargetAdapter ..> ArcPropertyMutationService','ComposeTargetAdapter "1" *-- "1" ArcPropertyMutationService')
c=c.replace('interface ArcAuthenticator {','class ArcBearerAuthenticator\nArcBearerAuthenticator ..|> ArcAuthenticator\nclass KeystoreClientAuthenticator\nKeystoreClientAuthenticator ..|> ClientAuthenticator\ninterface ArcAuthenticator {')
c=c.replace('  +closeOnOverflowRevocationOrLease()','  +closeOnOverflowRevocationOrLease()')
c=c.replace('McpServer ..> DirectStreamNegotiator','note bottom of DirectEventEmitter\nExplicit android.bridge.directEvents.v1 negotiation only.\nAuthenticated loopback GET /v1/subscriptions/{id}/events SSE.\nApplication cursor differs from MCP SSE Last-Event-ID.\nNo negotiation means standard resource journal only.\nend note\nMcpServer ..> DirectStreamNegotiator',1)
c+='''

## Code view mapping and editing

| Class family | Authoritative C4 component | Gradle project |
| --- | --- | --- |
| BridgeService, BootCompletedReceiver, ForegroundStatusNotifier, HealthSnapshot | lifecycle | bridge.server |
| McpServer, McpSessionRegistry, DirectStreamNegotiator, DirectEventEmitter | transport | bridge.server |
| ClientAuthenticator, KeystoreClientAuthenticator | auth | bridge.server |
| AdmissionGate, OwnerAuthenticationService, RemoteInputQuiescer | policy | bridge.server |
| CatalogRepository, SqliteCatalogRepository, SqliteStoreCoordinator | catalog/storage | bridge.catalog |
| DeviceViewResolver | device | bridge.bindings |
| FunctionalGroupIndex | groups | bridge.server |
| InvocationPlanner, BackendAdapter, CodecRegistry, BinderBackend, SdkBindingBackend, ArcTargetBackend | invoke | bridge.bindings |
| AdbPhoneBackend, AdbConnectionManager | adb | bridge.adb |
| CallbackBroker, CallbackProxyFactory, Subscription, LocalReplyPolicy, EventJournal, SqliteEventJournal, McpCallbackResourceAdapter | callbacks | bridge.server |
| AuditLogger | audit | bridge.catalog |
| OwnerCommandFacade, SessionCommandFacade | ownerCommands/sessionCommands | bridge.server |
| ArcSessionManager, ArcClientAdapter, RemoteFrameState | arcController | bridge.arc |
| ArcTcpTransport, ArcFrameCodec, ArcProtocolModel, ArcCompatibilityProfile, ArcRequestHandler, ArcSessionRegistry, ArcConnectionSession, ArcAuthenticator, ArcBearerAuthenticator, ArcAuthorizationGate, ArcRuntime | arc | bridge.arc |
| ComposeTargetAdapter, TargetAdapter, TargetNodeRegistry, ArcFrameCapture, ArcFrameStreamService, ArcInputDispatcher, ArcInputLeaseRegistry, ArcPropertyMutationService, ArcLogStreamService, UiDispatcher | arc target adapter | bridge.arc / bridge.ui |
| ComposeManagementViewModel, ComposeRemoteControlViewModel | ui | bridge.ui |

SVG textLength attributes are renderer output; edit DSL/PlantUML sources and regenerate,
or remove those attributes before manual SVG text editing. No SVG is runtime app code.
'''
p.write_text(c)

p=P/'architecture/workspace.dsl';w=p.read_text()
w=w.replace('Typed generic/raw dispatch, codecs, Binder and ADB adapters','Typed generic/raw dispatch, codecs, Binder, ADB and ARC target adapters')
w=w.replace('Internal Kotlin port preserving pinned ARC wire/profile; not implemented','Proposed Kotlin port preserving pinned ARC wire/profile; no app implementation claimed')
w=w.replace('Returns finite results or subscribed URI-only notifications','Returns finite results, subscribed URI-only notifications or explicitly negotiated direct-event SSE')
w=w.replace('        bridge.app.invoke -> bridge.app.adb','        bridge.app.invoke -> bridge.app.arcController "ArcTargetBackend forwards exact admitted canonical ARC operation"\n        bridge.app.invoke -> bridge.app.adb',1)
w=w.replace('Ordinary local start/stop and immutable health; no config/grant change','Ordinary local stop/status; stopped-service start uses an Activity foreground-service start intent')
w=w.replace('include bridge.app.arc bridge.app.arcController','include bridge.app.lifecycle bridge.app.arc bridge.app.arcController')
w=w.replace('include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.groups bridge.app.ownerCommands','include bridge.app.lifecycle bridge.app.transport bridge.app.auth bridge.app.policy bridge.app.catalog bridge.app.device bridge.app.callbacks bridge.app.groups bridge.app.ownerCommands')
w=w.replace('GET SSE/stdio:','GET SSE:')
w=w.replace('            bridge.app.policy -> bridge.app.transport "Return exact Admission','            bridge.app.policy -> bridge.app.audit "Fail-closed mandatory admission audit before dispatch"\n            bridge.app.policy -> bridge.app.transport "Return exact Admission')
w=w.replace('Validate principal/version/method and exact canonical operation','Admit authenticated principal and exact reserved ARC method identity')
w=w.replace('            bridge.app.arc -> bridge.app.policy "Admit authenticated','            bridge.app.arc -> bridge.app.auth "Authenticate envelope with expected_kind=arc; validate version/method before target access"\n            bridge.app.arc -> bridge.app.policy "Admit authenticated')
w=w.replace('Release per-connection input/streams; record sanitized outcome','Close per-request streams; input lease spans unary sockets; record sanitized outcome')
w=w.replace('Quiesce bridge input; display OS BiometricPrompt for service-owned exact challenge','Service quiesces/drains every bridge remote-input path before requesting protected OS prompt')
w=w.replace('image bridge.app.transport ServerCoreClasses','image bridge.app ServerCoreClasses').replace('image bridge.app.invoke ServerExecutionClasses','image bridge.app ServerExecutionClasses')
w=w.replace('"Host loopback" {','"Host loopback; logical protocol lines use owner adb forward" {')
w=w.replace('"Different UID; explicitly opted-in ARC target, not needed for generic ADB" "Android app"','"Different UID; explicitly opted-in ARC target, not needed for generic ADB" "Separate UID opted-in target; generic ADB needs no embedding"')
w=w.replace('            phone.sandbox.runtime -> phone.adbd','            phone.localAgent -> phone.sandbox.runtime "Separate-UID on-device MCP client; authenticated loopback listener"\n            phone.sandbox.runtime -> phone.adbd')
w=w.replace('                adbd = infrastructureNode','                localAgent = softwareSystemInstance agent\n                adbd = infrastructureNode')
p.write_text(w)

p=P/'tooling/bootstrap.py';t=p.read_text();t=t.replace("expected=get(url+'.sha256').decode().strip().split()[0]","expected=next(v['sha256'] for v in pinned['files'] if v['file']=='.tools/gradle-'+version+'-bin.zip');published=get(url+'.sha256').decode().strip().split()[0]\nif published!=expected:raise ValueError('Published Gradle checksum differs from committed lock')")
t=t.replace("(R/'gradle/wrapper/gradle-wrapper.properties').write_text(properties)","if (R/'gradle/wrapper/gradle-wrapper.properties').read_text()!=properties:raise ValueError('Committed wrapper properties differ from pinned expected configuration')")
p.write_text(t)
print(json.dumps({'updated':['docs/Android MCP Bridge Design.md','architecture/code-diagrams.md','architecture/workspace.dsl','tooling/bootstrap.py'],'frozen_reviews_modified':False}))
