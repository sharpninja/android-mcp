"""Exhaustive transport recovery after actual021 provider prompt_too_long.

No time, words, size, turns or aggregate output limit is imposed. All distinct
artifacts are assigned to complete evidence passes before one unified verdict.
"""
from pathlib import Path
import json,hashlib,datetime,subprocess,os,sys,concurrent.futures
R=Path(__file__).resolve().parent;OLD=R/'reviews/round-021';D=R/'reviews/round-022';now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
if D.exists():raise SystemExit('022 exists: inspect actual execution, never duplicate')
c=json.loads((OLD/'completion.json').read_text())
if c.get('result_metadata',{}).get('terminal_reason')!='prompt_too_long' or c['exit_code']!=1:raise ValueError('Unexpected021 outcome')
D.mkdir();S=D/'input';S.mkdir();manifest=json.loads((OLD/'input-manifest.json').read_text())
for row in manifest['files']:
    p=OLD/'input'/row['file'];raw=p.read_bytes()
    if sha(raw)!=row['sha256']:raise ValueError('Frozen021 mismatch')
    q=S/row['file'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);q.chmod(0o444)
aliases={x['file'] for x in manifest['byte_identical_aliases']};binaries={x['file'] for x in manifest['binary_dependencies']}
groups={1:[],2:[],3:[]}
for row in manifest['files']:
    rel=row['file']
    if rel in aliases or rel in binaries:continue
    if rel.endswith('.svg'):
        stage=2 if '/docs/diagrams/' in rel else 3
    elif rel.endswith('.html') or '/storyboards/' in rel or rel.endswith(('ui-asset-manifest.json','ui-screens-and-storyboards.md')):stage=3
    else:stage=1
    groups[stage].append(row)
all_assigned=[x['file'] for group in groups.values() for x in group]
expected={x['file'] for x in manifest['files']}-aliases-binaries
if len(all_assigned)!=len(set(all_assigned)) or set(all_assigned)!=expected:raise ValueError('Coverage omission or duplicate assignment')
root=dict(created_at=now(),scope=manifest['scope'],parent_failed021_manifest_sha256=sha((OLD/'input-manifest.json').read_bytes()),same_exact_candidate=True,files=manifest['files'],byte_identical_aliases=manifest['byte_identical_aliases'],binary_dependencies=manifest['binary_dependencies'],passes={str(k):v for k,v in groups.items()},coverage_complete=True,transport_reason='Actual021prompt_too_long before inference; all distinct files now assigned by coherent artifact families',controls=manifest['controls'])
(D/'input-manifest.json').write_text(json.dumps(root,indent=2)+'\n')
for p in sorted(S.rglob('*'),reverse=True):
    if p.is_dir():p.chmod(0o555)
S.chmod(0o555)
source=(R/'review_design_unbounded.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
# Replace only the completion marker check; all model/effort/output settings and
# uncapped streaming capture are identical to the user-authorized unbounded runner.
tail=tail.replace("'END_DESIGN_REVIEW_'+number","'END_DESIGN_STAGE_'+number")
base=(OLD/'prompt.txt').read_text().split('\nCOMPLETE FROZEN MANIFEST\n',1)[0]
def artifact(row):
    p=S/row['file'];return '\nBEGIN FULL ARTIFACT '+json.dumps(row)+'\n'+p.read_text()+'\nEND ARTIFACT '+row['file']+'\n'
def context(rel):
    return artifact(next(x for x in manifest['files'] if x['file']==rel))
def run(stage,prompt):
    target=D/stage;target.mkdir();(target/'prompt.txt').write_text(prompt);(target/'input-manifest.json').write_text(json.dumps(dict(parent_manifest_sha256=sha((D/'input-manifest.json').read_bytes()),stage=stage),indent=2)+'\n')
    code='from pathlib import Path\nimport os,sys,json,hashlib,datetime,subprocess\nR=Path('+repr(str(R))+');D=Path('+repr(str(target))+');number='+repr(stage)+'\nnow=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()\nsha=lambda b:hashlib.sha256(b).hexdigest()\nprompt=(D/"prompt.txt").read_text()\nmanifest={"controls":'+repr(manifest['controls'])+'}\nrows=[];texts=[]\n'+tail
    (target/'capture.py').write_text(code)
    result=subprocess.run([sys.executable,str(target/'capture.py')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    (target/'capture-output.txt').write_text(result.stdout)
    complete=json.loads((target/'completion.json').read_text())
    print(json.dumps(dict(stage=stage,completed_at=complete['completed_at'],exit_code=complete['exit_code'],report_complete=complete['report_complete'],actual_models=complete['actual_models'])),flush=True)
    if result.returncode or not complete['report_complete']:raise RuntimeError('Stage incomplete; preserve and recover only '+stage)
    return (target/'review-report.txt').read_text()
def evidence(item):
    n,rows=item;stage=f'pass-{n:02d}'
    task='''
TRANSPORT EVIDENCE PASS, NOT AN INDEPENDENT ACCEPTANCE SCORE.
The original whole request hit actual provider prompt_too_long. Three complete
artifact-family passes cover all distinct input files, followed by ONE unified
current-design acceptance synthesis. No assistant time/words/size/turn/output cap.
Inspect every assigned FULL artifact, preserve all material findings, exact evidence
paths/lines and minimal fixes, verified resolutions and cross-artifact questions.
Do not score yet or falsely call something globally absent because another pass
owns its definition. Mark conditional questions for the final synthesis to reconcile.
Do not stop at an output limit; continue until this pass is complete. No Google
surface/extractor/data review, tools, network or hidden reasoning. End with
END_DESIGN_STAGE_'''+stage+''' only after all assigned evidence is assessed.
'''
    assigned='\nASSIGNED COMPLETE FILES\n'+json.dumps(rows)+'\nGLOBAL COVERAGE INDEX\n'+json.dumps({str(k):[x['file'] for x in v] for k,v in groups.items()})+'\nALIASES/BINARY PINNED VALIDATION\n'+json.dumps(dict(aliases=manifest['byte_identical_aliases'],binaries=manifest['binary_dependencies']))
    prompt=base+task+assigned+''.join(artifact(x) for x in rows)
    if n in (2,3):
        for rel in ('design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md'):
            prompt+='\nSHARED EXACT MODEL/CLASS CONTEXT\n'+context(rel)
    print(json.dumps(dict(stage=stage,preparing_at=now(),files=len(rows),prompt_bytes=len(prompt.encode()),assistant_caps=None)),flush=True)
    return n,run(stage,prompt)
(D/'process.json').write_text(json.dumps(dict(pid=os.getpid(),status='running',started_at=now(),scope=root['scope'],assistant_caps=None),indent=2)+'\n')
print(json.dumps(dict(launched_at=now(),coordinator_pid=os.getpid(),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),passes={str(k):dict(files=len(v),bytes=sum(x['size_bytes'] for x in v)) for k,v in groups.items()},assistant_caps=None,requested_output_tokens=128000)),flush=True)
try:
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:reports=dict(pool.map(evidence,groups.items()))
    (D/'pass-reports.json').write_text(json.dumps(reports,indent=2)+'\n')
    prompt=base+'''
FINAL SINGLE UNIFIED FULL CURRENT DESIGN ASSESSMENT.
All three COMPLETE artifact-family reports follow; their assignments cover EVERY
distinct file in the frozen manifest, with byte-identical aliases accounted for.
Reconcile every finding, including cross-pass questions, against the full current
main design and full model/classes/contracts below. Resolve duplicates/contradictions
without dropping material defects. All current requirements remain in scope; Google
API surface/data and unfinished implementation are excluded from design scoring.
No assistant time/word/size/turn/aggregate output cap; provider limits require
continuation rather than truncated acceptance. Give one independent whole-design
accuracy/completeness and ACCEPTED/NOT_ACCEPTED at98/98, not an average of pass/old
scores. Include complete prior020/root/grouped-low disposition, every remaining/new
finding with exact evidence/minimal fix, requirements coverage and implementation
gates separately. Do not claim to have executed code or viewed pixels; actual local
build/browser/visual receipts and raw SVG sources are evidence. Start with
DESIGN ACCURACY: N/100 and DESIGN COMPLETENESS: N/100. End with
END_DESIGN_STAGE_synthesis only after the entire unified review is complete.
'''+ '\nALL COMPLETE PASS REPORTS\n'+json.dumps(reports,indent=2)
    for rel in ('design-c4/docs/Android MCP Bridge Design.md','design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md','catalog/SOURCE-METADATA-CONTRACT.md','reviews/round-020/review-report.txt'):
        prompt+=context(rel)
    report=run('synthesis',prompt)
    (D/'review-report.txt').write_text(report)
    (D/'completion.json').write_text(json.dumps(dict(completed_at=now(),exit_code=0,report_complete=True,scope=root['scope'],source='synthesis/review-report.txt',assistant_caps=None),indent=2)+'\n')
except Exception as error:
    (D/'completion.json').write_text(json.dumps(dict(completed_at=now(),exit_code=1,report_complete=False,error=str(error),recovery_obligation='Do not discard completed stages; recover missing/incomplete artifacts under the same immutable snapshot and proceed to unified synthesis',assistant_caps=None),indent=2)+'\n');raise
finally:
    p=D/'process.json';state=json.loads(p.read_text());state.update(status='finished',completed_at=now());p.write_text(json.dumps(state,indent=2)+'\n')
