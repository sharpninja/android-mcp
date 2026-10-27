"""Audit all used018 review stages without rewriting original failed receipts."""
from pathlib import Path
import hashlib,json,datetime,collections
R=Path(__file__).resolve().parent;D=R/'reviews/round-018'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
root=json.loads((D/'input-manifest.json').read_text());failures=[]
for row in root['files']:
    p=D/'input'/row['file']
    if sha(p)!=row['sha256']:failures.append('Frozen input mismatch '+row['file'])
stage_paths=[D/f'pass-{i:02d}' for i in (2,3,4,5)]+[D/f'core-section-{i:02d}' for i in range(1,8)]+[D/'recovered-synthesis']
rows=[]
for p in stage_paths:
    completion=json.loads((p/'completion.json').read_text());request=json.loads((p/'request.jsonl').read_text());events=[json.loads(x) for x in (p/'response.visible.jsonl').read_text().splitlines()]
    errors=[]
    if request['prompt_sha256']!=sha(p/'prompt.txt'):errors.append('request prompt hash')
    if completion['visible_response_sha256']!=sha(p/'response.visible.jsonl'):errors.append('visible response hash')
    if request['tools']!=[] or request['requested_effort']!='max' or request['requested_model']!='claude-fable-5-1':errors.append('model/effort/tools')
    if completion['exit_code']!=0:errors.append('nonzero process exit')
    if any(b.get('type') in ('thinking','redacted_thinking') for e in events if e.get('type')=='assistant' for b in e.get('message',{}).get('content',[])):errors.append('hidden block retained')
    terminal=[e for e in events if e.get('type')=='result']
    if not terminal or terminal[-1].get('is_error'):errors.append('missing/error terminal')
    complete=completion['report_complete']
    recovered=None
    if p.name=='core-section-06':
        recovered=json.loads((p/'review-report.recovered.json').read_text());complete=recovered.get('report_complete') is True
        audit=json.loads((p/'report-reconstruction.json').read_text())
        if not complete:errors.append('recovered section incomplete')
    if not complete:errors.append('incomplete report')
    failures.extend(p.name+': '+e for e in errors)
    rows.append(dict(stage=p.name,cli_exit_code=completion['exit_code'],original_report_complete=completion['report_complete'],used_report_complete=complete,reconstructed_encoding_only=recovered is not None,actual_models=completion['actual_models'],events=len(events),visible_sha256=sha(p/'response.visible.jsonl'),request_sha256=sha(p/'request.jsonl'),issues=errors))
report=json.loads((D/'recovered-synthesis/review-report.json').read_text())
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),root_manifest_sha256=sha(D/'input-manifest.json'),frozen_files_verified=len(root['files']),used_stages=rows,excluded_original_pass01='exit0 but aborted_streaming/max_tokens, report incomplete; no findings used',original_root_failure_preserved=True,final_synthesis_manifest_sha256=sha(D/'recovered-synthesis/input-manifest.json'),final_synthesis_complete=report['report_complete'],design_scores=report['scores'],failures=failures,scope='Design only; receipt integrity is not a new quality score')
out=R/'evidence/review-018-recovered-receipt-audit.json'
with out.open('x') as f:f.write(json.dumps(result,indent=2)+'\n')
(D/'completion.recovered.json').write_text(json.dumps(dict(completed_at=json.loads((D/'recovered-synthesis/completion.json').read_text())['completed_at'],report_complete=True,authoritative_synthesis='recovered-synthesis/review-report.json',scores=report['scores'],audit=str(out),audit_failures=failures,original_completion_preserved=True),indent=2)+'\n')
print(json.dumps(dict(frozen_files=len(root['files']),stages=len(rows),complete=report['report_complete'],failures=failures,audit=str(out))))
raise SystemExit(bool(failures))
