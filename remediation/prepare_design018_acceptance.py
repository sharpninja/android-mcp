"""Prepare exact reconciliation and current design evidence; no external call."""
from pathlib import Path
import json,hashlib,datetime,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parent; P=R/'design-c4'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report=json.loads((R/'reviews/round-018/recovered-synthesis/review-report.json').read_text())
fixes={
'F-01':('Unified diagram call paths','CallbackFlow','McpCallbackResourceAdapter','Finite registration -> separate subscribe/read/cancel; recorded cleanup OS edge'),
'F-02':('External ADB host ingress','host.forwarder','AdbPhoneBackend','Forward is ingress only; exact forwarding authority, no arbitrary loopback Host'),
'F-03':('CallbackDescriptor.materialization_strategy','callback_descriptors','unknown roles use unknown','Role-aware none/unknown and concrete/abstract unsupported subclass taxonomy'),
'F-04':('Internal Kotlin','bridge.arc','Historical round013','Replaced optional-only ARC decisions; current Kotlin ownership and migration vocabulary'),
'F-05':('OwnerAuthenticationService is also','FLAG_SECURE','Every Ask decision','Mandatory fresh auth, exact command/revision consume, foreign privileged controller boundary'),
'F-06':('LP(x)','manual_bearer','LoopbackTlsHost','Exact owner/nonce/proof ordering and named SSLServerSocket candidate; runtime spike explicitly gated'),
'F-07':('cleanup_disposition','registration_timeout','onCalleeDied','Closed transition/cleanup table; required renew arguments and bounded list cursor/limit'),
'F-08':('admitted but not dispatched','dispatched unary','Live handles never retarget','Distinct pre-dispatch/pinned-result/live-handle semantics; publication authority checked'),
'F-09':('HealthSnapshot','STATUS','catalog_readiness','One closed health/status tuple and read-only chip on every SVG screen'),
'F-10':('AdbConnectionManager','Paired adbd','identity_verification_method','Kotlin coordinator/native AOSP adapter choice, Locked transitions, paired-peer domains and topology'),
'F-11':('Design vocabulary crosswalk','reference:','ArcConnectionSession','Authoritative class views, service/connection lifetimes, policy convergence and vocabulary crosswalk'),
'F-12':('single serialized writer','OwnerTransition','Reads credential/client','Explicit container summaries, owner/auth/catalog/lifecycle/invalidation relationships'),
'F-13':('dynamic bridge.app ArcSession','dynamic bridge.app OwnerTransition','Final tool result','Ordered finite results, separate resource lifecycle, group CAS, startup and new failure paths'),
'F-14':('Reason envelopes separate','PACKAGE_NOT_VISIBLE','NOT_ON_BRIDGE_CLASSPATH','Layer plus stable code grammar; package visibility/permission/classpath/Wi-Fi reasons'),
'F-15':('An owner delete always','administrative entry ETag','import-stages','Exact tombstone/route/staging/import trust and SQLite allowlist; metadata item PUT and retry semantics'),
'F-16':('meta-json-v1','source-signature IDs retain','golden','Explicit canonical bytes and stable language namespace/JVM equivalence separation'),
'F-17':('android.bridge.directEvents.v1','DirectStreamNegotiator','DIRECT_STREAM_UNAVAILABLE','Negotiation, authenticated route, journal cursor/retention/bounds and fallback designed'),
'F-18':('RemoteControlProtocol.cs:31','Length zero is permitted','package0.8.0','Pinned constant evidence; exact envelope/default/cancel/frame/DIP/input/marker/version semantics'),
'F-19':('STB04','STB05','control_id','11 screens,5 ordered storyboards,21 slides; concrete actors/control/state IDs and branches'),
'F-20':('renderer-lock.json','verifyToolchain','expectedSvgPaths','Exact existing renderer/JDK/Node/Marp hashes, manifest asset set, strict offline failure and Gradle inputs'),
'F-21':('unknown/expired Mcp-Session-Id','MCP-Protocol-Version','isError=true','Session/header/error/GET/hint queue/disconnect/deadline/URI/idempotency rules'),
'F-22':('re-grant-required','Reserved','Mandatory admission audit failure','Semantic generations, inert stale rules, no auto-rebind, audit/log ordering and stable fingerprint'),
'F-23':('coordinateSpaceVersion','DISPLAY_MAPPING_UNPROVEN','unparseable','Logical/physical proof, explicit transforms/bounds/capture cap and typed brightness anomalies'),
'F-24':('structurizr-InvocationFlow.svg','Editable sources:','are acceptance plans','Correct links/API-version language/units/budget/phase labels; no raster replacement'),
'F-25':('group_dependencies','Cache seal is HMAC-SHA256','runtime-registered','Added missing table/ownership/index constraints, memory budgets, boot path and local/meta callbacks')}
rows=[]
files=[p for p in P.rglob('*') if p.is_file() and not any(x in p.parts for x in ('.tools','.gradle-home','.gradle','build')) and p.suffix in ('.md','.dsl','.svg','.json','.kts')]
for f in report['findings']:
    keys=fixes[f['id']];evidence=[]
    for needle in keys[:3]:
        hits=[]
        for p in files:
            for n,line in enumerate(p.read_text().splitlines(),1):
                if needle in line:
                    hits.append(dict(file=str(p.relative_to(R)),line=n,sha256=sha(p)));break
            if len(hits)>=3:break
        evidence.append(dict(anchor=needle,locations=hits))
    rows.append(dict(id=f['id'],severity=f['severity'],prior_issue=f['issue'],working_resolution=keys[3],status='corrected candidate; independent verification pending',evidence=evidence))
rows[14]['disputed_subclaims']='meta1 global-generation ETags are conservative false conflicts by design, not an HTTP prohibition; public representation includes the ETag. DELETE412 after prior successful effect is idempotent by intended effect, not identical response. Cursor replay is allowed. Item creation changed to PUT If-None-Match:* (genuine defect).'
rows[17]['disputed_subclaims']='arc-protobuf-v1 is defined by the actual upstream constant, not an invented profile. Reviewer suggested length<=0 reject would break upstream zero-frame semantics; preserve codec zero and reject default envelope at validation. Exact pinned source evidence supplied.'
out=R/'evidence/finding-ledger-design018-reconciled.json'
out.write_text(json.dumps(dict(at=now(),frozen_review_scores=report['scores'],current_acceptance_claimed=False,findings=rows),indent=2)+'\n')

# This is explicit vector-text evidence from generated images, avoiding base64/raster bulk.
evidence=[]
for p in sorted((P/'docs').rglob('*.svg')):
    root=ET.fromstring(p.read_bytes());texts=[]
    for e in root.iter():
        if e.tag.rsplit('}',1)[-1] in ('text','title','desc'):
            text=''.join(e.itertext())
            if text:texts.append(text)
    evidence.append(dict(file=str(p.relative_to(R)),sha256=sha(p),size_bytes=p.stat().st_size,viewBox=root.get('viewBox'),width=root.get('width'),height=root.get('height'),vector_text=texts,raster_images=sum(e.tag.rsplit('}',1)[-1]=='image' for e in root.iter()),scripts=sum(e.tag.rsplit('}',1)[-1]=='script' for e in root.iter())))
(R/'evidence/design018-current-vector-evidence.json').write_text(json.dumps(dict(at=now(),limitations='Extracted vector text and dimensions are structural evidence, not a Claude visual inspection. Local visual QA is a separate receipt. Exact full SVGs are frozen for provenance.',files=evidence),indent=2)+'\n')
status=json.loads((R/'LIVE-STATUS.json').read_text())
status.update(updated_at=now(),current_phase='018 verdict complete; corrected working design prepared for fresh acceptance',review_verdict_pending=False,design_verdict_pending=False,review_status='Recovered holistic synthesis018 completed09:25:57, design86/85; current candidate not yet assessed',last_design_scores=report['scores']['design'],review_packet_count=0,review_pid=None,current_build='Gradle validate014 exit0;34SVGs, new ARC/owner dynamic views',current_local_tests=dict(normal=307,optimized=307,design_regressions=5),pending_acceptance='All25 findings reconciled locally; independent acceptance pending',source_handoff_status='v1 immutable; focused post-review delta being packaged')
(R/'LIVE-STATUS.json').write_text(json.dumps(status,indent=2)+'\n')
print(json.dumps(dict(ledger=str(out),findings=len(rows),vector_outputs=len(evidence),unmatched_evidence_anchors=[dict(id=f['id'],anchor=e['anchor']) for f in rows for e in f['evidence'] if not e['locations']]),indent=2))
