"""Launch preflighted020 after lossless JSON whitespace compaction only."""
from pathlib import Path
import json,hashlib,datetime,subprocess
R=Path(__file__).resolve().parent;D=R/'reviews/round-020'
if (D/'process.json').exists() or (D/'request.jsonl').exists():raise SystemExit('Inspect uncertain existing launch')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
prompt=(D/'prompt.txt').read_text();old=prompt.encode();raw=(D/'exact-main-delta.json').read_text();compact=json.dumps(json.loads(raw),ensure_ascii=False,separators=(',',':'))
if raw not in prompt:raise ValueError('Prepared delta not found')
prompt=prompt.replace(raw,compact)
if len(prompt.encode())>260000:raise SystemExit('Still exceeds recovery budget')
(D/'prompt.txt').write_text(prompt)
(D/'prelaunch-recovery.json').write_text(json.dumps(dict(at=now(),initial_preflight_bytes=len(old),initial_guard_bytes=260000,request_sent=False,recovery='Lossless JSON whitespace compaction, all exact replacement strings unchanged',initial_prompt_sha256=sha(old),launched_prompt_sha256=sha(prompt.encode()),launched_prompt_bytes=len(prompt.encode())),indent=2)+'\n')
for p in sorted((D/'input').rglob('*'),reverse=True):
    if p.is_dir():p.chmod(0o555)
(D/'input').chmod(0o555)
number='020-incremental-design-acceptance';source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
print(json.dumps(dict(prepared_at=now(),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),prompt_bytes=len(prompt.encode()),all_current_candidate_bytes_identical_to019=True,tools=[])),flush=True)
exec(compile(tail,str(D/'capture.py'),'exec'))
