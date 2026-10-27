# C4 code-level detail, using UML class notation

Structurizr has no native class view. These editable inline PlantUML definitions are rendered locally and referenced by image views in workspace.dsl. They are proposed Kotlin classes/interfaces, not implemented Android code. Composition diamonds denote owned lifetime; dashed arrows denote dependency, and quoted labels denote multiplicity. Each block identifies its scoped C4 component.

## server-core-classes — bridge.app.transport and server ownership

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Kotlin server core — proposed ownership and contracts
class BridgeService {
  +startDesiredState()
  +stopWithinDeadline()
  +observeReadiness(): StateFlow
}
class McpServer {
  +handlePost(request): Response
  +openNotificationStream(session)
  +readResource(uri, cursor, limits)
}
class McpSessionRegistry {
  +resolveOwnedSession(principal, id)
  +invalidateEpoch(epoch)
}
interface ClientAuthenticator {
  +authenticate(credential): Principal
}
class AdmissionGate {
  +admit(plan, pins): Admission
  +recheck(admission): Decision
}
interface CatalogRepository {
  +streamEffectiveInventory(snapshot)
  +commitOverride(command, expectedRevision)
  +activateSealedView(pins)
}
class SqliteCatalogRepository
class DeviceViewResolver {
  +resolve(facts, inventory): StagedView
  +activateCAS(expectedView, stagedView)
}
class FunctionalGroupIndex {
  +discover(job, constraints, budget)
  +setActiveCAS(session, desiredSet, etag)
}
class AuditLogger {
  +recordAdmission(principal, pins)
  +recordBoundedOutcome(result, logPolicy)
}
class OwnerCommandFacade {
  +submit(command, ownerCapability, etag)
  +observeImmutableStatus()
}
BridgeService "1" *-- "1" McpServer
BridgeService "1" *-- "1" DeviceViewResolver
BridgeService "1" *-- "1" SqliteCatalogRepository
McpServer "1" *-- "1" McpSessionRegistry
McpServer ..> ClientAuthenticator
McpServer ..> AdmissionGate
McpServer ..> FunctionalGroupIndex
AdmissionGate ..> CatalogRepository
AdmissionGate ..> DeviceViewResolver
AdmissionGate ..> AuditLogger
SqliteCatalogRepository ..|> CatalogRepository
DeviceViewResolver ..> CatalogRepository
FunctionalGroupIndex ..> CatalogRepository
FunctionalGroupIndex ..> AdmissionGate
OwnerCommandFacade ..> AdmissionGate
OwnerCommandFacade ..> CatalogRepository
note bottom of OwnerCommandFacade
Unexported bounded local Binder facade.
UI reference is not owner authorization.
end note
@enduml
```

## arc-controller-classes — bridge.app.arcController and local Compose controller UI

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Kotlin ARC controller UI and client — proposed
class ComposeRemoteControlViewModel {
  +selectTarget(profileId)
  +connectAndProbe()
  +requestApprovedInput(events)
  +disconnect()
}
class ArcSessionManager {
  +openOwnedSession(principal, targetPins)
  +validateCapabilitiesAndEpoch()
  +cancelByClosingSocket()
}
class ArcClientAdapter {
  +sendUnary(method, typedPayload)
  +watchTypedResponses(method)
  +validateIdVersionStatusAndCommandResult()
}
class ArcFrameCodec
class ArcProtocolModel
class RemoteFrameState {
  +targetGeneration
  +sequence: ULong
  +pixelAndLogicalBounds
  +renderScale
}
class AdmissionGate {
  +admitClientTargetOperation(plan)
}
class CompatibleExternalRuntime <<external>>
ComposeRemoteControlViewModel ..> ArcSessionManager : local service facade
ArcSessionManager "1" *-- "0..*" ArcClientAdapter
ArcSessionManager ..> AdmissionGate
ArcClientAdapter ..> ArcFrameCodec
ArcFrameCodec ..> ArcProtocolModel
ArcClientAdapter --> CompatibleExternalRuntime : unchanged ARC TCP bytes
ArcClientAdapter --> RemoteFrameState : generation-pinned observation
ComposeRemoteControlViewModel ..> RemoteFrameState
note bottom of ArcClientAdapter
No separate handshake. Capabilities-first is a convention.
Validate OK envelope AND CommandResult.succeeded.
Cancel enum8 is not a supported v1 command.
end note
@enduml
```

## server-execution-classes — bridge.app.invoke and bridge.app.callbacks

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Kotlin invocation and callback contracts — proposed
class InvocationPlanner {
  +plan(bindingId, args, callbacks, pins)
  +dispatch(admission): BoundedResult
}
interface BackendAdapter {
  +invoke(verifiedPlan): Outcome
  +observeCapabilityEpoch()
}
class BinderBackend {
  +transact(verifiedWirePlan)
}
class AdbPhoneBackend {
  +screenState(scope)
  +tap(scope, coordinates)
}
class CodecRegistry {
  +selectExact(schemaHash, wireProof)
}
class CallbackBroker {
  +register(admission): SubscriptionHandle
  +receive(sender, method, payload)
  +cancel(handle): CleanupOutcome
  +recoverOrphanedRegistrations()
}
class Subscription {
  +id: String
  +streamEpoch: String
  +revision: Long
  +state: RegistrationState
  +registrationAndCleanupPins
}
interface EventJournal {
  +appendOrderedWithinQuota(event)
  +readOwnedPage(cursor, limits)
  +recordGapOrTerminal(reason)
}
class SqliteEventJournal
class LocalReplyPolicy {
  +validateTypedFallback(evidence)
  +replyWithinDeadline(input)
}
class McpCallbackResourceAdapter {
  +subscribeOwnedUri(session, uri)
  +notifyUriOnly(uri)
  +readBoundedPage(principal, cursor)
}
InvocationPlanner ..> BackendAdapter
InvocationPlanner ..> CodecRegistry
InvocationPlanner ..> CallbackBroker
BinderBackend ..|> BackendAdapter
AdbPhoneBackend ..|> BackendAdapter
BinderBackend ..> CodecRegistry
CallbackBroker "1" *-- "0..*" Subscription
CallbackBroker ..> EventJournal
CallbackBroker ..> LocalReplyPolicy
SqliteEventJournal ..|> EventJournal
McpCallbackResourceAdapter ..> CallbackBroker
McpCallbackResourceAdapter ..> EventJournal
note bottom of LocalReplyPolicy
No network or agent roundtrip for sync return.
Unknown valid deadline/default means unsupported.
end note
note bottom of Subscription
Durable handle/journal is not a durable OS registration.
Transport disconnect does not imply unregister.
end note
@enduml
```

## arc-ui-classes — bridge.app.arc, bridge.app.ui and ownerCommands

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Internal Kotlin ARC port and Compose boundary — proposed
class CompatibleArcClient <<external>>
class ArcTcpTransport {
  +acceptLoopbackConnection()
  +readOneRequestAndStreamResponses()
  +closeSocketOnCancel()
}
class ArcFrameCodec {
  +readExactSignedBigEndianLength()
  +decodeBoundedFrame(): BridgeRequest
  +writeFrame(response)
}
class ArcProtocolModel {
  +protocolVersion = "1.0"
  +transport = "arc-protobuf-v1"
  +preserveUnknownProtobufFields()
  +parseRequest(bytes): BridgeRequest
  +serializeResponse(response): ByteArray
}
class ArcCompatibilityProfile {
  +validateVersionAndMethod(request)
  +validateResponseIdVersionStatus()
}
class ArcRequestHandler {
  +handleUnary(request): BridgeResponse
  +handleWatch(request): ResponseFlow
}
class ArcSessionRegistry {
  +pinConnectionPrincipalAndEpoch()
  +revokeAndCloseStreams()
}
interface ArcAuthenticator {
  +authenticateEnvelopeBearer(): Principal
}
class ArcAuthorizationGate {
  +admitTargetOperation(principal, pins)
}
class ArcRuntime {
  +getCapabilities(principal)
  +invokeGuardedOperation(plan)
  +watchBoundedEvents(plan)
}
interface TargetAdapter {
  +snapshot(rootGeneration)
  +watchFramesWithinQuota()
  +invokeApprovedAction()
  +sendInput(rootGeneration, events)
}
class ComposeTargetAdapter {
  +captureUnprotectedSelectedRoot()
  +mapDipAndFrameScale()
  +releasePressedInputOnClose()
}
class UiDispatcher {
  +executeBoundedOnMainThread(action)
}
class ComposeManagementViewModel {
  +observeStatusAndDrafts()
  +submitOwnerCommand(expectedRevision)
}
class OwnerCommandFacade {
  +requireFreshOwnerCapability(command)
}
CompatibleArcClient --> ArcTcpTransport : retained ARC wire
ArcTcpTransport "1" *-- "0..*" ArcSessionRegistry
ArcTcpTransport ..> ArcFrameCodec
ArcFrameCodec ..> ArcProtocolModel
ArcTcpTransport ..> ArcRequestHandler
ArcRequestHandler ..> ArcAuthenticator
ArcRequestHandler ..> ArcCompatibilityProfile
ArcRequestHandler ..> ArcAuthorizationGate
ArcRequestHandler ..> ArcRuntime
ArcRuntime ..> TargetAdapter
ComposeTargetAdapter ..|> TargetAdapter
ComposeTargetAdapter ..> UiDispatcher
ComposeManagementViewModel ..> OwnerCommandFacade : local only
note right of ArcTcpTransport
Authenticated plaintext loopback TCP, no fake TLS.
One request/socket. Cancel enum8 is unsupported;
the compatible client closes its socket.
end note
note bottom of ComposeTargetAdapter
Selected ordinary root only.
Protected approval/grant screens excluded.
Generic phone input uses separate AdbPhoneBackend.
end note
note bottom of OwnerCommandFacade
ARC principal cannot mint local owner approval.
The ViewModel is not the external ARC client.
end note
@enduml
```

## arc-target-classes — bridge.app.arc target/runtime adapter detail

```plantuml
@startuml
!pragma layout smetana
skinparam defaultFontName DejaVu Sans
skinparam classAttributeIconSize 0
skinparam shadowing false
skinparam backgroundColor white
title Kotlin ARC target adapter and UI execution — proposed
interface TargetAdapter
class ComposeTargetAdapter {
  +snapshotSelectedRoot()
  +invokeApprovedAction()
  +observeRootGeneration()
}
class TargetNodeRegistry {
  +resolveNode(id, generation)
  +invalidateOnRootReplacement()
}
class ArcFrameCapture {
  +captureBoundedPng(root): FrameUpdate
  +excludeProtectedSurfaces()
}
class ArcFrameStreamService {
  +streamWithinLocalRateAndByteQuota()
  +closeSlowConsumerWithTerminal()
}
class ArcInputDispatcher {
  +mapRootDipAndAvaloniaKeyNames(events)
  +dispatchOrderedApprovedBatch()
  +releasePressedStateOnClose()
}
class ArcPropertyMutationService {
  +applyAllowlistedTypedProperty()
}
class ArcLogStreamService {
  +filterRedactAndReportDroppedCount()
}
class UiDispatcher {
  +executeBoundedOnMainThread(action)
}
class ArcAuthorizationGate
ComposeTargetAdapter ..|> TargetAdapter
ComposeTargetAdapter "1" *-- "1" TargetNodeRegistry
ComposeTargetAdapter ..> ArcFrameCapture
ComposeTargetAdapter ..> ArcInputDispatcher
ComposeTargetAdapter ..> ArcPropertyMutationService
ArcFrameStreamService ..> ArcFrameCapture
ArcFrameCapture ..> UiDispatcher
ArcInputDispatcher ..> UiDispatcher
ArcPropertyMutationService ..> UiDispatcher
ArcInputDispatcher ..> ArcAuthorizationGate
ArcFrameStreamService ..> ArcAuthorizationGate
ArcLogStreamService ..> ArcAuthorizationGate
note bottom of ArcInputDispatcher
Mouse/wheel/key/text only; no wire pointer ID or multitouch.
Key strings are not Android keycodes.
Target generation, policy epoch and protected-root gate rechecked.
end note
@enduml
```
