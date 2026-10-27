"""Resume the prepared synthesis after a local prelaunch hash-helper failure."""
import pathlib,json,hashlib,datetime,subprocess
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-018/recovered-synthesis'
if (D/'process.json').exists() or (D/'request.jsonl').exists():raise SystemExit('Launch outcome must be inspected before retry')
prompt=(D/'prompt.txt').read_text();sha=lambda b:hashlib.sha256(b).hexdigest();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
if sha((D/'input-manifest.json').read_bytes())!='39b8a817c869f2d1a643c31163c83ec00a8a01f42b3a58ee9978312c4b65cef1':raise ValueError('Prepared synthesis changed')
(D/'prelaunch-recovery.json').write_text(json.dumps(dict(at=now(),initial_failure='Local SHA helper expected Path but receipt capture passed bytes; failed before request/process creation',initial_api_request_sent=False,fix='Use byte-oriented SHA helper required by established capture',prepared_prompt_unmodified=True,section06='Encoding-only reconstruction already complete; no section re-reviewed'),indent=2)+'\n')
source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):];number='018-recovered-synthesis'
exec(compile(tail,str(D/'capture.py'),'exec'))
