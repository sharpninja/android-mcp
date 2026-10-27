"""Close cross-section conflicts and record reviewed ARC evidence, working copy only."""
from pathlib import Path
import json,re
P=Path(__file__).resolve().parent/'design-c4'
p=P/'docs/Android MCP Bridge Design.md';s=p.read_text()
a=s.index('## Manual pairing flow');b=s.index('Use an explicit pairing surface',a)
s=s[:a]+'''## Manual bearer and optional helper profiles

`manual_bearer` is the interoperable static-header path: fresh local owner authentication creates credential_id.secret once, the owner copies the exact endpoint/TLS trust and Authorization: Bearer value into the selected client, and grants start denied. No bootstrap exchange or agent HMAC is required. Lost reveal requires rotation/revocation; the service stores only the verifier and credential identifier.

`paired_tls` is the optional helper path: opening its owner window creates a fresh60second QR attempt; the client pins its certificate, submits the exact nonce/label proof, then the owner confirms that specific request with a fresh<=30second prompt before the attempt expires. The detailed byte protocol and failure/retry rules are in "Pairing, owner authentication and boot lifecycle". The10minute window permits multiple distinct attempts; it never extends an individual nonce. No bootstrap secret survives its attempt.

'''+s[b:]
s=s.replace('optional Android RemoteControl integration','required internal Kotlin ARC port and optional compatible external targets')
s=s.replace('optional Android RemoteControl','required internal Kotlin ARC port')
s=s.replace('backend_kind CHECK self_adb/external_adb/avalonia_runtime','backend_kind CHECK native/self_adb/arc_target; legacy external_adb is import-only ingress metadata and cannot execute')
s=s.replace('tools.executor_kind CHECK native/adb/avalonia','tools.executor_kind CHECK native/adb/arc_target')
s=s.replace('SQLite page cache<=64MiB','SQLite page cache<=32MiB')
s=s.replace('The headless service target is <=256MiB idle RSS and <=384MiB peak excluding OS-owned graphics buffers,', 'The headless service target is <=256MiB idle RSS and <=384MiB peak PSS including attributable graphics,')
s=s.replace('rounds up to30GiB plus the quota','is about29GiB for three9.57GiB copies plus1GiB reserve, rounded conservatively to30GiB plus the quota')
s=s.replace('reports backend_unreachable:non_sdk','reports {layer:acquisition,code:NON_SDK_BLOCKED}')
s=s.replace('report backend_unreachable:hidl','report {layer:acquisition,code:HIDL_BACKEND_UNAVAILABLE}')
s=s.replace('An abstract callback class cannot use java.lang.reflect.Proxy.', 'Neither an abstract nor a concrete callback class can use java.lang.reflect.Proxy; a final class cannot be subclassed at all.')
s=s.replace('Source AIDL alone does not establish generated transaction codes', 'Source AIDL alone does not establish generated transaction codes')
s += '''

### ARC compatibility defaults and Compose realization

The transport marker arc-protobuf-v1 is upstream, not a coined bridge name: pinned commit19792a52a13a3df342f90bab6b246b547e673919, src/Avalonia.RemoteControl.Protocol/RemoteControlProtocol.cs:31 defines AndroidBridgeTransportProtocol. That file's MajorVersion=1 and MinorVersion=0 form DisplayVersion1.0; upstream package0.8.0 is a distribution version and never substitutes for the wire envelope. [Pinned constants](https://github.com/sharpninja/Avalonia.RemoteControl/blob/19792a52a13a3df342f90bab6b246b547e673919/src/Avalonia.RemoteControl.Protocol/RemoteControlProtocol.cs).

ArcFrameCodec.maxFrameBytes=8388608 applies before allocation; negative/oversize length closes without dispatch, truncated headers/bodies and EOF release the connection. Length zero is permitted by upstream framing and decodes to a default protobuf envelope, which fails version/auth validation; do not change this to a framing-level zero rejection. No empty protocol_version, unknown method0 or absent bearer gains authority. An empty request ID is not upgraded to an authenticated session; the server treats it as invalid request input and produces a bounded sanitized error then closes, with the decoded ID if present. The port compatibility fixtures must record this deliberate invalid-request rejection alongside upstream behavior. Missing end_of_stream defaults false; every actual terminal sets it true explicitly. Incoming Cancel8 returns BRIDGE_STATUS_UNSUPPORTED with matching version/ID and end_of_stream=true, then normal socket close; it never cancels another stream.

The client rejects a response envelope version other than1.0 or mismatching request ID before interpreting status or terminal flag. The server rejects a request envelope version other than1.0 without backend dispatch; capabilities payload protocol_version must also be1.0 and consistent with the envelope. A capabilities-first call is convention, not an invented handshake. Any compatibility exception requires an explicitly versioned profile and golden fixtures; no negotiation guesses. A token is newly generated for each bind, stale marker removed at startup, marker created privately only after successful bind, and marker deleted on orderly stop. Abnormal process death may leave a marker file, but reconnect validates live bind/token/version and never trusts that stale file as readiness.

Compose DIP means dp for the selected root; render_scale is that root's Density.density. Root-origin-relative bounds and logical width/height use dp; PNG dimensions are the actual captured pixel dimensions (rounding from root dp*density is recorded, not assumed exact after crop). Tree, frame and input pin one root generation and transform. ArcInputDispatcher maps pointer/wheel to ordered mouse-source MotionEvents on UiDispatcher, exactly one pointer; maps keys using a committed versioned Avalonia-name-to-Android-key table; text uses supported focused editable semantics set-text, otherwise unsupported. The reviewed table starts with Arrow/Enter/Escape/Tab/Back/Delete/Home/End/Page/letter/digit names and explicit unknown rejection, with platform/IME/layout acceptance fixtures before enabling each mapping. It is a port implementation artifact, not invented numeric key equivalence. No unsupported key becomes an arbitrary Android code. ComposeTargetAdapter exposes only the approved property allowlist and stable node generation; stale node or unsupported focus/property/text returns bounded CommandResult failure, which a client inspects even in an OK envelope. Raw shell or foreign UI controllers remain outside ARC's selected-root input/capture guarantee.

### Design vocabulary crosswalk

| Architecture interface or role | Defining code view / class |
| --- | --- |
| IDeviceCapabilityCollector / IVersionApplicabilityResolver / IResolvedTypeGraphBuilder | server-core-classes DeviceViewResolver owns these bounded internal strategies and immutable DeviceCapabilitySnapshot/ResolvedDeviceView DTOs |
| IDeviceViewStore / catalog projection | server-core-classes CatalogRepository / SqliteCatalogRepository; immutable SQLite staging and CAS activation |
| ICanonicalDispatchPlanner | server-execution-classes InvocationPlanner |
| IWireContractRegistry / IParcelCodec | server-execution-classes WireContractVerifier / CodecRegistry; typed codecs selected by exact proof |
| IGenericBinderTransactionEngine / IBinderTransport | server-execution-classes BinderBackend plus BinderHandleProvider; backend-specific reviewed native boundaries |
| IClientPolicyEvaluator / IAdmissionGate | server-core-classes AdmissionGate; one shared canonical evaluator |
| OwnerCapabilityIssuer | server-core-classes OwnerAuthenticationService; no second issuer or different token contract |
| Local Compose management facade | server-core-classes OwnerCommandFacade for protected mutations; SessionCommandFacade for bounded ordinary session commands |
| ICallbackBroker / IEventJournal / ILocalReplyPolicy | server-execution-classes CallbackBroker / EventJournal / LocalReplyPolicy |
| IFunctionalGroupIndex / IMcpToolViewAdapter | server-core-classes FunctionalGroupIndex / McpServer |
| Internal ARC engine | arc-ui-classes ArcRuntime/router/codec/auth; controller and target detail are explicit reference stubs in their scoped views |

Grouped018 residuals remain tracked individually in the reconciliation ledger. Acceptance evidence is proposed unless a specific retained local build/test receipt confirms it; an architecture table's AC label is never a claim that Android interoperability tests already passed.
'''
p.write_text(s)

# Maintain the historical correction source as a clearly non-normative audit document,
# so its former closed enum and renderer decisions cannot silently override the current model.
p=P/'architecture/round013-design-corrections.md'
s=p.read_text()
header='> Historical round013 correction record, superseded by the current canonical design. Not a current normative specification; retained for review provenance only.\n\n'
if not s.startswith(header): p.write_text(header+s)

# Status and callback failure descriptions are machine-addressable without repainting source images.
p=P/'docs/ui-screens-and-storyboards.md';s=p.read_text()
s+='\nEvery SCRxx.STATUS chip is a read-only AppBar projection of the same HealthSnapshot revision as the Android foreground notification; it is not an approval control. SCR05.S3/S4 show target death, lease expiry, orphan/revoke and confirmed versus unknown cleanup. Screen/card values are illustrative, not a running application.\n'
p.write_text(s)
print('Closed in-place consistency and ARC source checks')
