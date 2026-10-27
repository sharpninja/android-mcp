"""Single design verdict from byte-complete018 evidence; no rereview of Google data."""
import pathlib,json,hashlib,datetime,subprocess
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-018';S=D/'recovered-synthesis'
if S.exists():raise SystemExit('Synthesis exists; inspect before retry')
reports={};provenance=[];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for n in range(1,8):
 p=D/f'core-section-{n:02d}';reconstructed=n==6
 report=p/('review-report.recovered.json' if reconstructed else 'review-report.json')
 completion=json.loads((p/'completion.json').read_text())
 if reconstructed:
  audit=json.loads((p/'report-reconstruction.json').read_text())
  if not audit['model_report_complete'] or audit['recovered_sha256']!=sha(report):raise ValueError('Unverified encoding recovery')
 elif not completion['report_complete']:raise ValueError('Core report incomplete '+str(n))
 value=json.loads(report.read_text())
 if value.get('report_complete') is not True:raise ValueError('Missing report completeness')
 reports['core-section-'+str(n)]=value;provenance.append(dict(stage=p.name,file=str(report.relative_to(D)),sha256=sha(report),encoding_recovery=reconstructed))
for n in (2,3,4,5):
 p=D/f'pass-{n:02d}';c=json.loads((p/'completion.json').read_text())
 if not c['report_complete']:raise ValueError('Artifact pass incomplete')
 file=p/'review-report.json';reports['artifact-pass-'+str(n)]=json.loads(file.read_text());provenance.append(dict(stage=p.name,file=str(file.relative_to(D)),sha256=sha(file)))
manifest=json.loads((D/'input-manifest.json').read_text())
for row in manifest['files']:
 if sha(D/'input'/row['file'])!=row['sha256']:raise ValueError('Frozen input changed')
S.mkdir();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
stage_manifest=dict(created_at=now(),parent_manifest_sha256=sha(D/'input-manifest.json'),recovery_manifest_sha256=sha(D/'main-recovery-manifest.json'),all_reports=provenance,scope='One DESIGN ONLY synthesis; excludes Google API surface/extractor/data completeness; all frozen files assigned to completed evidence checks',original_failed_core_pass='pass01 aborted_streaming/max_tokens, no findings used',frozen_baseline='Delivered design018 baseline; later working fixes intentionally not assessed here')
(S/'input-manifest.json').write_text(json.dumps(stage_manifest,indent=2)+'\n')
instruction=(D/'requirements.txt').read_text()
prompt=instruction+'''\nFINAL SINGLE HOLISTIC DESIGN SYNTHESIS\nEvery frozen artifact was assigned once to a completed evidence check; the original failed core pass contributes no findings. Its exact source bytes were covered by seven smaller sections. Core06 needed only raw-newline escaping to parse its complete emitted JSON; the original and encoding audit remain preserved. All evidence workpapers follow in full, plus exact cross-boundary source excerpts for reconciliation. This is one design review pipeline, not averaged scores or independent area acceptances.\n\nReconcile every material finding and cross-question; distinguish a requirement missing globally from a detail omitted in an individual diagram or sliced paragraph. Several workpapers did not see later normative mechanisms. Do not promote their conditional absence claims without reconciling the other workpapers/excerpts. Selected class-member views need not list every inherited method, but actual contradictory responsibilities, absent required paths and unimplemented design mechanisms remain defects. Distinguish design accuracy/completeness from app/data implementation and independent execution gates. No Google inventory or extractor/data completeness scoring. Retain all material requirements; avoid low-value wording nits as readiness blockers unless they change behavior or understanding. No predetermined target score, no averaging, no invented evidence.\n\nReturn a concise, complete JSON verdict promptly: {review_scope,requirements_baseline,verdict,scores:{design:{accuracy_0_100,completeness_0_100,rationale}},findings:[{id,severity,area,artifact,evidence,issue,exact_minimal_correction,verification_needed}],verified_resolutions,blockers_to_design_98_98,implementation_validation_gates,coverage,limitations,report_complete:true}. Group duplicate causes; preserve exact evidence for actionable defects. An unexplained limitation survives in coverage and score rationale. No hidden reasoning, shell/tools or external retrieval.\nFULL EVIDENCE WORKPAPERS\n'''+json.dumps(reports,indent=2)
for file in ('design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md'):
 prompt+='\nEXACT FULL SOURCE '+file+'\n'+(D/'input'/file).read_text()
doc=(D/'input/design-c4/docs/Android MCP Bridge Design.md').read_text()
for first,last in [('## Verified design mechanisms after round013','# 19 Verification and delivery'),('# Full binding coverage and generic invocation','## Architecture diagram')]:
 if first in doc:
  start=doc.index(first);end=doc.find(last,start+len(first));end=len(doc) if end<0 else end
  prompt+='\nEXACT RECONCILIATION EXCERPT '+first+'\n'+doc[start:end]
(S/'prompt.txt').write_text(prompt)
source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
number='018-recovered-synthesis';oldD=D;D=S
print(json.dumps(dict(prepared_at=now(),prompt_bytes=len(prompt.encode()),input_manifest_sha256=sha(S/'input-manifest.json'),evidence_reports=len(reports),scope='design-only',tools=[])),flush=True)
exec(compile(tail,str(S/'capture.py'),'exec'))
