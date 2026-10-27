"""One combined review pipeline: byte-complete bounded evidence packets, then synthesis.
No reviewer tools; preserves failed014/015 and uses their exact approved baseline.
"""
import pathlib,json,hashlib,datetime,subprocess,concurrent.futures,sys
R=pathlib.Path(__file__).resolve().parent
D=R/'reviews'/('round-'+(sys.argv[1] if len(sys.argv)>1 else '016'));OLD=R/'reviews/round-015'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda b:hashlib.sha256(b).hexdigest()
if D.exists():raise SystemExit('016 exists; inspect its effects before any retry')
old=json.loads((OLD/'completion.json').read_text())
if old['exit_code']!=1 or old['result_metadata'].get('api_error')!='autocompact_thrashing':raise SystemExit('Unexpected015outcome')
for p in (R/'reviews').glob('round-*/process.json'):
 if json.loads(p.read_text()).get('status')=='running' and not (p.parent/'completion.json').exists():raise SystemExit('Potential active review '+str(p))
D.mkdir();index=json.loads((OLD/'input-manifest.json').read_text());rows=index['files']
# Exact byte partitions, never lossy summaries. Separate oversized file slices use
# byte offsets and UTF-8 boundaries so every source byte remains assigned once.
packets=[];packet=[];size=0;limit=260_000
for row in sorted(rows,key=lambda r:r['file']):
 p=OLD/'input'/row['file'];raw=p.read_bytes()
 if p.is_symlink() or sha(raw)!=row['sha256']:raise ValueError('Frozen hash mismatch '+str(p))
 start=0
 while start<len(raw):
  end=min(start+limit,len(raw))
  while end<len(raw) and raw[end]&0xc0==0x80:end-=1
  part=raw[start:end];piece=dict(file=row['file'],start_byte=start,end_byte=end,total_bytes=len(raw),sha256=sha(part),text=part.decode('utf-8'))
  if packet and size+len(part)>limit:packets.append(packet);packet=[];size=0
  packet.append(piece);size+=len(part);start=end
 if not raw:packet.append(dict(file=row['file'],start_byte=0,end_byte=0,total_bytes=0,sha256=sha(raw),text=''))
if packet:packets.append(packet)
coverage=[{k:v for k,v in piece.items() if k!='text'} for group in packets for piece in group]
manifest=dict(created_at=now(),source_manifest_sha256=sha((OLD/'input-manifest.json').read_bytes()),source_manifest=str(OLD/'input-manifest.json'),files=rows,packet_count=len(packets),byte_coverage=coverage,scope='Same approved209-file baseline; no later working changes; failed015 retained')
(D/'input-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
instruction=(R/'combined-review-instruction.txt').read_text()
capture=(R/'review_design_round.py').read_text();tail=capture[capture.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
def run_stage(stage,prompt):
 directory=D/stage;directory.mkdir();(directory/'prompt.txt').write_text(prompt)
 (directory/'input-manifest.json').write_text(json.dumps(dict(parent_manifest_sha256=sha((D/'input-manifest.json').read_bytes()),stage=stage,prompt_sha256=sha(prompt.encode())),indent=2))
 code="import pathlib,json,hashlib,subprocess,datetime\n"+"R=pathlib.Path("+repr(str(R))+");D=pathlib.Path("+repr(str(directory))+");number="+repr(stage)+"\n"+"now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()\nsha=lambda b:hashlib.sha256(b).hexdigest()\nprompt=(D/'prompt.txt').read_text()\n"+tail
 runner=directory/'capture.py';runner.write_text(code)
 result=subprocess.run([sys.executable,str(runner)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 (directory/'capture-output.txt').write_text(result.stdout)
 completion=json.loads((directory/'completion.json').read_text()) if (directory/'completion.json').exists() else {}
 if result.returncode or completion.get('exit_code')!=0:raise RuntimeError('Stage failed; inspect '+str(directory))
 report=json.loads((directory/'review-report.json').read_text())
 if not report.get('report_complete'):raise RuntimeError('Incomplete stage '+str(directory))
 return report
def inspect(item):
 n,group=item
 text='\n'.join('BEGIN EXACT ARTIFACT SLICE '+json.dumps({k:v for k,v in piece.items() if k!='text'})+'\n'+piece['text']+'\nEND EXACT ARTIFACT SLICE' for piece in group)
 prompt=instruction+'\nBOUNDED EVIDENCE INSPECTION '+str(n)+'/'+str(len(packets))+'''\nThis is an evidence-inspection stage of ONE combined hostile review, not a separate area acceptance review. Do NOT give scores or readiness yet. Inspect every supplied byte slice. Other stages cover all remaining baseline files; final synthesis receives their full observations. Artifact slices may begin/end mid-definition: mark cross-slice questions instead of inventing omitted content. Retain concrete findings, exact citations, prior findings verified/refuted and cross-contract dependencies. Findings must distinguish a defect from missing evidence. No tools, code execution, browsing or hidden thinking. Return JSON {stage,files_slices_assessed,findings:[{id,severity,area,artifact,evidence,issue,exact_minimal_correction,verification_needed}],verified_resolutions,cross_slice_questions,coverage_limits,report_complete:true}. Supply concise evidence and all material findings without repeating bulk input. This overrides the final-score output format above for this intermediate stage only.\n'''+text
 print('Starting evidence packet',n,flush=True)
 return n,run_stage(f'packet-{n:02d}',prompt)
(D/'process.json').write_text(json.dumps(dict(pid=__import__('os').getpid(),status='running',started_at=now(),mode='bounded no-tools evidence packets then holistic synthesis',packet_count=len(packets)),indent=2))
print(json.dumps(dict(launched_at=now(),pid=__import__('os').getpid(),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),packets=len(packets),tools=[])),flush=True)
try:
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:reports=dict(pool.map(inspect,enumerate(packets,1)))
 (D/'evidence-observations.json').write_text(json.dumps(reports,indent=2)+'\n')
 # Synthesis sees every complete stage report plus normative design/API, not a
 # selected finding subset. The exact raw packet prompts remain auditable.
 core=[]
 for file in ['design-c4/docs/Android MCP Bridge Design.md','catalog/API-CONTRACT.md','catalog/contracts/openapi.json','evidence/finding-ledger-combined014.json']:
  p=OLD/'input'/file
  if p.exists():core.append('BEGIN COMPLETE CORE '+file+'\n'+p.read_text()+'\nEND COMPLETE CORE')
 prompt=instruction+'''\nFINAL HOLISTIC SYNTHESIS OF THE ONE COMBINED REVIEW\nFailed015 produced no verdict because repeated auto-compaction exhausted its context. This recovery assigned EVERY frozen source byte exactly once across bounded no-tools evidence packets; all complete packet reports follow, together with full normative core artifacts. Reconcile cross-packet questions, deduplicate findings by cause without dropping material gaps, distinguish observed contradictions from incomplete evidence, and inspect every supplied observation. No score averaging. Give one current requirements baseline, all requested per-area scores plus overall accuracy/completeness, exact blockers and report completeness. Do not label the whole project 98/98 while genuine data/reference gaps remain. Explicitly disclose that this is bounded evidence analysis followed by synthesis, not one session reading every original file. Any unresolved packet coverage limit must survive into the verdict. Do not claim Android/runtime implementation or own test execution.\n'''+json.dumps(dict(manifest_sha256=sha((D/'input-manifest.json').read_bytes()),byte_coverage=coverage))+'\nALL COMPLETE EVIDENCE OBSERVATIONS\n'+json.dumps(reports,indent=2)+'\n'+'\n'.join(core)
 result=run_stage('synthesis',prompt)
 (D/'review-report.json').write_text(json.dumps(result,indent=2)+'\n')
 (D/'completion.json').write_text(json.dumps(dict(completed_at=now(),exit_code=0,report_complete=True,packet_count=len(packets),overall=result.get('overall'),scores=result.get('scores')),indent=2))
except Exception as error:
 (D/'completion.json').write_text(json.dumps(dict(completed_at=now(),exit_code=1,report_complete=False,error=str(error)),indent=2));raise
finally:
 state=json.loads((D/'process.json').read_text());state['status']='finished';state['completed_at']=now();(D/'process.json').write_text(json.dumps(state,indent=2))
