"""Close020 design defects in the working tree, preserving all frozen reviews."""
from pathlib import Path
import json
R=Path(__file__).resolve().parent;P=R/'design-c4';p=P/'docs/Android MCP Bridge Design.md';s=p.read_text()
a=s.index('The legacy stored `external_adb` label');b=s.index('\n\n',s.index('unannounced wire or policy identity change.',a))
s=s[:a]+'''Migration precedes the live CHECK domains above. Read legacy rows only from a
quarantined migration input or immutable history, never the active tables.
Rewrite external_adb configuration into host_forward_profiles (ID, exact host
authority/allocated port, listener identity, serial, owner-managed mapping,
revision and endpoint epoch). It is ingress only, carries no host5037 credential,
and is never an execution backend. References that previously selected it for
execution/target/grant remain inert in migration quarantine until the owner
selects a proven native/self_adb/arc_target replacement; never retarget a grant.
Rewrite avalonia_runtime and executor avalonia to arc_target only after exact
ARC profile/target compatibility validation, otherwise quarantine them too.
Preserve original row bytes/identities in migration history. Publish migrated
configuration and invalidate old epochs atomically before opening admission.
An unmigrated database cannot start device dispatch. No excluded legacy value
is retained in a live CHECK-constrained table for later admission rejection.
The earlier bridge-config.sql remains an offline legacy reference; this ordered
Kotlin runtime migration is a required implementation gate, not a claim it has
already been deployed. A legacy-shaped execution request fails
INGRESS_PROFILE_NOT_EXECUTABLE without creating a live profile row.'''+s[b:]
s=s.replace('admin window prevents commit, retaining only an explicitly resumable stage. A', 'admin window prevents commit and expires/deletes unactivated staging through bounded cleanup. No resumable-stage mode is selected. A')
s=s.replace('client certificate pinning and the exact MCP transport', 'the client pinning the server certificate and the exact MCP transport')
s=s.replace('about29GiB for three9.57GiB copies plus1GiB reserve', 'about29.7GiB for three9.57GiB copies plus1GiB reserve')
s=s.replace('arc-ui-classes ArcRuntime/router/codec/auth', 'arc-ui-classes ArcRuntime/ArcRequestHandler/ArcFrameCodec/ArcAuthenticator')
# Keep normal start/stop independent of privileged permission/configuration changes.
s += '''

## Round020 closure: remaining ownership and event races

`outcome_unknown` is non-terminal uncertainty, not an alternate active registration.
Every event has an explicit disposition, under the same subscription revision CAS:

| State / arriving event | Disposition |
| --- | --- |
| outcome_unknown + late confirmed registration result, exact pins/consent/lease still valid and no cancellation | active; publish only events accepted after confirmation; never revive an expired or revoked consent |
| outcome_unknown + late confirmed failure/no registration | closed, cleanup_disposition=not_required |
| outcome_unknown + late success after cancel/revoke/lease expiry or changed pins | cancelling; keep delivery closed and queue only verified recorded cleanup |
| outcome_unknown + callback event | suppress payload, record a bounded gap/diagnostic count; event arrival is not proof of successful authorized registration |
| outcome_unknown + callback.cancel with an exact materialized provisional cleanup token and still-safe cleanup contract | cancelling; execute that recorded unregister once, never replay registration |
| outcome_unknown + callback.cancel without a proven cleanup token/contract | remain outcome_unknown with delivery_closed=true and cleanup_unknown; issue no guessed operation; late result may supply the missing recorded token |
| outcome_unknown + confirmed endpoint death or protected owner acknowledgement | closed with not_required only for proven endpoint death, otherwise cleanup_unknown |
| outcome_unknown + another timeout/uncertain observation | remain outcome_unknown, delivery closed; bounded obligation/status survives until reconciliation/retention, never an unbounded retry loop |

An idempotent cancellation_id records the decision, not a promise that an uncertain
OS effect occurred. Duplicate cancellation returns the recorded result without a
second uncertain unregister. All callback delivery threads, including SDK callbacks,
use the same bounded acceptance/local-reply policies; uncertainty never starts an
agent/network round trip for a synchronous return.

SessionCommandFacade owns ordinary local start/stop requests and immutable status
reads. It derives LocalOwnerSessionContext from the unexported Activity/service
binding and app UID; a remote MCP/ARC token cannot assert this context. These
ordinary desired-state commands do not require a new biometric prompt. The
notification Stop action uses an immutable service-owned PendingIntent to the
nonexported receiver and the same ordinary stop command. Boot/TLS/profile/grant
configuration, credential reveal and sensitive export remain protected exact-owner
commands. Start never changes those settings or widens permissions.

AuditLogger owns bounded redacted read snapshots and export preparation as well as
append operations. SessionCommandFacade.queryRedactedLogs filters the local owner's
selected clients/time/range under hard page/byte limits. OwnerCommandFacade.exportLogs
requires fresh exact owner capability for that sealed redacted snapshot, selected
fields, destination and expected logging revision. Export contains only retained
allowlisted projections, never credentials, full raw arguments, screenshots or
excluded fields; encrypted optional projections are decrypted only for this approved
bounded export. Cancellation/disk failure reports no false success. The mandatory
security audit records the export scope/result; exported copies are outside later
purge/expiry enforcement. No UI accesses SQLite directly.

SqliteStoreCoordinator owns the one serialized write connection and bounded read-only
snapshot pool. CatalogRepository, EventJournal and AuditLogger own their table-level
validation and submit transactions through that coordinator. The C4 persistence
edges therefore converge on it; three independent SQLite writers are not selected.
BridgeService owns one coordinator, DirectStreamNegotiator, DirectEventEmitter,
ArcRuntime, ArcSessionManager and SdkBindingBackend. DirectEventEmitter owns bounded
per-stream state; the existing ARC registry owns per-connection frame/log/input state.

ArcAuthenticator is a transport adapter over ClientAuthenticator, not another identity
or policy authority. ClientAuthenticator.authenticate(credential, expected_kind)
resolves the shared random client_id and credential epoch from the app-private
credential registry. credentials.kind CHECK mcp/admin/arc; each route requires its
own kind before session creation. Incoming ARC keeps its opaque Bearer string on
the pinned wire; an indexed keyed token verifier maps it to an owner-configured ARC
credential/principal and exact target scope without adding a wire credential-ID
field. Shared credentials imply the same principal; unique tokens are required for
different client permissions. MCP/admin credentials never fall back to ARC or vice
versa. The discovery marker carries only the currently enabled default ARC principal's
new per-bind token in app-private storage, with zero default grants. Additional unique
ARC credentials are privately owner-provisioned and revoked on the configured bind
epoch change. Outbound ArcClientAdapter target tokens are backend secrets, distinct
from incoming client credentials and owner authority. All resulting operations use
the same canonical AdmissionGate and per-client log configuration.

Reason-envelope `code` values are uppercase registry identifiers. Lowercase names in
older flow prose are shorthand/legacy aliases, never a second new wire grammar:
stale_contract -> CONTRACT_STALE, revoked -> AUTHORITY_REVOKED, epoch_changed ->
EPOCH_CHANGED, unknown_dependency -> GROUP_DEPENDENCY_UNKNOWN and adapter_unsupported
-> ADAPTER_UNSUPPORTED. Subscription lifecycle `reason` is a separate typed enum
(target_died, lease_expired, registration_failed, registration_timeout, etc.), not
the general error-envelope code; when surfaced as an error it maps to an explicit
CALLBACK_* code. Unknown future codes remain visible and nonauthorizing.

Entry effective-document and shared contract JSON hashing use the same meta-json-v1
encoding profile described in the supporting contract; only the selected semantic
fields/domain prefix differ. Original source/file hashes remain raw-byte SHA-256.
POST /entries creates a local binding Entry under its established Idempotency-Key
contract; PUT metadata/{id} creates a CallbackDescriptor/FunctionalGroup under
If-None-Match:*. They are distinct resources, not competing creation methods for
one URI. In both cases imported evidence is immutable and local owner authority is
required. Pairing-flow names are manual_bearer_setup and pairing_helper_v1; paired_tls
names only their TLS transport profile, never a second ambiguous flow name.

The callback materializer uses CLASS_NO_RUNTIME_SUBCLASS with explicit sub_reason
abstract/concrete/final when subclass construction is unavailable. A final class
cannot be subclassed even by a prebuilt adapter; only a separately proven compatible
factory/delegate mechanism could support it. No such mechanism is guessed.
After version/id validation and authentication, ARC method0 and unknown method enum
values return BRIDGE_STATUS_UNSUPPORTED with matching ID/server version and terminal
end_of_stream=true, then close, with no backend dispatch. This is separate from
invalid version/ID errors and from a successful envelope carrying CommandResult false.

For MCP, the rechecked pinned tools page distinguishes malformed CallToolRequest or
unknown tool (protocol error, example -32602) from a valid tools/call whose tool input
fails validation (isError=true). It explicitly lists out-of-range/input-validation
errors as tool execution errors. This is the selected split, not a recollected older
wording. [Pinned tools error handling](https://modelcontextprotocol.io/specification/2025-11-25/server/tools#error-handling).
'''
p.write_text(s)

p=P/'architecture/workspace.dsl';s=p.read_text()
s=s.replace('                audit = component', '                storage = component "SqliteStoreCoordinator" "One serialized writer connection and bounded read-only snapshots" "Kotlin / SQLite"\n                audit = component')
s=s.replace('bridge.app.callbacks -> bridge.db "Appends ordered bounded events and cleanup state"','bridge.app.callbacks -> bridge.app.storage "Appends ordered bounded events and cleanup state through one writer"')
s=s.replace('bridge.app.catalog -> bridge.db "Owns table transactions through the single serialized writer; read-only snapshots"','bridge.app.catalog -> bridge.app.storage "Validated catalog/config transactions and bounded read snapshots"')
s=s.replace('bridge.app.audit -> bridge.db "Persists mandatory audit and configured call records"','bridge.app.audit -> bridge.app.storage "Appends audit/logs and reads redacted snapshots through store coordinator"')
s=s.replace('bridge.app.ownerCommands -> bridge.app.lifecycle "Reads health and requests protected desired state"','bridge.app.ownerCommands -> bridge.app.lifecycle "Reads health/boot eligibility; protected configuration uses repository"')
s=s.replace('bridge.app.transport -> bridge.app.callbacks "Reads owned bounded status/pages and manages exact-URI subscriptions"','bridge.app.transport -> bridge.app.callbacks "callback.status/cancel dispatch, exact-URI subscribe and finite owned journal reads"')
s=s.replace('                arc = component "ArcEngine"', '                arc = component "ArcEngine"')
s=s.replace('"Pinned MCP2025-11-25 transport, sessions and bounded resource reads"', '"Pinned MCP2025-11-25 sessions/resources plus separately negotiated direct-event route"')
s=s.replace('        deploymentEnvironment "ReferenceAndroid" {','''        bridge.app.storage -> bridge.db "Owns the single writer connection and bounded snapshot readers"
        bridge.app.lifecycle -> bridge.app.storage "Owns and closes coordinator before service shutdown"
        bridge.app.ownerCommands -> bridge.app.audit "Exports exact redacted snapshot after fresh owner authorization"
        bridge.app.sessionCommands -> bridge.app.audit "Queries bounded local-owner log snapshots; no direct SQL"
        bridge.app.sessionCommands -> bridge.app.lifecycle "Ordinary local start/stop and immutable health; no config/grant change"
        bridge.app.arc -> bridge.app.auth "ArcAuthenticator delegates expected_kind=arc to shared credential/client registry"
        deploymentEnvironment "ReferenceAndroid" {''')
s=s.replace('                adbd = infrastructureNode', '''                deploymentNode "Optional instrumented target app" "Different UID; explicitly opted-in ARC target, not needed for generic ADB" "Android app" {
                    softwareSystemInstance arcTarget
                }
                adbd = infrastructureNode''')
s=s.replace('bridge.app.audit bridge.db', 'bridge.app.audit bridge.app.storage bridge.db')
s=s.replace('bridge.app.catalog bridge.db android','bridge.app.catalog bridge.app.storage bridge.db android')
s=s.replace('bridge.app.policy bridge.app.audit bridge.app.storage bridge.db arcClient', 'bridge.app.policy bridge.app.auth bridge.app.audit bridge.app.storage bridge.db arcClient')
s=s.replace('            bridge.app.callbacks -> bridge.db "Accept and commit ordered journal event within quotas"','            bridge.app.callbacks -> bridge.app.storage "Accept and commit ordered journal event within quotas"')
old='''            bridge.app.groups -> bridge.app.device "Pin availability and required type/dependency closure"
            bridge.app.transport -> bridge.app.groups "CAS activate coherent set or return stale/conflict"'''
new='''            bridge.app.transport -> bridge.app.groups "Begin requested atomic activation with expected pins"
            bridge.app.groups -> bridge.app.device "Validate availability and required type/dependency closure"
            bridge.app.groups -> bridge.app.catalog "CAS commit coherent activation or return stale/conflict"
            bridge.app.groups -> bridge.app.transport "Return committed group view or explicit conflict"'''
if old not in s:raise ValueError('GroupFlow anchor missing')
s=s.replace(old,new)
p.write_text(s)

p=P/'architecture/code-diagrams.md';s=p.read_text()
s=s.replace('+authenticate(credential): Principal','+authenticate(credential, expectedKind): Principal')
s=s.replace('class SqliteCatalogRepository','''class SqliteStoreCoordinator {
  +writeSerialized(transaction)
  +readBoundedSnapshot(query, pins)
}
class SqliteCatalogRepository''',1)
s=s.replace('  +recordBoundedOutcome(result, logPolicy)','  +recordBoundedOutcome(result, logPolicy)\n  +queryRedactedSnapshot(ownerContext, filters, cursor, limits)\n  +exportApprovedSnapshot(command, ownerCapability)')
s=s.replace('BridgeService "1" *-- "1" SessionCommandFacade','''BridgeService "1" *-- "1" SessionCommandFacade
BridgeService "1" *-- "1" SqliteStoreCoordinator
BridgeService "1" *-- "1" DirectStreamNegotiator
BridgeService "1" *-- "1" DirectEventEmitter
SqliteCatalogRepository ..> SqliteStoreCoordinator
AuditLogger ..> SqliteStoreCoordinator
SessionCommandFacade ..> AuditLogger : bounded redacted local-owner query
OwnerCommandFacade ..> AuditLogger : protected exact-snapshot export''')
s=s.replace('BridgeService "1" *-- "1" AdbPhoneBackend','''BridgeService "1" *-- "1" AdbPhoneBackend
BridgeService "1" *-- "1" SdkBindingBackend
class SqliteStoreCoordinator <<reference: server-core-classes>>
SqliteEventJournal ..> SqliteStoreCoordinator''')
s=s.replace('class ArcAuthenticator {','class ClientAuthenticator <<reference: server-core-classes>>\nArcAuthenticator ..> ClientAuthenticator : authenticate expectedKind=arc; same client registry\nclass ArcAuthenticator {')
s=s.replace('class ArcFrameCodec {','class ArcFrameCodec {\n  +maxFrameBytes: Int = 8388608')
# Service owns the shared runtime/controller manager, connections still own stream/input state.
s=s.replace('ArcRuntime ..> TargetAdapter','''ArcRuntime ..> TargetAdapter
class BridgeService <<reference: server-core-classes>>
BridgeService "1" *-- "1" ArcRuntime''')
s=s.replace('ArcSessionManager "1" *-- "0..*" ArcClientAdapter','''ArcSessionManager "1" *-- "0..*" ArcClientAdapter
class BridgeService <<reference: server-core-classes>>
BridgeService "1" *-- "1" ArcSessionManager''')
p.write_text(s)

p=P/'build.gradle.kts';s=p.read_text()
s=s.replace('val expectedUiViews = (1..11).map { "SCR%02d".format(it) } + (1..5)', 'val expectedUiViews = (1..11).map { "SCR%02d".format(it) } + (1..6)')
s=s.replace('val svg = source.readText()', 'val svg = source.readText().replace(Regex("<!--SRC=\\\\[.*?\\\\]-->", RegexOption.DOT_MATCHES_ALL), "")')
s=s.replace('inputs.files(fileTree("build/code-views") { include("*.puml") }, pumlJar)','inputs.files(fileTree("build/code-views") { include("*.puml") }, pumlJar, "build.gradle.kts", "renderer-lock.json")')
s=s.replace('inputs.files(fileTree("build/structurizr-export") { include("*.puml") }, pumlJar)','inputs.files(fileTree("build/structurizr-export") { include("*.puml") }, pumlJar, "build.gradle.kts", "renderer-lock.json")')
s=s.replace('check(!text.contains("<image", ignoreCase = true)', 'check(!text.contains("<!--SRC=[")) { "Stale embedded renderer metadata: $svg" }\n            check(!text.contains("<image", ignoreCase = true)')
# Inline Marp contains SVG data URIs; deny external CSS assets as well as HTML src/href.
s=s.replace('check(!Regex("(?:src|href)=[\\\"\']https?://").containsMatchIn(file("docs/storyboards.html").readText()))', 'check(!Regex("(?:src|href)=[\\\"\']https?://|url\\\\(\\\\s*[\\\"\']?https?://", RegexOption.IGNORE_CASE).containsMatchIn(file("docs/storyboards.html").readText()))')
p.write_text(s)
print('Applied020 ownership, migration, state-machine and build corrections; UI extension follows')
