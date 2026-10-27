"""Extend current canonical SVG/Marp specifications from the manifest; no app code."""
from pathlib import Path
import json,html,textwrap
R=Path(__file__).resolve().parent;P=R/'design-c4';D=P/'ui-svg';mp=P/'docs/ui-asset-manifest.json';m=json.loads(mp.read_text())
if any(x['id']=='STB06' for x in m['storyboards']):raise SystemExit('Already extended; inspect instead')
settings=next(x for x in m['screens'] if x['id']=='SCR08')
settings['states']['SCR08.S4']='Pairing failed/peer unverified: no Connected state and no new MCP grant'
esc=lambda x:html.escape(str(x),quote=True)
def text(x,y,t,size=14,weight=400,color='#20354a'):return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{color}">{esc(t)}</text>'
def rect(x,y,w,h,color='#ffffff'):return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{color}" stroke="#cfdae5"/>'
def wrap(x,y,t,width=40,size=13,step=19,color='#20354a'):return ''.join(text(x,y+i*step,v,size,color=color) for i,v in enumerate(textwrap.wrap(t,width)))
svg=D/'SCR08.svg';s=svg.read_text();state='<g data-state-id="SCR08.S4">'+rect(968,575,270,100,'#fff6e8')+text(981,600,'SCR08.S4',13,700)+wrap(981,624,settings['states']['SCR08.S4'],32,12,18)+'</g>'
s=s.replace('<text x="970" y="706"',state+'<text x="970" y="706"');svg.write_text(s)
steps=[
dict(screen_id='SCR08',state_id='SCR08.S0',control_id='SCR08.C02',actor='Owner',action='Review self-ADB pairing profile',result='Show exact device/user and discovered or entered pairing endpoint',alternate='Cancel: no connection or permission change'),
dict(screen_id='SCR09',state_id='SCR09.S0',control_id='SCR09.C01',actor='Owner',action='Authenticate exact pairing/connect command',result='Bounded native pairing uses the owner-provided Android pairing material',alternate='Expired code/wrong peer: fail; no settings bypass'),
dict(screen_id='SCR08',state_id='SCR08.S0',control_id='SCR08.C02',actor='Owner',action='Inspect verified paired endpoint status',result='Connected or Paired offline is distinct from MCP listener health',alternate='SCR08.S4: unverified peer never becomes Connected'),
dict(screen_id='SCR08',state_id='SCR08.S0',control_id='SCR08.C03',actor='Owner',action='Navigate to dashboard',result='SCR01 shows independent MCP/ADB/device-view health; grants unchanged',alternate='Backend unavailable stays explicit; never auto-pair or enable debugging')]
m['storyboards'].append(dict(id='STB06',title='Pair self-ADB and inspect backend health',steps=steps,boundary='The owner enables wireless debugging outside the bridge if desired. Pairing/connect is explicit and protected; it does not grant any MCP client capability. These are design panels, not executed phone actions.'))
titles={x['id']:x['title'] for x in m['screens']}
index='# Kotlin Compose SVG screen definitions and storyboards\n\nCanonical source is ui-svg; Gradle copies identical bytes to docs/screens. Reference units are dp with48dp action/navigation targets. These are editable design specifications, not a running app. The shared SCRxx.STATUS AppBar chip and Android notification use the same HealthSnapshot revision.\n\n'
for x in m['screens']:
    index+='## '+x['id']+' — '+x['title']+'\n\n'+x['requirements']+'.\n\n'
    index+=' | '.join('`'+key+'` '+value for key,value in x['controls'].items())+'\n\n'
    index+='Alternatives: '+'; '.join('`'+key+'`: '+value for key,value in x['states'].items())+'.\n\n'
    if x['id']=='SCR09':index+='Protected modal: navigation locked; fresh owner proof binds the exact command and revisions.\n\n'
    if x['id']=='SCR02':index+='The empty-client Pair action routes locally to SCR11; a display label is not a principal.\n\n'
    index+='!['+x['id']+'](screens/'+x['id']+'.svg)\n\n'
deck='---\nmarp: true\ntheme: default\npaginate: true\nsize: 16:9\nstyle: |\n  section { font-size:24px; color:#20354a; }\n  h1 { font-size:34px; }\n---\n\n# Kotlin Compose UI storyboards\n\nOwner controls and explicit external-agent triggers. Screen/state/control IDs are authoritative. Design only; no phone operations.\n'
for story in m['storyboards']:
    sid=story['id'];body=rect(0,0,1680,900,'#f2f6fa')+text(28,42,sid+' — '+story['title'],25,700)+text(28,74,'Ordered screen panels · actual controls/states · success, error and cancel branches',16)
    for i,step in enumerate(story['steps']):
        x=28+i*414;screen=step['screen_id'];body+=rect(x,110,392,615)+text(x+15,143,f'{i+1}. '+step['actor']+' · '+step['state_id'],16,700)+wrap(x+15,173,titles[screen],37,15)
        body+=rect(x+15,224,362,183,'#e8f0f7')+text(x+27,251,screen+' / '+titles[screen],12,600)+wrap(x+27,281,'Selected client/session or exact target scope',43,12)
        body+=rect(x+27,324,338,60,'#234b70')+wrap(x+39,347,step['control_id']+' — '+step['action'],38,12,18,'#ffffff')
        body+=text(x+15,446,'Success / transition',14,700)+wrap(x+15,473,step['result'],40,14,20)+text(x+15,555,'Error / cancel branch',14,700)+wrap(x+15,582,step['alternate'],40,13,19)
        if i<3:body+=f'<path d="M {x+394} 342 h17 l-6 -6 m6 6 l-6 6" fill="none" stroke="#234b70" stroke-width="3"/>'
        deck+='\n---\n\n# '+sid+' · '+str(i+1)+'. '+step['action']+'\n\n![bg right:62% contain](../docs/screens/'+screen+'.svg)\n\n**'+step['actor']+'** · `'+step['state_id']+'` · `'+step['control_id']+'`\n\n**Result:** '+step['result']+'\n\n**Error/cancel:** '+step['alternate']+'\n'
    body+=rect(28,752,1620,119,'#fff6e8')+text(45,783,'Authority / branch boundary',16,700)+wrap(45,813,story['boundary'],172,14,21)
    (D/(sid+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1680" height="900" viewBox="0 0 1680 900" role="img"><title>{esc(sid+" "+story["title"])}</title><g font-family="DejaVu Sans, sans-serif">{body}</g></svg>')
    index+='## '+sid+' — '+story['title']+'\n\n'+story['boundary']+'\n\n!['+sid+'](screens/'+sid+'.svg)\n\n'
index+='[Editable Marp deck](../storyboards/storyboard.md) · [Self-contained slideshow](storyboards.html)\n'
(P/'docs/ui-screens-and-storyboards.md').write_text(index);(P/'storyboards/storyboard.md').write_text(deck);mp.write_text(json.dumps(m,indent=2)+'\n')
p=P/'docs/Android MCP Bridge Design.md';s=p.read_text();a=s.index('| Screen | Primary content and actions |');b=s.index('\n\n',a)
table='| Screen ID | Definition / primary actions |\n| --- | --- |\n'
for x in m['screens']:table+='| ['+x['id']+'](screens/'+x['id']+'.svg) '+x['title']+' | '+'; '.join(x['controls'].values())+' |\n'
table+='\n| Storyboard ID | Ordered owner flow |\n| --- | --- |\n'
for x in m['storyboards']:table+='| ['+x['id']+'](screens/'+x['id']+'.svg) | '+x['title']+' |\n'
s=s[:a]+table.rstrip()+s[b:]
s=s.replace('10 screen','11 screen').replace('five ordered','six ordered').replace('5 storyboards','6 storyboards').replace('21-slide','25-slide')
s+='\n\nThe screen and storyboard tables above are generated from docs/ui-asset-manifest.json. STB06 covers explicit protected self-ADB pairing/connect and independent backend health; it never enables Android debugging or grants agent permissions automatically.\n'
p.write_text(s)
p=P/'README.md';s=p.read_text().replace('34 unique docs SVGs:13 C4 views,5 class views,11 screens and5 storyboards','35 unique docs SVGs:13 C4 views,5 class views,11 screens and6 storyboards');p.write_text(s)
print(json.dumps(dict(screens=len(m['screens']),storyboards=len(m['storyboards']),slides=1+sum(len(x['steps']) for x in m['storyboards']))))
