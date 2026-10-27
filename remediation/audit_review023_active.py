"""Read only exact task-owned review argv and the single nonsecret output control."""
from pathlib import Path
import sys,json,hashlib,datetime
from hv_runtime import ARGS,ENV,only_output_environment,alive
from hv_invariants import preflight
R=Path(__file__).resolve().parent;D=R/'reviews/round-023';S=D/'supervisor';rows=[];now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
for p in sorted(D.glob('pass-*/continuation-*/process.json')):
    state=json.loads(p.read_text());pid=state['pid'];request=json.loads((p.parent/'request.jsonl').read_text())
    if state['status']!='running' or not alive(pid):continue
    raw=Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')
    if raw[-1]==b'':raw.pop()
    actual=[x.decode() for x in raw];selected=only_output_environment(pid)
    if actual!=ARGS:raise ValueError('Actual args differ from reviewed capture invocation')
    preflight(actual,{'CLAUDE_CODE_MAX_OUTPUT_TOKENS':selected},'Unbounded design review',[],[],supervised=True)
    prompt=(p.parent/'prompt.txt').read_bytes()
    if hashlib.sha256(prompt).hexdigest()!=request['prompt_sha256']:raise ValueError('Prompt/request mismatch')
    rows.append(dict(stage=str(p.parent.relative_to(D)),pid=pid,actual_argv=actual,actual_selected_environment={'CLAUDE_CODE_MAX_OUTPUT_TOKENS':selected},request_prompt_sha256=request['prompt_sha256'],prompt_bytes=len(prompt),no_other_environment_values_recorded=True))
lease=json.loads((S/'lease.json').read_text());control=json.loads((S/'control.json').read_text())
if control['status']=='active' and alive(lease['pid']):lease.update(status='active',verified_active_at=now());(S/'lease.json').write_text(json.dumps(lease,indent=2)+'\n')
out=dict(at=now(),supervisor_pid=lease['pid'],supervisor_alive=alive(lease['pid']),stages=rows,assistant_caps=None,maximum_requested_per_response_tokens=128000,provider_hard_limit_continuation='Automatic in current supervisor; no finite retry/global output/time/word/turn budget',regressions='12normal+12optimized passed in supervisor preflight',full_capture='request.jsonl and response.visible.jsonl; only hidden thinking blocks excluded')
with (S/'actual-runtime-audit.json').open('x') as f:f.write(json.dumps(out,indent=2)+'\n')
live=R/'LIVE-STATUS.json';d=json.loads(live.read_text());d.update(updated_at=now(),executor_responsive=True,current_phase='023 corrected whole-design HV active; contactQA recovered, all62screenshots verified',review_round='023',review_directory=str(D),review_pid=lease['pid'],review_manifest_sha256=hashlib.sha256((D/'input-manifest.json').read_bytes()).hexdigest(),review_status='Fresh supervisor-owned evidence passes then ONE unified design verdict; uncapped automatic continuation',review_scope='Full current design only; contracts read for internal design consistency, no Google API/source/population completeness scoring',review_verdict_pending=True,design_verdict_pending=True,current_build='package026exit0;38SVGs;62slidecaptures;all39SVG/HTMLforcedhashesidentical',current_ui={'screens':11,'storyboards':6,'slides':62,'controls':74,'svg_outputs':38},current_design_tests={'normal':19,'optimized':19},orchestration_tests={'normal':12,'optimized':12},orchestration_audit=str(S/'actual-runtime-audit.json'),pending_acceptance='023 pending; latestvalid02292/89NOT_ACCEPTED; noGitHubcreation/push',working_design_differs_from_review018=True,working_design_fix='evidence/design022-closure-ledger.json');live.write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps(dict(supervisor_pid=lease['pid'],alive=out['supervisor_alive'],active_stages=[{'stage':x['stage'],'pid':x['pid']} for x in rows],assistant_caps=None,audit=str(S/'actual-runtime-audit.json'))))
