"""Verify immutable design bytes and complete visible per-pass receipt chains."""
import pathlib,json,hashlib,sys,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews'/('round-'+sys.argv[1])
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
root=json.loads((D/'input-manifest.json').read_text());failures=[];stages=[]
for row in root['files']:
 p=D/'input'/row['file']
 if not p.is_file() or p.is_symlink() or sha(p)!=row['sha256']:failures.append('Frozen file mismatch '+row['file'])
for stage in sorted(list(D.glob('pass-*'))+[D/'synthesis']):
 if not stage.exists():continue
 if not (stage/'completion.json').exists():stages.append(dict(stage=stage.name,status='pending'));continue
 completion=json.loads((stage/'completion.json').read_text());request=json.loads((stage/'request.jsonl').read_text());events=[json.loads(line) for line in (stage/'response.visible.jsonl').read_text().splitlines()]
 issues=[]
 if request['prompt_sha256']!=sha(stage/'prompt.txt'):issues.append('Prompt hash mismatch')
 if request['manifest_sha256']!=sha(stage/'input-manifest.json'):issues.append('Stage manifest hash mismatch')
 parent=json.loads((stage/'input-manifest.json').read_text())
 if parent['parent_manifest_sha256']!=sha(D/'input-manifest.json'):issues.append('Parent manifest mismatch')
 if completion['visible_response_sha256']!=sha(stage/'response.visible.jsonl'):issues.append('Visible response hash mismatch')
 if completion['requested_effort']!='max' or request['requested_model']!='claude-fable-5-1':issues.append('Wrong model/effort request')
 if request['tools']!=[]:issues.append('Unexpected reviewer tools')
 if not any(e.get('type')=='system' and e.get('subtype')=='init' and e.get('model')=='claude-fable-5-1' and e.get('tools')==[] for e in events):issues.append('Model/tool init not verified')
 hidden=[b.get('type') for e in events if e.get('type')=='assistant' for b in e.get('message',{}).get('content',[]) if b.get('type') in ('thinking','redacted_thinking')]
 if hidden:issues.append('Hidden reasoning in delivered visible receipt')
 terminal=[e for e in events if e.get('type')=='result']
 if not terminal:issues.append('Terminal result missing')
 stages.append(dict(stage=stage.name,status='completed',exit_code=completion['exit_code'],report_complete=completion['report_complete'],actual_models=completion['actual_models'],visible_events=len(events),safety_stops=completion.get('result_metadata',{}).get('safety_stops'),issues=issues))
 failures += [stage.name+': '+issue for issue in issues]
complete=(D/'completion.json').exists() and json.loads((D/'completion.json').read_text()).get('report_complete') is True
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),review=str(D),manifest_sha256=sha(D/'input-manifest.json'),frozen_files=len(root['files']),stages=stages,failures=failures,whole_review_complete=complete,scope='Receipt integrity only; no independent quality score')
out=R/'evidence'/('review-'+sys.argv[1]+'-receipt-audit-'+sys.argv[2]+'.json')
with out.open('x') as f:f.write(json.dumps(report,indent=2)+'\n')
print(json.dumps(dict(whole_review_complete=complete,frozen_files=len(root['files']),stages=len(stages),failures=failures,receipt=str(out))))
raise SystemExit(1 if failures else 0)
