"""Generate explicit SVG screen/state contracts and actor-aware Marp storyboards."""
from pathlib import Path
import json,html,textwrap,re
R=Path(__file__).resolve().parent;P=R/'design-c4';D=P/'ui-svg';mp=P/'docs/ui-asset-manifest.json';m=json.loads(mp.read_text())
screens={x['id']:x for x in m['screens']}
# Original IDs retain their semantic action. New affordances get new IDs.
extra={
'SCR01':[(4,'Open health details','navigation','Show catalog readiness, ARC listener, MCP, ADB and device-view revision independently')],
'SCR02':[(4,'Add client','navigation','Open SCR11; also enabled in the no-client state'),(5,'Select client','selector','Stable client ID, credential kind and 12-hex display fingerprint'),(6,'Choose exact rule','editor','Deny / Ask / Allow; canonical binding/schema and target/user scope; draft only'),(7,'Discard grant draft','cancel','Delete unsaved draft; no grant change')],
'SCR03':[(4,'Search inventory','query','Text/type/namespace/version/evidence filters; unknown and unavailable included'),(5,'Next catalog page','pagination','Opaque snapshot cursor and configured byte/page bounds'),(6,'Choose import document','document-picker','Android SAF document; stage and validate before a protected activation review'),(7,'Preview override diff','editor','Exact field/schema/type dependency diff, revisions and CAS conflicts'),(8,'Discard override or import','cancel','Delete unactivated staging/draft; immutable import remains history')],
'SCR04':[(4,'Select client/session','selector','Owner preview only; cannot impersonate an MCP client'),(5,'Describe job','text-field','Task text plus optional constraints; summaries within max_bytes'),(6,'Search group summaries','query','Deterministic labels/index by default; optional semantic ranking never grants'),(7,'Describe selected group','pagination','Pinned revision/hash, cursor, max_bytes; fetch only requested manifest page'),(8,'Discard mapping draft','cancel','No mapping or session activation change')],
'SCR05':[(3,'Open navigation','navigation','Open local navigation; no client identity assertion'),(4,'Select subscription','selector','Owner-filtered principal, stream epoch, state, cleanup phase and disposition'),(5,'Read event page','pagination','Bounded owned journal cursor; gap/terminal status separate from payload'),(6,'Refresh status','read','Status after owner cancellation, lease expiry, death or cleanup uncertainty')],
'SCR06':[(4,'Filter logs','query','Owner selects client/time/outcome; redacted bounded snapshot'),(5,'Choose export document','document-picker','Android SAF create-document after protected exact-snapshot approval'),(6,'Edit retention quotas','editor','Protected settings review; mandatory audit reserve and optional-log quotas separate'),(7,'Cancel export','cancel','No document written; release sealed export snapshot')],
'SCR07':[(5,'Select target profile','selector','Exact outbound endpoint, target/user/profile and protected bearer reference'),(6,'Inspect tree or refresh frame','read','Frame viewport and tree selection; ID/version/status then CommandResult checks'),(7,'Pointer or wheel input','viewport','Current-root dp coordinates; one mouse pointer, serialized lease, no stale-frame guarantee'),(8,'Inspect target details','read','arc-protobuf-v1 / envelope1.0 / package0.8.0 are distinct; PNG scale and bounds')],
'SCR08':[(4,'Discard settings draft','cancel','Return to last committed revision without changing boot/listeners/grants'),(5,'Edit startup and MCP listener','editor','Boot opt-in, loopback address/port, exact Origin allowlist, TLS profile; all save protected'),(6,'Edit same-device ADB profile','editor','Device/user, pairing host/port/code and discovered connect port; no debugging toggle'),(7,'Edit inbound ARC listener','editor','Enabled, loopback bind/port, selected ordinary root, per-client action grants'),(8,'Rotate inbound ARC credential','protected-action','Fresh SCR09 review; invalidate credential epoch and active streams/leases'),(9,'Inspect inbound controllers','read','Bound/listener state, authenticated principals, sessions, root and denied reasons'),(10,'Edit log quotas','editor','Optional payload retention, mandatory audit reserve and journal quotas; protected save')],
'SCR09':[(3,'Locked navigation','disabled','No route switch until challenge completes/cancels; owner touch remains available'),(4,'Inspect exact challenge','read','Command hash, revisions, scope, monotonic expiry, queued Ask count; no secrets')],
'SCR10':[(4,'Open catalog import','navigation','Route SCR03.C06 when catalog absent; no fabricated ready state'),(5,'Inspect failure reason','read','Exact health snapshot and sanitized reason; retry never changes permissions')],
'SCR11':[(4,'Choose pairing flow','selector','manual_bearer_setup or pairing_helper_v1; latter requires paired_tls trust'),(5,'Inspect submitted client proof','read','Exact key/fingerprint, nonce, Origin and endpoint; proof alone grants nothing'),(6,'Review exact client commit','protected-action','Fresh SCR09 proof after client response, before principal commit; grants start Deny'),(7,'Copy manual bearer once','protected-action','Fresh exact-owner reveal; store verifier only; never logs or remote root')]
}
for sid,rows in extra.items():
    x=screens[sid];details=x.setdefault('control_specs',{})
    for n,label,kind,behavior in rows:
        cid=sid+'.C'+f'{n:02}';x['controls'][cid]=label;details[cid]=dict(kind=kind,behavior=behavior)
    for cid,label in x['controls'].items():details.setdefault(cid,dict(kind='action',behavior='Exact named action; protected mutations open SCR09; reads never change authority'))
    # Navigation is explicit and has the same menu contract at both widths.
    if sid!='SCR09':x['controls'][sid+'.C03']='Open navigation';details[sid+'.C03']=dict(kind='navigation',behavior='Drawer on phone; rail on wide layout. Destinations SCR01..SCR08,SCR10,SCR11; Back discards only after explicit discard/cancel decision.')
    x['states'].setdefault(sid+'.S0','Depicted example; service and target statuses shown independently')
    allids=list(x['controls']);x['state_specs']={}
    for state,label in x['states'].items():
        disabled=[]
        if sid=='SCR07' and not state.endswith('.S4'):disabled=[sid+'.C04',sid+'.C07']
        if sid=='SCR09':disabled=[sid+'.C03']
        x['state_specs'][state]=dict(label=label,enabled_controls=[c for c in allids if c not in disabled],disabled_controls=disabled,
          effect='Read-only until exact protected commit; error/conflict never applies a partial mutation.',
          recovery='Owner may inspect reason, explicitly retry when safe, or cancel/discard; no automatic uncertain write replay.')
    x['health_example']=dict(service='Error' if sid=='SCR10' else 'Running',desired_state='running',mcp='error' if sid=='SCR10' else 'ready',adb='offline',device_view='stale' if sid=='SCR10' else 'ready',catalog='quarantined' if sid=='SCR10' else 'ready',arc_listener='disabled',revision='r9-error' if sid=='SCR10' else 'r9-ready')
    x['phone_layout']=dict(width_dp=390,navigation='Top AppBar menu opens modal drawer with all screen destinations; Back returns previous screen. SCR09 disables drawer and intercepts navigation until cancel/complete.',content='Single-column scroll, 48dp actions, filters above results; detail/preview replaces list pane with Back; no compressed rail.',landscape='Wide rail and list/detail layout when sufficient width; IDs/actions unchanged.')
# Preserve explicit empty/denied/error variants and add state contracts with real recovery.
screens['SCR08']['states']['SCR08.S5']='Inbound ARC disabled: no socket, sessions or exposed root'
screens['SCR08']['states']['SCR08.S6']='Inbound ARC ready: bound127.0.0.1:port, approved ordinary root and controller list'
screens['SCR11']['states']['SCR11.S4']='Proof submitted: owner exact-commit review pending; no principal yet'
screens['SCR11']['states']['SCR11.S5']='Paired: client committed, all rules Deny until separately approved'
screens['SCR03']['states']['SCR03.S4']='Override/import preview: dependencies, exact diff, hash and CAS shown; not active'
screens['SCR06']['states']['SCR06.S4']='Export approved: Android create-document picker; cancel writes nothing'
screens['SCR01']['states']['SCR01.S4']='Approval pending: notification opens SCR09 exact queued request, never approves'
screens['SCR07']['states']['SCR07.S4']='Connected: C06 refreshes frame/tree; C04/C07 require current target capability and policy'
for x in screens.values():
    for state,label in x['states'].items():x['state_specs'].setdefault(state,dict(label=label,enabled_controls=list(x['controls']),disabled_controls=[],effect='Exact state shown; no automatic authority change.',recovery='Inspect, explicitly retry or cancel; stale revisions reload.'))
disabled_by_state={
 'SCR01.S1':[1], 'SCR02.S1':[1,2,5,6], 'SCR02.S3':[1,2],
 'SCR03.S1':[1,2,5,7], 'SCR03.S2':[2,7], 'SCR03.S3':[2,7],
 'SCR04.S2':[2], 'SCR04.S3':[2], 'SCR05.S1':[1,2,4,5],
 'SCR05.S3':[5], 'SCR05.S4':[2], 'SCR06.S1':[2,5], 'SCR06.S3':[2,5],
 'SCR07.S0':[4,6,7], 'SCR07.S1':[4,6,7], 'SCR07.S2':[4,6,7], 'SCR07.S3':[4,6,7],
 'SCR08.S1':[1,2,8], 'SCR08.S3':[1,2,8],
 'SCR09.S0':[3], 'SCR09.S1':[1,3], 'SCR09.S2':[1,3], 'SCR09.S3':[1,3],
 'SCR11.S1':[5,6,7], 'SCR11.S2':[5,6,7], 'SCR11.S3':[5,6,7], 'SCR11.S5':[1,5,6]
}
for state,numbers in disabled_by_state.items():
    sid=state.split('.')[0];x=screens[sid];spec=x['state_specs'][state]
    spec['label']=x['states'][state];spec['disabled_controls']=[sid+'.C'+f'{n:02}' for n in numbers]
    spec['enabled_controls']=[c for c in x['controls'] if c not in spec['disabled_controls']]
for x in screens.values():
    for state,spec in x['state_specs'].items():spec['label']=x['states'][state]
screens['SCR04']['control_specs']['SCR04.C02']['behavior']='Protected owner group-mapping override, preview exact revision then SCR09; this is not client-session set_active.'
screens['SCR05']['control_specs']['SCR05.C02']['behavior']='Request protected administrative cancellation in SCR09; mark delivery closed immediately after admitted commit; status/event terminal remains readable.'
screens['SCR01']['control_specs']['SCR01.C01']['behavior']='Stopped: eligible local foreground-service start intent. Running: ordinary stop through service facade. Neither changes configuration.'
screens['SCR09']['control_specs']['SCR09.C01']['behavior']='Service-owned exact challenge and OS prompt; Activity completion is trusted in this single process, not cryptographic attestation against a compromised app. Exact one-use CAS; timeout/deny makes no commit.'
screens['SCR09']['control_specs']['SCR09.C02']['behavior']='Dismiss current challenge, return denial and originating screen. Queue advances without reusing proof.'

def st(sid,n,purpose,state=0,actor='Owner',result='',error='',cancel='',interaction='control'):
    cid=sid+'.C'+f'{n:02}'
    return dict(screen_id=sid,state_id=sid+'.S'+str(state),control_id=cid,actor=actor,interaction=interaction,action=screens[sid]['controls'][cid],purpose=purpose,result=result or 'Show exact resulting state; no implicit permission change',error=error or 'Show sanitized denial/failure and retain unchanged revisions',cancel=cancel or 'Cancel/back returns without the uncommitted effect',alternate='Error: '+(error or 'deny without partial commit')+'; Cancel: '+(cancel or 'return unchanged'))
stories=[
('STB01','Agent discovers groups, subscribes and owner cancels',[
st('SCR04',6,'External android.groups.discover; owner preview updates only',actor='External MCP client',interaction='external_event',result='Ranked summaries bounded by max_bytes; no authority granted'),
st('SCR04',7,'External describe then set_active CAS',actor='External MCP client',interaction='external_event',result='Atomic client group view; portable binding.invoke remains'),
st('SCR05',6,'External binding.invoke with callback, resources/subscribe, bounded read',actor='External MCP client',interaction='external_event',result='Finite handle then URI-only hint and journal read; callback list updates'),
st('SCR05',1,'Owner inspects exact subscription and cleanup phase'),st('SCR05',2,'Open exact administrative cancellation review',result='SCR09 challenge binds selected subscription/revision'),
st('SCR09',1,'Confirm this administrative cancellation',result='Delivery closed and recorded cleanup issued once'),
st('SCR05',6,'Read terminal event/status',state=4,result='Client learns cancellation through callback.status or retained terminal journal; cleanup confirmed or unknown'),
st('SCR05',6,'Inspect cleanup uncertainty without replay',state=3,error='Uncertain cleanup keeps phase=cleanup and delivery closed'),
st('SCR09',2,'Cancellation alternative before commit',result='Return SCR05; registration unchanged')]),
('STB02','ARC select, inspect viewport, control and disconnect',[
st('SCR07',5,'Choose exact outbound compatible target profile'),st('SCR07',1,'Connect and request capabilities',result='First response ID/version/status checked; no handshake'),
st('SCR07',6,'Inspect frame viewport and selected tree node',state=4,result='Current-root dimensions/density visible; stale node rejected'),
st('SCR07',7,'Press, move and release across unary requests',state=4,result='Principal/root lease serializes current-coordinate input; no wire generation invented'),
st('SCR07',4,'Send key/text using approved named mapping',state=4,error='Unknown key or failed CommandResult is displayed even with OK envelope'),
st('SCR07',2,'Disconnect controller',state=4,result='Best-effort releases then watch socket close; idle lease bound covers network failure'),
st('SCR07',1,'Inspect wrong-token failure',state=2,error='No tree/frame data or enabled input controls'),
st('SCR08',9,'Inspect inbound listener separately from outbound controller',state=6,result='Bound loopback endpoint, exposed ordinary root and incoming principals shown')]),
('STB03','Protected configuration, start and truthful failure',[
st('SCR08',5,'Edit exact boot/listener draft'),st('SCR08',4,'Discard alternative',result='No committed setting changes'),
st('SCR08',1,'Save reviewed exact draft',result='SCR09 shows exact values/revisions'),st('SCR09',1,'Fresh protected proof and CAS',result='Return SCR08 committed revision; queue next pending challenge'),
st('SCR08',3,'Navigate to dashboard'),st('SCR01',1,'Request foreground start',state=3,result='Starting, admission closed, then actual ready'),
st('SCR01',4,'Inspect healthy tuple',result='MCP ready is separate from ADB paired-offline'),
st('SCR10',5,'Failure branch after denied/failed start',error='Error badge, no Running; exact recovery reason'),
st('SCR10',1,'Explicit retry after prerequisite fixed',result='No permission or settings bypass; Starting reevaluated'),
st('SCR09',2,'Deny/cancel configuration review alternative',result='Return SCR08 previous committed revision')]),
('STB04','Pair proof, exact commit, grant and revoke',[
st('SCR02',4,'Open Add client, including empty-client state',state=1),st('SCR11',4,'Select helper flow and explicit TLS trust'),
st('SCR11',1,'Request one-time scoped challenge'),st('SCR09',1,'Approve pairing window/attempt',result='Nonce available for exact client; no principal yet'),
st('SCR11',5,'Client presents pinned nonce/key/Origin proof',state=4,actor='External MCP client',interaction='external_event',result='Service validates proof; owner sees exact pending fingerprint'),
st('SCR11',6,'Review exact submitted client commit',state=4),st('SCR09',1,'Confirm this client identity/revisions',result='Return SCR11 Paired, default Deny'),
st('SCR11',3,'Navigate to clients',state=5),st('SCR02',1,'Edit exact binding/schema/target rule draft'),st('SCR09',1,'Approve grant CAS',result='Return SCR02 Allow/Ask/Deny committed as reviewed'),
st('SCR02',2,'Request credential revoke'),st('SCR09',1,'Confirm exact revoke',result='Epoch increments, streams/input close, pending calls recheck'),
st('SCR11',2,'Cancel attempt alternative',state=3,result='No client committed'),st('SCR11',1,'Expired challenge alternative',state=1,error='New nonce required; no reuse')]),
('STB05','Catalog preview and scoped log export',[
st('SCR03',4,'Search binding/type evidence including unknown'),st('SCR03',1,'Inspect selected evidence and runtime-versus-grant distinction'),
st('SCR03',2,'Edit local override draft'),st('SCR03',7,'Preview exact diff and dependency/CAS effects',state=4),st('SCR09',1,'Approve exact override publication',result='Catalog epoch invalidates affected device/group views'),
st('SCR06',4,'Select clients/time/outcome and redacted snapshot'),st('SCR06',2,'Request selected-scope export',result='SCR09 shows immutable redacted snapshot and limits'),
st('SCR09',1,'Approve exact export snapshot',result='Return SCR06 ready for create-document picker'),st('SCR06',5,'Choose Android SAF destination',state=4,result='Write only approved redacted snapshot'),
st('SCR06',7,'Cancel picker alternative',state=4,result='No document written'),st('SCR06',1,'Quota/audit failure branch',state=3,error='Mandatory audit failure closes admission; optional logs evicted first'),
st('SCR03',6,'Absent-catalog import branch',result='SAF copy/validation stage; protected activation review required')]),
('STB06','Pair self-ADB and inspect independent backend health',[
st('SCR08',6,'Enter device/user and Android-provided pairing host/port/code'),st('SCR08',2,'Request exact pair/connect command',result='SCR09 review; no Android debugging toggle'),
st('SCR09',1,'Authenticate exact profile and peer request',result='Bounded native pairing/connect starts'),st('SCR08',9,'Observe paired peer/backend status',actor='Android backend',interaction='external_event',result='Status change is evidence, never an MCP grant'),
st('SCR08',3,'Navigate dashboard'),st('SCR01',4,'Inspect MCP ready versus ADB Paired offline/Connected'),
st('SCR08',2,'Wrong peer/code branch',state=4,error='Unverified peer never becomes Connected'),st('SCR09',2,'Cancel pairing alternative',result='No profile/key change')])]
m['storyboards']=[]
for sid,title,steps in stories:m['storyboards'].append(dict(id=sid,title=title,steps=steps,boundary='External events are observed on the anchored UI control, not a remote press on that control. Protected mutations require exact local owner authentication; discovery/activation/pairing does not grant device authority. Self-ADB does not grant any MCP client capability.'))
m['schema_version']='1.2.0';m['shared_status']=dict(source='HealthSnapshot',fields=['service','desired_state','mcp','adb','device_view','catalog','arc_listener'],revision='One immutable revision shared by AppBar and notification',error_rule='Error/Stopped never displays Running')
m['implemented_android_ui']=False;m['external_step_contract']='For interaction=external_event, control_id is an observation anchor only; actor performs the named MCP/backend operation described in purpose, never the UI control action.'

esc=lambda x:html.escape(str(x),quote=True)
def text(x,y,t,size=17,weight=400,color='#20354a'):return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{color}">{esc(t)}</text>'
def box(x,y,w,h,color='#fff'):return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{color}" stroke="#b8c9d8"/>'
def wrap(x,y,t,width=66,size=16,step=22,color='#20354a'):return ''.join(text(x,y+i*step,line,size,color=color) for i,line in enumerate(textwrap.wrap(t,width,break_long_words=False)))
def svg(title,body,w,h):return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img"><title>{esc(title)}</title><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>'
for sid,x in screens.items():
    h=x['health_example'];count=len(x['controls']);rows=(count+1)//2;statey=max(1120,200+rows*140);height=statey+80+len(x['states'])*112
    b=box(0,0,1680,height,'#f2f6fa')+text(28,44,sid+' — '+x['title'],29,700)+text(28,76,'Kotlin Compose screen definition · design only · '+x['requirements'],17)
    chip='Service '+h['service']+' (desired '+h['desired_state']+') · MCP '+h['mcp']+' · ADB '+h['adb']+' · view '+h['device_view']+' · catalog '+h['catalog']+' · ARC '+h['arc_listener']+' · '+h['revision']
    b+='<g data-status-id="'+sid+'.STATUS">'+box(24,96,1630,55,'#fff0e7' if sid=='SCR10' else '#e3f2e9')+text(40,130,chip,16,700)+'</g>'
    b+=text(28,184,'Wide layout: persistent rail + scrollable list/detail; control contracts',20,700)
    for i,(cid,label) in enumerate(x['controls'].items()):
        xx=28+(i%2)*588;yy=210+(i//2)*140;spec=x['control_specs'][cid]
        color='#e3eaf1' if spec['kind']=='disabled' else '#fff'
        b+='<g data-control-id="'+cid+'" data-kind="'+spec['kind']+'">'+box(xx,yy,572,128,color)+text(xx+12,yy+26,cid+' · '+label,17,700)+wrap(xx+12,yy+51,spec['behavior'],65,13,18)+'</g>'
    if sid=='SCR07':
        vy=210+rows*140;b+=box(28,vy,1160,150,'#e1edf6')+text(44,vy+36,'Frame viewport / tree selection',21,700)+wrap(44,vy+69,'Pointer uses current root dp coordinates; display PNG dimensions, render_scale and last sequence. Tree rows use stable generation-scoped node IDs. Input disabled until exact target capability/policy is verified. Open full frame at device aspect ratio; no fabricated pixels in this design.',100,17,24)
    b+=box(1220,172,430,744,'#fff')+text(1238,208,'Phone · 390dp single pane',23,700)+box(1238,226,394,62,'#e3eaf1')+text(1251,263,'☰ Menu · '+sid+' · status',19,700)
    if sid=='SCR09':b+=wrap(1240,322,'Navigation locked and styled disabled. Exact challenge remains visible; Cancel always available. Remote input quiesced in service before OS prompt.',34,19,28)
    else:b+=wrap(1240,322,'Menu opens all destinations in a modal drawer. Filters precede a paged list. Selecting a row replaces list with detail; Back returns list. Scroll retains draft and focused field.',34,19,28)
    b+=box(1240,590,390,64,'#234b70')+text(1253,628,sid+'.C01 · primary action',19,700,'#fff')+box(1240,670,390,64,'#f0f4f8')+text(1253,709,'48dp targets; no compressed rail',18)+wrap(1240,772,'All wide-layout control IDs map to the same actions here. State enablement below is authoritative.',34,17,24)
    b+=text(28,statey,'State definitions — enabled controls, effects and recovery',24,700)
    for i,(state,spec) in enumerate(x['state_specs'].items()):
        yy=statey+24+i*112;b+='<g data-state-id="'+state+'">'+box(28,yy,1622,101,'#fff6e8')+text(42,yy+25,state+' · '+spec['label'],17,700)+wrap(42,yy+51,'Disabled: '+(', '.join(spec['disabled_controls']) or 'none except standard permission/target readiness gates')+'. '+spec['effect']+' '+spec['recovery'],177,14,20)+'</g>'
    (D/(sid+'.svg')).write_text(svg(sid+' '+x['title'],b,1680,height))

index='# Kotlin Compose screen definitions and actor-aware storyboards\n\nCanonical editable SVG source is ui-svg; Gradle copies identical bytes to docs/screens. Action targets are at least 48dp. Screen state/control contracts below are design specifications, not implemented Android UI.\n\n'
deck='---\nmarp: true\ntheme: default\npaginate: true\nsize: 16:9\nstyle: |\n  section { font-size:22px; color:#20354a; }\n  h1 { font-size:30px; }\n---\n\n# Kotlin Compose UI storyboards\n\nOwner, external MCP client and Android backend are explicit actors. External event control IDs are observation anchors, never remote presses. Happy, error and cancellation branches are distinct steps.\n'
for sid,x in screens.items():
    index+='## '+sid+' — '+x['title']+'\n\n'+x['requirements']+'\n\n![Screen definition](screens/'+sid+'.svg)\n\n'
    for cid,label in x['controls'].items():index+='- `'+cid+'` **'+label+'**: '+x['control_specs'][cid]['behavior']+'\n'
    index+='\nPhone: '+x['phone_layout']['navigation']+' '+x['phone_layout']['content']+'\n\n'
    for state,spec in x['state_specs'].items():index+='- `'+state+'`: '+spec['label']+' Disabled: '+(', '.join(spec['disabled_controls']) or 'standard admission/readiness gating only')+'. '+spec['recovery']+'\n'
    index+='\n'
for story in m['storyboards']:
    sid=story['id'];steps=story['steps'];cols=3;rows=(len(steps)+cols-1)//cols;hh=130+rows*715+130
    b=box(0,0,1680,hh,'#f2f6fa')+text(28,42,sid+' — '+story['title'],26,700)+text(28,82,'Panels show explicit state, actor, control/observation, action, success, error and cancel.',19)
    for i,step in enumerate(steps):
        xx=28+(i%cols)*550;yy=115+(i//cols)*715;x=screens[step['screen_id']];spec=x['state_specs'][step['state_id']]
        b+=box(xx,yy,526,690)+text(xx+16,yy+30,f'{i+1}. '+step['actor']+' · '+step['state_id'],18,700)+wrap(xx+16,yy+61,step['purpose'],46,18,24)
        b+=box(xx+16,yy+137,494,180,'#e2ecf5')+text(xx+28,yy+164,step['screen_id']+' · '+x['title'],18,700)+wrap(xx+28,yy+194,spec['label'],48,15,22)+wrap(xx+28,yy+270,('Observation anchor: ' if step['interaction']=='external_event' else 'Control: ')+step['control_id']+' '+step['action'],48,14,20)
        b+=text(xx+16,yy+352,'Success →',17,700)+wrap(xx+16,yy+378,step['result'],52,15,22)+text(xx+16,yy+478,'Error →',17,700)+wrap(xx+16,yy+504,step['error'],52,14,20)+text(xx+16,yy+581,'Cancel →',17,700)+wrap(xx+16,yy+607,step['cancel'],52,14,20)
        deck+='\n---\n\n# '+sid+' · '+str(i+1)+'. '+step['purpose']+'\n\n![bg right:58% contain](../docs/screens/'+step['screen_id']+'.svg)\n\n**'+step['actor']+'** · `'+step['state_id']+'`\n\n'+('Observation: ' if step['interaction']=='external_event' else 'Action: ')+step['control_id']+' '+step['action']+'\n\n**Success:** '+step['result']+'\n\n**Error:** '+step['error']+'\n\n**Cancel:** '+step['cancel']+'\n'
    b+=wrap(28,hh-85,story['boundary'],165,16,24);(D/(sid+'.svg')).write_text(svg(sid+' '+story['title'],b,1680,hh))
    index+='## '+sid+' — '+story['title']+'\n\n'+story['boundary']+'\n\n![Ordered storyboard](screens/'+sid+'.svg)\n\n'
index+='[Editable Marp deck](../storyboards/storyboard.md) · [Self-contained HTML](storyboards.html)\n'
mp.write_text(json.dumps(m,indent=2)+'\n');(P/'docs/ui-screens-and-storyboards.md').write_text(index);(P/'storyboards/storyboard.md').write_text(deck)
count=1+sum(len(x['steps']) for x in m['storyboards'])
p=P/'docs/Android MCP Bridge Design.md';s=p.read_text();s=re.sub(r'all\s*25\s*slides',f'all {count} slides',s);s=re.sub(r'25-slide',f'{count}-slide',s)
a=s.index('| Screen ID |');b=s.index('\n\n',s.index('| Storyboard ID |',a))
table='| Screen ID | Definition / primary actions |\n| --- | --- |\n'+''.join('| ['+x['id']+'](screens/'+x['id']+'.svg) '+x['title']+' | '+'; '.join(x['controls'].values())+' |\n' for x in m['screens'])+'\n| Storyboard ID | Actor-aware flow |\n| --- | --- |\n'+''.join('| ['+x['id']+'](screens/'+x['id']+'.svg) | '+x['title']+' |\n' for x in m['storyboards'])
s=s[:a]+table.rstrip()+s[b:]
# This generated final section is replaced, never accumulated on regeneration.
start=s.find('\n\nUI contracts are canonical in docs/ui-asset-manifest.json')
if start>=0:
    ending='redacted-snapshot approval and writes nothing if the picker is cancelled.'
    finish=s.index(ending,start)+len(ending)
    s=s[:start]+s[finish:]
s+='''

UI contracts are canonical in docs/ui-asset-manifest.json and generated SVG screen
specifications. SCR08 owns the inbound ARC listener controls/root/credential rotation;
SCR07 is the separate outbound controller with frame viewport, tree and typed input.
SCR03 covers SAF import and override dependency/CAS preview. SCR04 mapping saves and
all SCR08 persistent setting changes are protected; ordinary navigation, reads and
start/stop are not. Every draft has explicit discard/cancel. No owner screen asserts
another client's MCP identity. External client proof and protected final commit are
separate STB04 panels. STB01 includes real external discovery/activation/registration;
owner cancellation and the client's terminal-status observation remain distinct.
Event cursor expiry arises from configured time retention or explicitly selected
drop-oldest policy, not from the default close-on-overflow policy. Orphan metadata
is retained for the configured diagnostic lifetime (default7days), with payload access
closed; verified cleanup obligations survive independently until resolved/acknowledged.
Mandatory audit failure is fail-closed for new dispatch; optional logs are evicted
before the mandatory reserve. Export uses Android SAF create-document after exact
redacted-snapshot approval and writes nothing if the picker is cancelled.
''';p.write_text(s)
print(json.dumps(dict(screens=len(m['screens']),storyboards=len(m['storyboards']),slides=count,controls=sum(len(x['controls']) for x in m['screens']),frozen_reviews_modified=False)))
