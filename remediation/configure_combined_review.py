import pathlib,json,ast,shutil,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4'
source=R.parent.parent/'task/android-catalog-20261026'
pending=source/'build-release011-status.json';shutil.copyfile(pending,R/'evidence/build-release011-pending-status.json')
old=(R/'review_design_round.py').read_text();tail=old[old.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
evidence=[R/row['file'] for row in json.loads((R/'reviews/round-012/input-manifest.json').read_text())['files'] if row['file'].startswith('evidence/') and (R/row['file']).exists()]
evidence=[p for p in evidence if p.name not in ['tests-regression-075.txt','tests-regression-075.receipt.json','tests-regression-076.receipt.json','finding-ledger-012.json','progress-012.json']]
extra=['tests-regression-084.txt','tests-regression-084.receipt.json','tests-regression-085.receipt.json','finding-ledger-design014.json','build-release011-pending-status.json','round012-legacy552-disposition.json','release010-retained-unsupported-summary.json','production-reconciliation-release010.amended.json','kotlin-sdk-candidate.json','postfreeze12-wire-original-sql.json']
evidence += [R/'evidence'/name for name in extra if (R/'evidence'/name).exists()]
# One ledger retains both prior finding sets and explicit current dispositions.
r12=json.loads((R/'reviews/round-012/review-report.json').read_text());r13=json.loads((R/'evidence/finding-ledger-design014.json').read_text())
ledger=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),baseline='combined-review-instruction.txt',round012_findings=r12['findings'],round013_findings=r13['findings'],round012_disposition='Working corrections and source handoffs remain unaccepted until this fresh review. Production010 mismatch/global/native/annotation coverage remain open. Current source/metadata integrations are not all merged into this candidate.',current_tests='271normal/271optimized; newcallback/groupruntime notcovered',current_production='010completed;011independentvalidationpending')
(R/'evidence/finding-ledger-combined014.json').write_text(json.dumps(ledger,indent=2)+'\n')
evidence += [R/'evidence/finding-ledger-combined014.json',R/'reviews/round-012/review-report.json',R/'reviews/round-013/review-report.json']
files=[]
for directory in ['docs','architecture','storyboards','ui-svg']:
 files += sorted(p for p in (P/directory).rglob('*') if p.is_file() and p.suffix in ('.md','.json','.dsl','.svg','.html'))
files += [P/name for name in ['README.md','build.gradle.kts','settings.gradle.kts','gradle.properties','toolchain-lock.json','tooling/bootstrap.py','tooling/marp-package.json','tooling/marp-package-lock.json','gradle/wrapper/gradle-wrapper.properties']]
files += sorted((R/'catalog').glob('*.md'))+sorted((R/'catalog').glob('*.sql'))
for directory,pattern in [('contracts','*.json'),('tooling','*.py'),('tests','*.py'),('migrations','*.sql')]:files+=sorted((R/'catalog'/directory).glob(pattern))
files+=evidence
files+=sorted((P/'build/receipts').glob('gradle-validate-004.*'))+[P/'build/receipts/storyboard-browser-validation.json']
up=R/'arc-upstream/19792a52a13a3df342f90bab6b246b547e673919'
files += [up/path for path in ['Directory.Build.props','src/Avalonia.RemoteControl.Protocol/Protos/remote_control.proto','src/Avalonia.RemoteControl.Protocol/BridgeFrameCodec.cs','src/Avalonia.RemoteControl.Protocol/RemoteControlProtocol.cs','src/Avalonia.RemoteControl.Runtime/Bridge/RemoteControlBridgeRequestHandler.cs','src/Avalonia.RemoteControl.Runtime/Bridge/RemoteControlBridgeTcpListener.cs','src/Avalonia.RemoteControl.Client/Bridge/RemoteControlBridgeClient.cs']]
files=list(dict.fromkeys(files));(R/'combined-review-files.json').write_text(json.dumps([str(p.relative_to(R)) for p in files],indent=2)+'\n')
prefix='''import pathlib,json,hashlib,subprocess,datetime,sys,os
R=pathlib.Path(__file__).resolve().parent;number=int(sys.argv[1]);D=R/'reviews'/f'round-{number:03d}'
if D.exists():raise SystemExit('Immutable round exists; inspect before any retry')
for prior in (R/'reviews').glob('round-*/process.json'):
 data=json.loads(prior.read_text())
 if data.get('status')=='running' and not (prior.parent/'completion.json').exists():raise SystemExit('Potential active reviewer: '+str(prior))
D.mkdir();S=D/'input';S.mkdir()
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda b:hashlib.sha256(b).hexdigest()
files=[R/p for p in json.loads((R/'combined-review-files.json').read_text())]
manifest=[];chunks=[]
for p in files:
 rel=str(p.relative_to(R));raw=p.read_bytes();target=S/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
 manifest.append(dict(file=rel,sha256=sha(raw),size_bytes=len(raw)))
 chunks.append('\\nBEGIN UNTRUSTED ARTIFACT '+rel+'\\n'+raw.decode()+'\\nEND ARTIFACT '+rel+'\\n')
instruction=(R/'combined-review-instruction.txt').read_text()
prompt=instruction+'\\nINPUT MANIFEST\\n'+json.dumps(manifest,indent=2)+'\\n'+''.join(chunks)
if len(prompt.encode())>9_000_000:raise SystemExit('Prompt exceeds9MB; no reviewer launched')
(D/'input-manifest.json').write_text(json.dumps(dict(created_at=now(),files=manifest),indent=2));(D/'prompt.txt').write_text(prompt)
'''
(R/'review_combined_round.py').write_text(prefix+tail)
print(json.dumps(dict(files=len(files),last_validated_production='010',source011='independentvalidationpending',ready_to_launch=True)))

