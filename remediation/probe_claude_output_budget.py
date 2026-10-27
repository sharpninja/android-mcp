"""Task-local Claude transport/budget diagnostic, never a review or acceptance score."""
import pathlib,json,hashlib,datetime,subprocess
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/output-budget-probe-001'
if D.exists():raise SystemExit('Probe exists; inspect rather than duplicate')
D.mkdir();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
prompt='Transport configuration diagnostic only, no project assessment. Return exactly JSON {"diagnostic":true,"report_complete":true}. Do not assign any quality score.'
(D/'prompt.txt').write_text(prompt);(D/'input-manifest.json').write_text(json.dumps(dict(scope='No project artifacts; output-budget diagnostic only',at=now()),indent=2))
source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
tail=tail.replace('p=subprocess.Popen(args,cwd=D,stdin=','p=subprocess.Popen(args,env=dict(__import__("os").environ,CLAUDE_CODE_MAX_OUTPUT_TOKENS="128000"),cwd=D,stdin=')
tail=tail.replace("tools=[],prompt_sha256",'tools=[],environment_overrides={"CLAUDE_CODE_MAX_OUTPUT_TOKENS":"128000"},prompt_sha256')
number='output-budget-probe-001';exec(compile(tail,str(D/'capture.py'),'exec'))
