"""Recover only018's token-limited core pass; retain other exact-baseline passes."""
import pathlib,json,hashlib,datetime,subprocess,sys,concurrent.futures,re,time,os
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-018'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
failed=json.loads((D/'pass-01/completion.json').read_text())
if failed['report_complete'] or failed['result_metadata'].get('terminal_reason')!='aborted_streaming':raise SystemExit('Unexpected core-pass outcome; do not duplicate')
M=D/'main-recovery-manifest.json'
if M.exists():raise SystemExit('Recovery exists; inspect effects')
assigned=[json.loads(v) for v in re.findall(r'^BEGIN FULL ARTIFACT (\{[^\n]+\})$',(D/'pass-01/prompt.txt').read_text(),re.M)]
chunks=[]
for row in assigned:
 raw=(D/'input'/row['file']).read_bytes()
 if sha(raw)!=row['sha256']:raise ValueError('Frozen bytes changed')
 start=0
 while start<len(raw):
  end=min(start+55000,len(raw))
  while end<len(raw) and raw[end]&0xc0==0x80:end-=1
  part=raw[start:end];chunks.append(dict(file=row['file'],start_byte=start,end_byte=end,total_bytes=len(raw),sha256=sha(part),text=part.decode()));start=end
coverage=[{k:v for k,v in row.items() if k!='text'} for row in chunks]
M.write_text(json.dumps(dict(created_at=now(),parent_manifest_sha256=sha((D/'input-manifest.json').read_bytes()),affected_files=assigned,chunks=coverage,scope='Same design-only baseline; no changed working artifacts or Google data'),indent=2)+'\n')
instruction=(D/'requirements.txt').read_text();source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
def run(stage,prompt):
 p=D/stage;p.mkdir();(p/'prompt.txt').write_text(prompt);(p/'input-manifest.json').write_text(json.dumps(dict(parent_manifest_sha256=sha((D/'input-manifest.json').read_bytes()),recovery_manifest_sha256=sha(M.read_bytes()),stage=stage),indent=2))
 code="import pathlib,json,hashlib,subprocess,datetime\nR=pathlib.Path("+repr(str(R))+");D=pathlib.Path("+repr(str(p))+");number="+repr(stage)+"\nnow=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()\nsha=lambda b:hashlib.sha256(b).hexdigest()\nprompt=(D/'prompt.txt').read_text()\n"+tail
 (p/'capture.py').write_text(code);result=subprocess.run([sys.executable,str(p/'capture.py')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True);(p/'capture-output.txt').write_text(result.stdout)
 c=json.loads((p/'completion.json').read_text())
 if result.returncode or c['exit_code']!=0 or not c['report_complete']:raise RuntimeError('Incomplete recovery stage '+str(p))
 return json.loads((p/'review-report.json').read_text())
def inspect(item):
 n,row=item
 prompt=instruction+'\nCORE SECTION EVIDENCE CHECK '+str(n)+'/'+str(len(chunks))+'''\nThis replaces ONLY a token-limited evidence pass, not a new acceptance review. Other section checks inspect remaining exact bytes; passes02..05 inspect other design artifacts. Assess this assigned slice against applicable requirements. Do not reconstruct or score the absent whole design here. If a sentence/definition crosses the byte boundary, mark its unresolved dependency rather than assume missing content. Return a concise complete JSON workpaper promptly; all concrete findings need exact evidence, but speculative improvements are not defects. No tools, hidden reasoning or arbitrary score targets. Return {stage,coverage,findings:[{id,severity,area,artifact,evidence,issue,exact_minimal_correction,verification_needed}],verified_resolutions,cross_artifact_questions,limitations,report_complete:true}. No scores.\nEXACT BYTE RANGE\n'''+json.dumps({k:v for k,v in row.items() if k!='text'})+'\n'+row['text']
 print('Launching core section',n,flush=True);return n,run(f'core-section-{n:02d}',prompt)
state=D/'main-recovery-process.json';state.write_text(json.dumps(dict(pid=os.getpid(),status='running',started_at=now(),sections=len(chunks)),indent=2))
print(json.dumps(dict(launched_at=now(),pid=os.getpid(),sections=len(chunks),manifest_sha256=sha(M.read_bytes()),tools=[])),flush=True)
try:
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:reports={'core-'+str(n):v for n,v in pool.map(inspect,enumerate(chunks,1))}
 for n in (2,3,4,5):
  p=D/f'pass-{n:02d}'
  while not (p/'completion.json').exists():time.sleep(10)
  c=json.loads((p/'completion.json').read_text())
  if not c['report_complete']:raise RuntimeError('Other baseline pass incomplete '+str(n))
  reports['original-pass-'+str(n)]=json.loads((p/'review-report.json').read_text())
 (D/'recovered-observations.json').write_text(json.dumps(reports,indent=2)+'\n')
 prompt=instruction+'''\nONE FINAL HOLISTIC DESIGN SYNTHESIS\nThe original core pass hit its output limit twice without a visible report and was interrupted; it contributes NO findings or scores. Its exact bytes were assigned completely to the smaller section checks below. All remaining original passes are complete. Reconcile every observation against full requirements, distinguish omitted diagram detail from a mechanism already specified in prose, resolve contradictions and deduplicate causes without dropping material defects. Preserve genuine unknown coverage. This is ONE design review pipeline; no Google inventory/extractor/production review or finished-app penalty. Do not repeat bulk analysis: return a complete reasoned design verdict with precise evidence and corrections. Design scores are your independent judgment, not an average. The desired98/98 is not a dictated score. All workpapers follow in full; editable C4/class sources follow for cross-boundary checks. No tools or hidden reasoning. Return {review_scope,requirements_baseline,verdict,scores:{design:{accuracy_0_100,completeness_0_100,rationale}},findings:[{id,severity,area,artifact,evidence,issue,exact_minimal_correction,verification_needed}],verified_resolutions,blockers_to_design_98_98,implementation_validation_gates,coverage,limitations,report_complete:true}.\nFULL WORKPAPERS\n'''+json.dumps(reports,indent=2)
 for file in ('design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md'):
  prompt+='\nEXACT FULL CORE SOURCE '+file+'\n'+(D/'input'/file).read_text()
 report=run('recovered-synthesis',prompt);(D/'review-report.recovered.json').write_text(json.dumps(report,indent=2)+'\n')
 (D/'completion.recovered.json').write_text(json.dumps(dict(completed_at=now(),report_complete=True,scores=report['scores'],scope='design-only',recovery_manifest_sha256=sha(M.read_bytes()),original_pass01_disposition='interrupted token-limit loop; no findings used'),indent=2)+'\n')
except Exception as e:(D/'recovery-failure.json').write_text(json.dumps(dict(at=now(),error=str(e),report_complete=False),indent=2));raise
finally:
 d=json.loads(state.read_text());d.update(status='finished',completed_at=now());state.write_text(json.dumps(d,indent=2))
