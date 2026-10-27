"""Select known nonsecret review control values, never collect auth environment."""
from pathlib import Path
import json,datetime,sys
R=Path(__file__).resolve().parent;D=R/'reviews/round-023';allowed={'CLAUDE_CODE_MAX_OUTPUT_TOKENS','CLAUDE_CODE_MAX_TURNS','MAX_THINKING_TOKENS','ANTHROPIC_MAX_TOKENS','HV_MAX_TURNS','HV_MAX_SECONDS','HV_MAX_PROMPT_BYTES','HV_MAX_REPORT_BYTES','HV_MAX_TOKENS','HV_RETRY_LIMIT','HV_TIMEOUT','HV_DEADLINE','HV_BUDGET'}
def selected(pid):
    found={}
    with open(f'/proc/{pid}/environ','rb') as f:
        while True:
            name=bytearray()
            while True:
                c=f.read(1)
                if not c:return found
                if c in (b'=',b'\0'):break
                name.extend(c)
            keep=name.decode(errors='replace') in allowed;value=bytearray()
            if c==b'=':
                while True:
                    c=f.read(1)
                    if not c or c==b'\0':break
                    if keep:value.extend(c)
            if keep:found[name.decode()]=value.decode()
            if not c:return found
rows=[]
for p in sorted(D.glob('pass-*/continuation-*/process.json')):
    info=json.loads(p.read_text())
    if info['status']!='running':continue
    try:controls=selected(info['pid'])
    except FileNotFoundError:continue
    rows.append(dict(stage=str(p.parent.relative_to(D)),pid=info['pid'],selected_nonsecret_review_controls=controls))
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),allowed_keys=sorted(allowed),active=rows,other_environment_names_values_not_recorded=True,forbidden_present=[dict(stage=x['stage'],key=k,value=v) for x in rows for k,v in x['selected_nonsecret_review_controls'].items() if k!='CLAUDE_CODE_MAX_OUTPUT_TOKENS'])
out=D/'supervisor/selected-control-environment-audit.json'
with out.open('x') as f:f.write(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt))
