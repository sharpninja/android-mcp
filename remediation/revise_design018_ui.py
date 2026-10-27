"""Refine editable SVG screen/storyboard contracts; not a Compose implementation."""
import ast,pathlib,html,json,textwrap,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';D=P/'ui-svg'
tree=ast.parse((R/'author_svg_ui.py').read_text());data={}
for node in tree.body:
 if isinstance(node,ast.Assign) and isinstance(node.targets[0],ast.Name) and node.targets[0].id in ('specs','navigation'):data[node.targets[0].id]=ast.literal_eval(node.value)
specs=data['specs'];navigation=data['navigation']
def change(sid,heading=None,rows=None,actions=None,states=None):
 for i,row in enumerate(specs):
  if row[0]==sid:
   values=list(row)
   for index,value in ((3,heading),(4,rows),(5,actions),(6,states)):
    if value is not None:values[index]=value
   specs[i]=tuple(values);return
 raise ValueError(sid)
change('SCR01',rows=[('MCP service','Example: Running / listener ready','AppBar and notification reflect this same service state'),('ADB backend','Disconnected','MCP health remains separate'),('Device view','Ready / Stale / Refreshing / Failed','Selected view and capability epoch shown separately'),('Active work','2 subscriptions / 1 client session','Illustrative data; scoped status and cancellation')])
change('SCR04',heading='Owner preview for selected MCP client/session',actions=['Inspect group summary','Save group mapping'],states=['Empty: unassigned entries searchable','Budget preview exceeded: save no partial group','Conflict: reload mapping revision'])
change('SCR05',heading='Owner view of selected client subscription metadata',actions=['Inspect bounded status','Cancel via owner review'],states=['Empty: no registrations','Cursor expired: show retention boundary','Backend death/orphan/revoke: payload hidden; cleanup may be unknown','Closed delivery: show confirmed or unknown cleanup'])
change('SCR07',states=['Wrong token: no target content','Unknown protocol: reject before send','Protected root: capture/input denied','Connected: C01 refreshes frame; C04 enabled only by current capability/policy'])
change('SCR08',rows=[('Start on boot','Off','Unlock and eligible foreground start required'),('MCP listener','Loopback address / allocated port / origin policy','Owner-reviewed TLS/auth profile; no public bind'),('ADB profile','Owner-paired identity and endpoint health','Host loopback forward is explicit; no device action here'),('ARC profile','Disabled until owner enables it','Bearer secret protected; raw TCP is not TLS')])
change('SCR09',rows=[('Requested operation','Exact client / binding / target','Single-use command nonce; changes invalidate approval'),('Pinned plan','Contract hash / revision / expiry','No UI or JSON assertion can create owner authority'),('Input boundary','Bridge-owned remote input paused','Secure owned windows; foreign ADB is not quiesced by us'),('Authentication','Fresh local owner authentication','Dismissal/expiry denies; navigation stays locked')],states=['Expired: no execution','Plan changed: request new review','Bridge remote input denied; uncertain isolation blocks approval'])
change('SCR10',rows=[('MCP service','Error / listener not ready','Admission closed; success is displayed on SCR01'),('Android reason','Start eligibility or permission prerequisite','Owner action means review this precise Android reason'),('ADB backend','Not connected','Separate from MCP and device-view readiness'),('Recovery','Review prerequisites locally','No automatic device-setting or security bypass')],states=['No catalog: import validated release','Probe disconnected: preserve unknown facts','Cancel: desired Stopped state retained'])
specs.append(('SCR11','Client pairing','FR-KT-02 / canonical client identity','Local owner pairing and challenge review',[
 ('Client identity','Name plus authenticated principal binding','Display label never grants authority'),('Endpoint','Loopback origin / TLS profile / client key','Show exact server identity and scope'),('Challenge','One-time nonce and expiry','Reveal or rotate pairing material only after owner auth'),('Result','Unpaired / Pending / Paired / Revoked','No grant until exact owner-reviewed commit')],['Generate pairing challenge','Cancel pairing'],['Expired challenge: create a new one','Wrong proof/origin: deny pairing','Cancel: no principal or grant committed']))
esc=lambda value:html.escape(str(value),quote=True)
def text(x,y,value,size=14,weight=400,fill='#20354a'):
 return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{fill}">{esc(value)}</text>'
def rect(x,y,w,h,fill='#ffffff'):
 return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{fill}" stroke="#cfdae5"/>'
def lines(x,y,value,width=75,size=13,step=18,fill='#20354a'):return ''.join(text(x,y+j*step,v,size,fill=fill) for j,v in enumerate(textwrap.wrap(value,width)))
for sid,title,fr,heading,rows,actions,states in specs:
 body=rect(0,0,1280,840,'#f2f6fa')+text(28,35,sid+' — '+title,25,700)+text(28,61,'Vector screen definition · reference units = dp · illustrative state, not a running app',14)
 body+=rect(24,82,165,720,'#e8f0f7')+text(44,119,'Android MCP',19,700)
 body+=f'<g data-control-id="{sid}.C03" aria-label="Navigation'+(' locked during approval' if sid=='SCR09' else '')+'">'
 for i,label in enumerate(navigation):
  body+=rect(34,139+i*56,145,48,'#e8f0f7')+text(44,169+i*56,label,14,600 if label.lower() in title.lower() else 400)
 body+='</g>'+rect(202,82,732,720)+text(222,124,title,23,700)+lines(222,154,heading,78,14)
 body+=rect(218,187,700,44,'#edf6ef')+text(232,215,'Service state / selected client/session scope · owner commands stay separate',13,600)
 for i,(label,value,note) in enumerate(rows):
  y=246+i*102;body+=rect(218,y,700,88)+text(232,y+22,label,13,600)+text(232,y+47,value,17,600)+lines(232,y+69,note,87,12)
 for i,label in enumerate(actions):
  x=222+i*338;body+=f'<g data-control-id="{sid}.C0{i+1}">'+rect(x,674,322,48,'#234b70' if i==0 else '#eef4f8')+text(x+13,705,label,14,600,'#ffffff' if i==0 else '#20354a')+'</g>'
 if sid=='SCR07':body+=f'<g data-control-id="{sid}.C04">'+rect(727,574,179,48,'#eef4f8')+text(737,603,'Send input (disabled)',12)+'</g>'
 body+=text(222,757,'C01 primary · C02 cancel/secondary · C03 navigation (locked on SCR09)',12)
 body+=rect(950,82,306,720)+text(970,117,'Controls / states',20,700)+text(970,149,sid+'.C01 / C02 / C03',13)
 body+=lines(970,181,fr,34,13)
 for i,state in enumerate(states):
  y=245+i*110;body+=f'<g data-state-id="{sid}.S{i+1}">'+rect(968,y,270,100,'#fff6e8')+text(981,y+25,sid+f'.S{i+1}',13,700)+lines(981,y+49,state,32,12)+'</g>'
 body+=text(970,706,'S0: depicted example; 48dp targets',12)+text(970,739,'No direct SQL or self-approval',12,600)+text(970,767,'Narrow layout: same IDs, single pane',12)
 (D/(sid+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="840" viewBox="0 0 1280 840" role="img"><title>{esc(sid+" "+title)}</title><desc>{esc(fr+"; controls, state variants and actions")}</desc><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>')
# Each panel names the actor, actual control/state, transition and alternate.
stories=[
 ('STB01','Inspect an agent session and stop a callback',[
  ('SCR04','S0','C01','Owner','Inspect group summary','Selected client/session shown','Denied grant remains visible; no UI activation'),
  ('SCR05','S0','C01','Owner','Inspect subscription status','Agent registered via its own MCP session','No registration: SCR05.S1; never impersonate client'),
  ('SCR09','S0','C01','Owner','Approve exact admin cancellation','Nonce/revision-bound cancellation admitted','Dismiss/expiry: no cancellation command'),
  ('SCR05','S4','C01','Owner','Inspect closed delivery','Cleanup confirmed or explicitly unknown','Backend death: payload hidden; retain cleanup status')],
  'The agent owns MCP discovery/activation/registration/resource reads. The owner UI observes scoped metadata; administrative cancellation uses protected owner authority, not client impersonation.'),
 ('STB02','ARC connect, inspect, control and disconnect',[
  ('SCR07','S0','C01','Owner','Connect selected target','First envelope checked; no handshake','Wrong token/profile: no target data'),
  ('SCR07','S4','C01','Owner','Observe capabilities/frame','Verified target generation and DIP scale','Protected/unknown root: no action'),
  ('SCR07','S4','C04','Owner','Request admitted control','Exact target policy checked before dispatch','OK envelope plus failed CommandResult is still denied'),
  ('SCR07','S4','C02','Owner','Disconnect/cancel','Watch socket closes; pressed state released','Uncertain click is not blindly replayed')],
  'Existing ARC v1 bytes remain unchanged. Target operation controls are capability/policy gated; protected local owner surfaces are never an ARC root.'),
 ('STB03','Edit configuration and observe real startup',[
  ('SCR08','S0','C01','Owner','Save exact draft revision','Protected change opens SCR09','Cancel: no committed mutation'),
  ('SCR09','S0','C01','Owner','Authenticate exact command','Service commits CAS then publishes state','Dismiss/timeout: deny; conflict: reload SCR08'),
  ('SCR01','S0','C01','Owner','Observe listener readiness','Starting becomes Running only when ready','Start denied: navigate to SCR10'),
  ('SCR10','S0','C02','Owner','Return to dashboard','Show actual MCP/ADB/view states','No permitted start: remain action-required')],
  'SCR01 is the success surface; SCR10 is the failure branch. UI recreation never implies foreground-service shutdown.'),
 ('STB04','Pair, review grants and revoke a client',[
  ('SCR11','S0','C01','Owner','Request pairing challenge','Review exact identity/origin in SCR09','Cancel/wrong proof: no principal committed'),
  ('SCR09','S0','C01','Owner','Approve scoped pairing','One-time challenge follows pinned pairing contract','Expired nonce or changed origin: reject'),
  ('SCR02','S0','C01','Owner','Edit exact client grants','Draft rule/scope opens protected review','Denied scope stays denied until approved commit'),
  ('SCR02','S0','C02','Owner','Request credential revocation','Protected review then epoch invalidation','Changed revision: no silent overwrite')],
  'SCR02.S1 routes Pair locally to SCR11. Grant edits and revocation use the exact owner flow; display names or group interests grant nothing.'),
 ('STB05','Override catalog metadata and export logs',[
  ('SCR03','S0','C02','Owner','Draft local override','Immutable source evidence stays unchanged','Schema/identity failure: show reason'),
  ('SCR09','S0','C01','Owner','Approve pinned override','CAS commits local overlay and history','Stale base/revision: conflict, reload'),
  ('SCR06','S0','C02','Owner','Preview selected export scope','Fresh protected review before export','Denied auth: no export data'),
  ('SCR06','S0','C01','Owner','Inspect export audit record','Redacted selected scope or explicit error','Cancel/disk failure: no false success')],
  'Local overrides cannot manufacture imported availability or grants. Export is owner-scoped, redacted and separate from mandatory audit retention.')]
titles={v[0]:v[1] for v in specs};manifest=dict(schema_version='1.1.0',canonical_sources='ui-svg; docs/screens is a generated copy',reference_unit='dp',screens=[],storyboards=[],implemented_android_ui=False)
for sid,title,fr,heading,rows,actions,states in specs:
 manifest['screens'].append(dict(id=sid,title=title,requirements=fr,controls={sid+'.C01':actions[0],sid+'.C02':actions[1],sid+'.C03':'Navigation; locked for SCR09'},states={sid+'.S0':'Depicted normal/example state',**{sid+f'.S{i+1}':state for i,state in enumerate(states)}}))
 if sid=='SCR07':manifest['screens'][-1]['controls'][sid+'.C04']='Send typed input; disabled unless connected capability/policy permits'
index='# Kotlin Compose SVG screen definitions and storyboards\n\nCanonical editable sources are `ui-svg/`; Gradle copies them to `docs/screens/`. Edit source SVG and its manifest together. Reference units map to Compose dp; action/navigation bounds are48dp. Narrow windows use the same IDs in a single pane. These are screen definitions, not an implemented UI.\n\n'
for sid,title,fr,heading,rows,actions,states in specs:
 index+=f'## {sid} — {title}\n\n{fr}. {sid}.C01 = {actions[0]}; C02 = {actions[1]}; C03 = navigation. SCR09 is modal with navigation locked. SCR02.S1 offers Pair locally -> SCR11. States S1–S{len(states)} are explicit alternatives; S0 is the pictured example. SCR07.C04 is input, enabled only in the connected and authorized state.\n\n![{sid}](screens/{sid}.svg)\n\n'
deck='---\nmarp: true\ntheme: default\npaginate: true\nsize: 16:9\nstyle: |\n  section { font-size: 24px; color: #20354a; }\n  h1 { font-size: 34px; }\n---\n\n# Kotlin Compose UI storyboards\n\nOwner controls and explicit external-agent triggers. SVG screen/state/control IDs are authoritative.\n\nDesign only; no app or device operations.\n'
for sid,title,panels,boundary in stories:
 body=rect(0,0,1680,900,'#f2f6fa')+text(28,42,sid+' — '+title,25,700)+text(28,74,'Ordered screen panels · actual controls/states · success, error and cancel branches',16)
 steps=[]
 for i,(screen,state,control,actor,action,result,alternate) in enumerate(panels):
  x=28+i*414;body+=rect(x,110,392,615)+text(x+15,143,f'{i+1}. {actor} · {screen}.{state}',16,700)+lines(x+15,173,titles[screen],37,15)
  body+=rect(x+15,224,362,183,'#e8f0f7')+text(x+27,251,screen+' / '+titles[screen],12,600)+lines(x+27,281,'Selected client/session or exact target scope',43,12)
  body+=rect(x+27,324,338,60,'#234b70')+lines(x+39,347,screen+'.'+control+' — '+action,38,12,fill='#ffffff')
  body+=text(x+15,446,'Success / transition',14,700)+lines(x+15,473,result,40,14,20)+text(x+15,555,'Error / cancel branch',14,700)+lines(x+15,582,alternate,40,13,19)
  if i<3:body+=f'<path d="M {x+394} 342 h 17 l -6 -6 m 6 6 l -6 6" fill="none" stroke="#234b70" stroke-width="3"/>'
  steps.append(dict(screen_id=screen,state_id=screen+'.'+state,control_id=screen+'.'+control,actor=actor,action=action,result=result,alternate=alternate))
  deck+=f'\n---\n\n# {sid} · {i+1}. {action}\n\n![bg right:62% contain](../docs/screens/{screen}.svg)\n\n**{actor}** · `{screen}.{state}` · `{screen}.{control}`\n\n**Result:** {result}\n\n**Error/cancel:** {alternate}\n'
 body+=rect(28,752,1620,119,'#fff6e8')+text(45,783,'Authority / branch boundary',16,700)+lines(45,813,boundary,172,14,21)
 (D/(sid+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1680" height="900" viewBox="0 0 1680 900" role="img"><title>{esc(sid+" "+title)}</title><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>')
 manifest['storyboards'].append(dict(id=sid,title=title,steps=steps,boundary=boundary));index+=f'## {sid} — {title}\n\n{boundary}\n\n![{sid}](screens/{sid}.svg)\n\n'
index+='[Editable Marp deck](../storyboards/storyboard.md) · [Self-contained slideshow](storyboards.html)\n'
(P/'docs/ui-screens-and-storyboards.md').write_text(index);(P/'docs/ui-asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(P/'storyboards/storyboard.md').write_text(deck)
(R/'evidence/design018-ui-working-ledger.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),screens=len(specs),storyboards=len(stories),slides=1+sum(len(s[2]) for s in stories),corrections=['P4-12','P4-13','P4-14','P4-15','P4-16','P4-17','P4-18','P4-19','P4-20','P4-21','P4-22','P4-23','P4-26','P4-29'],frozen018_unchanged=True,validation_pending=True),indent=2)+'\n')
print(json.dumps(dict(screens=len(specs),storyboards=len(stories),slides=21)))
