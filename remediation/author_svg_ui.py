"""Author editable vector screen specifications and storyboards; no app implementation."""
import pathlib,html,json
R=pathlib.Path(__file__).resolve().parent/'design-c4';D=R/'ui-svg';D.mkdir(parents=True,exist_ok=True)
esc=lambda t:html.escape(str(t),quote=True)
def text(x,y,value,size=16,weight=400,fill='#20354a'):
 return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{fill}">{esc(value)}</text>'
def box(x,y,w,h,fill='#ffffff',stroke='#cfdae5',r=12):return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}"/>'
def button(x,y,label,primary=False,width=170):return box(x,y,width,38,'#234b70' if primary else '#eef4f8')+text(x+14,y+25,label,14,600,'white' if primary else '#20354a')
specs=[
 ('SCR01','Dashboard','FR-KT-01 / FR-KT-03', 'Runtime status',[
  ('MCP service','Running - listener ready','Ready view 27 / capability epoch 8'),('ADB backend','Disconnected','MCP health stays Running'),('Catalog','Full inventory retained','Unknown and unavailable entries are searchable'),('Active work','2 subscriptions / 1 session','View or cancel through scoped controls')],['Start / Stop service','Open subscriptions'],['Starting: admission closed','Start denied: owner action required','Stopped: no listener, no Running badge']),
 ('SCR02','Clients and permissions','FR-KT-02 / canonical client grants','Client: Local agent',[
  ('Identity','Paired principal / credential epoch','Display name is not authority'),('binding.invoke','Ask','Exact binding and scope required'),('Location subscription','Denied','Activation cannot grant this permission'),('Call logging','Metadata only','Payload logging explicitly configured per client')],['Edit exact grants','Revoke credential'],['No clients: pair locally','Denied: explain exact scope','Conflict: reload changed revision']),
 ('SCR03','Full binding catalog','All bindings / FR-DEV-01','Search bindings, types, evidence',[
  ('SDK method','Available / active','OS authority and client grant checked separately'),('Binder interface','Unknown','Missing verified wire contract'),('Historical type','Unavailable on observed device','Full immutable source history retained'),('Local override','Revision 4 / source pin','Preview dependencies and CAS before saving')],['Inspect evidence','Edit local override'],['No matches: broaden query','Schema unknown: reason visible','Stale device view: refresh required']),
 ('SCR04','Groups for a job','FR-GRP-01 / 02 / 03','Job: observe location updates, then stop',[
  ('Location lifecycle','Rank 1 - summary only','Register + cleanup + event types: coherent unit'),('Context budget','8 KiB descriptors / estimate 1800 tokens','Strict bytes enforced; token estimate labeled'),('Applicability / permission','Available / Denied','Discovery does not grant authority'),('Session view','Portable generic-invoke mode','Dynamic aliases only for opted-in hosts')],['Describe group','Activate selected set'],['Empty: unassigned search available','Budget exceeded: no partial activation','CAS conflict: reload session view']),
 ('SCR05','Callback subscriptions','FR-CB-01 / 02 / 03 / 04','Subscription journal',[
  ('Registration','Active / pinned callback schema','Transport disconnect does not unregister'),('Delivery','Cursor 42 / 2 retained events','URI update hint, then bounded resource read'),('Retention','256 events or 1 MiB maximum','Overflow default closes delivery with reason'),('Cleanup','Verified unregister plan','Cancellation reports confirmed or unknown cleanup')],['Read bounded page','Cancel subscription'],['Empty: no registrations','Cursor expired: retention boundary','Orphaned / revoked: payload suppressed']),
 ('SCR06','Call logs','Per-client logging / FR-KT-02','Metadata-first audit and logs',[
  ('Filter','Client / result / binding / time','Bounded pages; other clients isolated'),('Audit record','Admission pins + outcome','Minimal mandatory audit is separate from optional logs'),('Payload','Not logged','Enable only by explicit client logging setting'),('Export / retention','Local owner command','Secret fields redacted; exact export scope preview')],['Inspect record','Export selected scope'],['Empty: no admitted calls','Denied: export needs owner auth','Disk quota: visible retention error']),
 ('SCR07','ARC session and control','FR-ARC-01 / 02 / 03 / 04','Controller: selected compatible target',[
  ('Connection','Disconnected - no live frame','arc-protobuf-v1 / wire 1.0; no handshake claim'),('Transport','Authenticated loopback plaintext','Not TLS; secure tunnel is separate'),('Target capabilities','Read tree/frame/control after probe','Denied command may use OK + succeeded=false'),('Input / frame','Root generation and DIP scale pinned','No multitouch claim; protected owner root excluded')],['Connect selected target','Disconnect / cancel'],['Wrong token: no target content','Unknown protocol: reject before forwarding','Protected surface: capture/input blocked']),
 ('SCR08','Settings and backends','FR-KT-03 / persistent service / ADB','Desired state and connection profiles',[
  ('Start on boot','Off','Does not override Android start restrictions'),('Service notification','Actual readiness + separate ADB warning','Notification permission denial shown truthfully'),('ADB profile','Owner-paired / target scope','No action performed by these design artifacts'),('ARC profile','Disabled until locally enabled','Token in private storage; plaintext profile explicit')],['Save with revision','Review pairing'],['Locked: wait for unlock','Ineligible start: requires owner action','Stale settings revision: reject save']),
 ('SCR09','Protected owner approval','FR-ARC-04 / canonical Ask approval','Exact local owner decision',[
  ('Requested operation','Grant scoped binding permission','Principal, binding and target shown exactly'),('Pinned plan','Contract hash / policy revision / expiry','Any relevant change invalidates approval'),('Input boundary','ARC + ADB input quiesced','No remote root, pixels or approval capability'),('Authentication','Fresh local owner authentication','Dismissal, timeout or missing UI denies execution')],['Authenticate and approve','Deny / dismiss'],['Expired: no execution','Plan changed: new review required','Remote input: rejected before effect']),
 ('SCR10','Action required','FR-KT-03 / no fabricated readiness','Service could not start',[
  ('MCP service','Error - listener not ready','Admission remains closed'),('Android reason','Foreground-service start not eligible','No automatic bypass or false Running state'),('ADB backend','Not connected','Separate backend status is retained'),('Recovery','Review prerequisites locally','Start only after a permitted owner action')],['Review prerequisites','Return to dashboard'],['No catalog: import validated release','Offline: explicit unknown capability facts','Cancel: desired Stopped state retained'])
]
navigation=['Dashboard','Clients','Catalog','Groups','Callbacks','Call logs','ARC control','Settings']
for sid,title,fr,heading,rows,actions,states in specs:
 body=box(0,0,1280,840,'#f2f6fa',r=0)+text(30,32,f'{sid} - {title}',25,700)+text(30,59,'Kotlin Compose screen specification - vector design, not a running application',14)
 body+=box(24,82,910,720)+box(24,82,165,720,'#e8f0f7')+text(45,120,'Android MCP',19,700)
 for i,n in enumerate(navigation):body+=text(44,166+i*48,n,15,600 if n.lower() in title.lower() else 400)
 body+=text(214,125,title,24,700)+text(214,156,heading,16)+box(210,182,694,46,'#edf6ef')+text(225,211,'Service-owned state | owner controls are separate from MCP grants',14,600)
 for i,(label,value,note) in enumerate(rows):
  y=247+i*102;body+=box(210,y,694,88)+text(226,y+24,label,13,600,'#547087')+text(226,y+49,value,18,600)+text(226,y+72,note,13)
 body+=button(214,678,actions[0],True,225)+button(457,678,actions[1],False,225)+text(214,757,'C01 primary action    C02 secondary action    C03 navigation rail',13)
 body+=box(957,82,299,720,'#ffffff')+text(975,117,'Definition and states',20,700)+text(975,148,'Control IDs: '+sid+'.C01/C02',13)+text(975,176,'Requirement mapping',14,600)
 # Wrap annotations rather than clipping at panel edges.
 import textwrap
 for j,line in enumerate(textwrap.wrap(fr,31)):body+=text(975,200+j*19,line,13)
 for i,state in enumerate(states):
  y=260+i*120;body+=box(973,y,267,99,'#fff6e8')+text(986,y+25,f'{sid}.S{i+1}',13,700)
  for j,line in enumerate(textwrap.wrap(state,31)):body+=text(986,y+49+j*18,line,13)
 body+=text(975,686,'Accessibility: labels + status text',12)+text(975,709,'Focus/contrast/touch targets to test',12)+text(975,746,'UI does not write SQL directly',13,600)+text(975,770,'No remote self-approval',13,600)
 svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="840" viewBox="0 0 1280 840" role="img"><title>{esc(sid+" "+title)}</title><desc>{esc(fr+"; control actions and important state variants")}</desc><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>'
 (D/(sid+'.svg')).write_text(svg)
storyboards=[
 ('STB01','Discover, activate and consume a callback', [('SCR04','1. Describe the job','Read bounded group summaries','Denied permission stays visible'),('SCR04','2. Select coherent group','CAS activate within byte budget','Conflict/budget error: nothing partial'),('SCR05','3. Register and consume','One final response, then journal pages','Expired cursor: show retention boundary'),('SCR05','4. Cancel explicitly','Stop delivery; verify unregister','Cleanup unknown remains visible')], 'If a grant is missing, stop and open local owner review (SCR09); activation never grants permission.'),
 ('STB02','ARC connect, inspect, control and disconnect', [('SCR07','1. Pick target profile','Check arc-protobuf-v1 and private token','Unknown protocol: no connect'),('SCR07','2. Authenticate and probe','Version 1.0 and matching request ID','Wrong token: no tree/frame disclosure'),('SCR07','3. Inspect ordinary root','Generation-pinned frame/DIP actions','Protected owner root: refuse input'),('SCR07','4. Disconnect','Close socket, release pressed keys','Never retry an uncertain click')], 'ARC wire remains unchanged. Denied control can be OK envelope + CommandResult.succeeded=false.'),
 ('STB03','Edit configuration and handle start failure', [('SCR08','1. Edit draft settings','Preview exact change and revision','Cancel: no committed mutation'),('SCR09','2. Authenticate locally','Review principal/scope/expiry','Dismiss/timeout: deny'),('SCR08','3. Commit with CAS','Service transaction then state update','Stale revision: reload, no overwrite'),('SCR10','4. Observe actual status','Running only when listener is ready','Start denied: action required, no bypass')], 'UI recreation/unbind does not stop the foreground service. AppBar and notification share actual service readiness.')
]
for sid,title,panels,branch in storyboards:
 body=box(0,0,1600,780,'#f2f6fa',r=0)+text(30,45,sid+' - '+title,28,700)+text(30,77,'Screen storyboard: ordered user actions, transitions and error/cancel branches',17)
 for i,(screen,step,action,error) in enumerate(panels):
  x=30+i*397;body+=box(x,115,365,485)+text(x+18,151,step,18,700)+text(x+18,181,screen,15,600)+box(x+18,206,329,170,'#e8f0f7')
  body+=box(x+30,220,305,24,'#ffffff')+text(x+41,237,screen+' / selected screen state',12)+box(x+30,260,305,46,'#ffffff')+box(x+30,318,180,35,'#234b70')+text(x+42,341,'User action',13,600,'#ffffff')
  for j,line in enumerate(textwrap.wrap(action,33)):body+=text(x+18,411+j*21,line,16)
  body+=text(x+18,476,'Alternate path',14,700,'#8b5c14')
  for j,line in enumerate(textwrap.wrap(error,33)):body+=text(x+18,504+j*21,line,15)
  if i<3:body+=f'<path d="M {x+368} 335 h 22 l -7 -6 m 7 6 l -7 6" fill="none" stroke="#234b70" stroke-width="3"/>'
 body+=box(30,633,1544,105,'#fff6e8')+text(48,663,'Boundary / branch',16,700)
 for j,line in enumerate(textwrap.wrap(branch,140)):body+=text(48,690+j*22,line,16)
 (D/(sid+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="780" viewBox="0 0 1600 780" role="img"><title>{esc(title)}</title><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>')
docs=R/'docs';docs.mkdir(exist_ok=True)
index='# Kotlin Compose screen definitions and storyboards\n\nEditable vector specifications; no actual Android UI implementation or device observations. Reference layout is an adaptive large-window Compose management surface; narrow-window navigation collapses to a drawer/single pane, with the same controls/state IDs. Minimum48dp touch targets, accessibility traversal, rotation and text scaling remain implementation acceptance gates.\n\n'
for sid,title,fr,heading,rows,actions,states in specs:index+=f'## {sid} — {title}\n\n{fr}. Controls C01/C02 are the primary/secondary actions, C03 is navigation. State S1/S2/S3 variants are defined in the right annotation panel.\n\n![{sid} {title}](screens/{sid}.svg)\n\n'
for sid,title,panels,branch in storyboards:index+=f'## {sid} — {title}\n\n{branch}\n\n![{sid} storyboard](screens/{sid}.svg)\n\n'
index+='[Editable Marp deck](../storyboards/storyboard.md) · [Self-contained HTML slideshow](storyboards.html)\n'
(docs/'ui-screens-and-storyboards.md').write_text(index)
deck='---\nmarp: true\ntheme: default\npaginate: true\nsize: 16:9\nstyle: |\n  section { font-size: 25px; color: #20354a; }\n  h1 { font-size: 36px; }\n---\n\n# Android MCP UI storyboards\n\nKotlin Compose screen/state specifications.\n\nDesign only — no application or device operations.\n\n---\n'
for sid,title,panels,branch in storyboards:
 for screen,step,action,error in panels:
  deck+=f'\n# {sid} · {step}\n\n![bg right:65% contain] (../docs/screens/{screen}.svg)\n\n{screen} · {action}\n\n**Alternate:** {error}\n\n---\n'
deck=deck.replace('] (','](').removesuffix('\n---\n');(R/'storyboards').mkdir(exist_ok=True);(R/'storyboards/storyboard.md').write_text(deck)
(docs/'ui-asset-manifest.json').write_text(json.dumps(dict(screens=[dict(id=x[0],title=x[1],requirements=x[2]) for x in specs],storyboards=[dict(id=x[0],title=x[1],screens=[p[0] for p in x[2]]) for x in storyboards],implemented_android_ui=False),indent=2)+'\n')
print(json.dumps(dict(screens=len(specs),storyboards=len(storyboards),slides=13,svg_source_of_truth=True)))
