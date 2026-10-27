"""Targeted working-draft corrections; never modify frozen018 or delivered files."""
import pathlib,json,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';p=P/'architecture/workspace.dsl';s=p.read_text()
def replace(old,new):
 global s
 if s.count(old)!=1:raise ValueError('Unexpected model anchor '+old[:80])
 s=s.replace(old,new)
replace('                ownerCommands = component "OwnerCommandFacade" "Bounded unexported local Binder commands and owner authentication" "Kotlin"','''                ownerCommands = component "OwnerCommandFacade" "Protected owner commands; nonce/revision-bound fresh authentication" "Kotlin"
                sessionCommands = component "SessionCommandFacade" "Bounded unexported session commands; server-derived principal and canonical policy" "Kotlin"
                adb = component "AdbPhoneBackend" "Separate paired ADB connection manager and on-demand phone operations" "Kotlin"''')
replace('        bridge.app.policy -> bridge.app.invoke "Admits verified canonical dispatch"','''        bridge.app.transport -> bridge.app.invoke "Plans bounded operation then dispatches canonical Admission"
        bridge.app.policy -> bridge.app.transport "Returns admission bound to exact plan and current pins"
        bridge.app.invoke -> bridge.app.transport "Returns finite outcome without replaying uncertain effects"
        bridge.app.transport -> agent "Returns finite results or subscribed URI-only notifications" "MCP2025-11-25"''')
replace('        bridge.app.invoke -> android "Performs authorized SDK/Binder/paired ADB operation"','''        bridge.app.invoke -> android "Performs authorized SDK/Binder operation"
        bridge.app.invoke -> bridge.app.adb "Dispatches admitted bounded ADB plan"
        bridge.app.adb -> android "Paired same-device ADB; on-demand screen.state/input.tap"
        android -> bridge.app.invoke "Returns registration or invocation outcome"''')
replace('        bridge.app.arc -> android "Uses separate authorized ADB backend for generic phone control"','')
replace('        bridge.app.ui -> bridge.app.arcController "Controls explicitly selected remote session through local service"','''        bridge.app.ui -> bridge.app.sessionCommands "Submits bounded session command; cannot assert owner identity"
        bridge.app.sessionCommands -> bridge.app.policy "Derives trusted context and admits exact target operation"
        bridge.app.sessionCommands -> bridge.app.arcController "Forwards admitted explicit target/session command"''')
s=s.replace('bridge.app.arcController bridge.app.ui bridge.app.ownerCommands bridge.app.policy','bridge.app.arcController bridge.app.ui bridge.app.ownerCommands bridge.app.sessionCommands bridge.app.policy')
s=s.replace('bridge.app.groups bridge.app.ownerCommands bridge.app.audit','bridge.app.groups bridge.app.ownerCommands bridge.app.sessionCommands bridge.app.audit')
s=s.replace('include bridge.app.policy bridge.app.device bridge.app.invoke bridge.app.callbacks','include bridge.app.policy bridge.app.device bridge.app.invoke bridge.app.adb bridge.app.callbacks')
start=s.index('        dynamic bridge.app CallbackFlow ');end=s.index('        dynamic bridge.app InvocationFlow ',start)
s=s[:start]+'''        dynamic bridge.app CallbackFlow "Finite callback registration; then separate resource subscription" {
            agent -> bridge.app.transport "tools/call binding.invoke with typed callback bindings (Streamable HTTP POST)"
            bridge.app.transport -> bridge.app.auth "Authenticate principal and scope"
            bridge.app.transport -> bridge.app.invoke "Plan exact registration, cleanup and callback types"
            bridge.app.transport -> bridge.app.policy "Admit exact plan with pinned authority and quotas"
            bridge.app.policy -> bridge.app.transport "Return Admission; no device effect yet"
            bridge.app.transport -> bridge.app.invoke "dispatch(admission) once"
            bridge.app.invoke -> bridge.app.callbacks "Create provisional proxy, journal and cleanup obligation"
            bridge.app.invoke -> android "Register verified SDK/Binder listener with exact argument mapping"
            android -> bridge.app.invoke "Return registration result or explicit unknown outcome"
            bridge.app.invoke -> bridge.app.transport "Return finite registration outcome and subscription handle"
            bridge.app.transport -> agent "Single final JSON-RPC result with journal URI; POST SSE normally terminates"
            agent -> bridge.app.transport "resources/subscribe on exact events URI; journal remains bounded"
            android -> bridge.app.callbacks "Deliver callback; synchronous reply is bounded and local"
            bridge.app.callbacks -> bridge.db "Accept and commit ordered journal event within quotas"
            bridge.app.callbacks -> bridge.app.transport "Coalesce URI update hint after commit"
            bridge.app.transport -> agent "notifications/resources/updated URI only, subscribed GET SSE/stdio session"
            agent -> bridge.app.transport "resources/read cursor-expanded URI for finite page"
            bridge.app.transport -> agent "Return bounded resource page; transport Last-Event-ID is separate"
            autoLayout tb
        }
''' + s[end:]
replace('            bridge.app.policy -> bridge.app.invoke "Admit once, no replay of uncertain effects"','''            bridge.app.policy -> bridge.app.transport "Return exact Admission and policy/contract pins"
            bridge.app.transport -> bridge.app.invoke "Dispatch Admission once; no replay of uncertain effects"''')
p.write_text(s)
p=P/'architecture/code-diagrams.md';s=p.read_text()
s=s.replace('Each block identifies its scoped C4 component.','Each block identifies its scoped C4 component. Member lists show selected responsibilities, not every Kotlin member. A reference stub explicitly names its authoritative view; realizations inherit the full interface contract. Local epochs/generations are bridge metadata unless the pinned proto has the corresponding field.')
def sub(old,new,count=1):
 global s
 if s.count(old)!=count:raise ValueError('Unexpected class anchor '+old[:80])
 s=s.replace(old,new)
sub('McpServer ..> FunctionalGroupIndex','''McpServer ..> FunctionalGroupIndex
class InvocationPlanner <<reference: server-execution-classes>>
class McpCallbackResourceAdapter <<reference: server-execution-classes>>
McpServer ..> InvocationPlanner : plan then dispatch admitted plan
McpServer ..> McpCallbackResourceAdapter : subscribe / bounded read / URI hint
class OwnerAuthenticationService {
  +issueChallenge(exactCommand, revisions, nonce)
  +consumeFreshBiometricResult(challenge): OwnerCapability
  +verifyAndConsume(capability, command, revisions)
}
class SessionCommandFacade {
  +submitSessionCommand(command, trustedContext, etag)
  +observeOwnedSessionStatus()
}
OwnerCommandFacade ..> OwnerAuthenticationService
SessionCommandFacade ..> AdmissionGate
note bottom of OwnerAuthenticationService
Service-owned nonce, exact command/revisions and foreground Activity.
BiometricPrompt strong/device credential, max30s, single use.
UI object or JSON principal is never owner authorization.
end note
BridgeService "1" *-- "1" AdmissionGate
BridgeService "1" *-- "1" ClientAuthenticator
BridgeService "1" *-- "1" FunctionalGroupIndex
BridgeService "1" *-- "1" AuditLogger
BridgeService "1" *-- "1" OwnerCommandFacade
BridgeService "1" *-- "1" OwnerAuthenticationService
BridgeService "1" *-- "1" SessionCommandFacade
BridgeService "1" *-- "1" InvocationPlanner
BridgeService "1" *-- "1" McpCallbackResourceAdapter''')
sub('class AdmissionGate {\n  +admitClientTargetOperation(plan)\n}','''class AdmissionGate <<reference: server-core-classes>>
class SessionCommandFacade <<reference: server-core-classes>>''')
sub('ComposeRemoteControlViewModel ..> ArcSessionManager : local service facade','''ComposeRemoteControlViewModel ..> SessionCommandFacade : bounded unexported Binder facade
SessionCommandFacade ..> ArcSessionManager : admitted target/session command''')
sub('class BinderBackend {\n  +transact(verifiedWirePlan)\n}','''class BinderBackend {
  +invoke(verifiedPlan): Outcome
  +observeCapabilityEpoch()
  +transact(verifiedWirePlan)
}''')
sub('class AdbPhoneBackend {\n  +screenState(scope)\n  +tap(scope, coordinates)\n}','''class AdbPhoneBackend {
  +invoke(verifiedPlan): Outcome
  +observeCapabilityEpoch()
  +screenState(scope)
  +tap(scope, coordinates)
}
class AdbConnectionManager {
  +connectVerifiedPairedProfile(profile): ConnectionOrReason
  +observeHealthAndEpoch()
  +closeBoundedCollectors()
}
class BinderHandleProvider {
  +acquireVerifiedAccessProfile(descriptor): BinderOrReason
}
class WireContractVerifier {
  +verifyPinnedSchemaAndBackendEvidence(plan): VerifiedWirePlanOrReason
}
AdbPhoneBackend "1" *-- "1" AdbConnectionManager
BinderBackend ..> BinderHandleProvider
BinderBackend ..> WireContractVerifier
note bottom of AdbConnectionManager
Owns paired ADB identity, encrypted key, endpoint/TLS verification,
pair/connect discovery and bounded reconnect/unsupported reasons.
No unauthenticated5555 or universal OEM access fallback.
end note
note bottom of WireContractVerifier
Trusted exact generated/imported codec proof plus current backend pins.
A caller JSON flag, source descriptor or API level is not wire proof.
Unsupported acquisition/codec remains explicit.
end note''')
sub('CallbackBroker "1" *-- "0..*" Subscription','''class BridgeService <<reference: server-core-classes>>
BridgeService "1" *-- "1" CallbackBroker
BridgeService "1" *-- "1" SqliteEventJournal
BridgeService "1" *-- "1" CodecRegistry
BridgeService "1" *-- "1" BinderBackend
BridgeService "1" *-- "1" AdbPhoneBackend
CallbackBroker "1" *-- "0..*" Subscription''')
sub('  +closeSocketOnCancel()','  +closeOnPeerDisconnectOrServerRevocation()')
sub('class ComposeTargetAdapter {\n  +captureUnprotectedSelectedRoot()\n  +mapDipAndFrameScale()\n  +releasePressedInputOnClose()\n}','class ComposeTargetAdapter <<reference: arc-target-classes>>')
sub('class OwnerCommandFacade {\n  +requireFreshOwnerCapability(command)\n}','''class OwnerCommandFacade <<reference: server-core-classes>>
class AdmissionGate <<reference: server-core-classes>>
ArcAuthorizationGate ..> AdmissionGate : delegates admission / recheck; no parallel authority''')
sub('ArcTcpTransport "1" *-- "0..*" ArcSessionRegistry','''ArcTcpTransport "1" *-- "1" ArcSessionRegistry
class ArcConnectionSession {
  +principalAndCredentialEpoch
  +boundedResponseDeadline
  +closeAndReleaseOwnedInput()
}
ArcSessionRegistry "1" *-- "0..*" ArcConnectionSession''')
sub('One request/socket. Cancel enum8 is unsupported;\nthe compatible client closes its socket.','One request/socket. Received Cancel8 returns UNSUPPORTED then closes.\nThe controller cancels a watch by closing its own watch socket.')
sub('interface TargetAdapter\nclass ComposeTargetAdapter {\n  +snapshotSelectedRoot()\n  +invokeApprovedAction()\n  +observeRootGeneration()\n}','''interface TargetAdapter <<reference: arc-ui-classes>>
class ComposeTargetAdapter {
  +snapshot(rootGeneration)
  +watchFramesWithinQuota()
  +invokeApprovedAction()
  +sendInput(rootGeneration, events)
  +observeRootGeneration()
  +excludeProtectedSelectedRoot()
}''')
sub('  +excludeProtectedSurfaces()','  +rejectInvalidOrProtectedRoot(rootGeneration)')
sub('  +closeSlowConsumerWithTerminal()','  +tryTerminalWithinWriteDeadlineElseCloseSocket()')
sub('class ArcAuthorizationGate\nComposeTargetAdapter','''class ArcAuthorizationGate <<reference: arc-ui-classes>>
class ArcConnectionSession <<reference: arc-ui-classes>>
ArcConnectionSession "1" *-- "0..1" ArcInputDispatcher
ArcConnectionSession "1" *-- "0..1" ArcFrameStreamService
ArcConnectionSession "1" *-- "0..1" ArcLogStreamService
note bottom of ArcConnectionSession
Per-connection input state/streams; close releases owned pressed state.
UiDispatcher and selected-root adapter are service singletons.
end note
ComposeTargetAdapter''')
sub('class ArcFrameCodec\nclass ArcProtocolModel','class ArcFrameCodec <<reference: arc-ui-classes>>\nclass ArcProtocolModel <<reference: arc-ui-classes>>')
p.write_text(s)
ledger=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source='018pass02; final synthesis still pending',working_fixes=['P2-F01','P2-F02','P2-F03','P2-F04','P2-F05','P2-F06','P2-F07','P2-F08','P2-F09','P2-F12','P2-F14','P2-F15','P2-F16','P2-F17','P2-F21'],cross_artifact_reconciliation_required=['P2-F10 owner mechanism already in prose30s','P2-F13 lease already in prose','P2-F22 transport/URI rules already in prose','P2-F34 oversize policy already in prose'],frozen018_unchanged=True,validation_pending=True)
(R/'evidence/design018-diagram-working-ledger.json').write_text(json.dumps(ledger,indent=2)+'\n')
print(json.dumps(ledger))
