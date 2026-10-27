"""Freeze corrected whole design and reserve all stages for one live supervisor."""
from pathlib import Path
import json,hashlib,datetime,subprocess,sys,os
from hv_invariants import preflight
from hv_runtime import ARGS,ENV,alive
R=Path(__file__).resolve().parent;P=R/'design-c4';D=R/'reviews/round-023';now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
if D.exists():raise SystemExit('023 exists; inspect known outcomes before retry')
old=R/'reviews/round-022'
if not json.loads((old/'completion.supervised.json').read_text())['report_complete']:raise ValueError('022 unified verdict not complete')
for f in (R/'reviews').glob('round-*/**/process.json'):
    d=json.loads(f.read_text())
    if d.get('status')=='running' and alive(d['pid']):raise ValueError('Existing task review active: '+str(f))
for name in ('gradle-package-026.completion.json','storyboard-browser-validation-v7.json','reproducibility-003.json','contact-sheet-recovery-v9.json'):
    v=json.loads((P/'build/receipts'/name).read_text())
    if not (v.get('build_successful') or v.get('pass') or v.get('all_render_hashes_identical')):raise ValueError('QA failed: '+name)
subprocess.run([sys.executable,'-m','unittest','discover','-s',str(R),'-p','test_hv_invariants.py'],check=True)
preflight(ARGS,ENV,'Full unbounded design-only HV',[],[],(R/'hv_supervisor.py').read_text(),supervised=True)
paths=[p for p in P.rglob('*') if p.is_file() and not p.is_symlink() and not any(n in p.relative_to(P).parts for n in ('.tools','.gradle-home','.gradle','build'))]
paths += [R/x for x in ('catalog/SOURCE-METADATA-CONTRACT.md','catalog/API-CONTRACT.md','catalog/contracts/openapi.json','catalog/contracts/CallbackDescriptor.schema.json','catalog/contracts/FunctionalGroup.schema.json','tests_design018_contract.py','tests_design020_contract.py','tests_design022_contract.py','evidence/design022-contract-before.txt','evidence/design022-contract-normal.txt','evidence/design022-contract-optimized.txt','evidence/design022-closure-ledger.json','reviews/round-022/supervisor/unified-review-report.txt')]
paths += list((R/'catalog/contracts').glob('*.json')) # Design interface shapes only, no source/dataset coverage assessment.
paths += [P/'build/receipts'/x for x in ('gradle-package-026.request.json','gradle-package-026.completion.json','storyboard-browser-validation-v7.json','reproducibility-003.json','all-slide-capture-v8.json','contact-sheet-recovery-v9.json','design022-visual-inspection.json')]
up=R/'arc-upstream/19792a52a13a3df342f90bab6b246b547e673919'
paths += [up/x for x in ('src/Avalonia.RemoteControl.Protocol/RemoteControlProtocol.cs','src/Avalonia.RemoteControl.Protocol/BridgeFrameCodec.cs','src/Avalonia.RemoteControl.Protocol/Protos/remote_control.proto','src/Avalonia.RemoteControl.Runtime/AvaloniaRemoteControlRuntimeServiceCollectionExtensions.cs','src/Avalonia.RemoteControl.Runtime/Input/RemoteControlInputDispatcher.cs','src/Avalonia.RemoteControl.Runtime/Logging/RemoteControlLogBuffer.cs','src/Avalonia.RemoteControl.Runtime/AvaloniaRemoteControlOptions.cs','src/Avalonia.RemoteControl.Client/Bridge/RemoteControlBridgeClient.cs','src/Avalonia.RemoteControl.Runtime/Bridge/RemoteControlBridgeRequestHandler.cs')]
D.mkdir();S=D/'input';S.mkdir();rows=[];aliases=[];binaries=[];seen={};groups={1:[],2:[],3:[]}
for p in sorted(set(paths)):
    raw=p.read_bytes();rel=str(p.relative_to(R));row=dict(file=rel,size_bytes=len(raw),sha256=sha(raw));q=S/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);q.chmod(0o444);rows.append(row)
    if row['sha256'] in seen:aliases.append(dict(file=rel,byte_identical_to=seen[row['sha256']],sha256=row['sha256'],bytes_verified=True));continue
    seen[row['sha256']]=rel
    try:raw.decode('utf8')
    except UnicodeDecodeError:binaries.append(dict(**row,scope='Pinned build dependency validated by exact hash; no bridge runtime implementation claimed'));continue
    stage=2 if '/docs/diagrams/' in rel else 3 if rel.endswith(('.svg','.html')) or '/storyboards/' in rel or rel.endswith(('ui-asset-manifest.json','ui-screens-and-storyboards.md')) else 1
    groups[stage].append(row)
expected={x['file'] for x in rows}-{x['file'] for x in aliases}-{x['file'] for x in binaries};assigned=[x['file'] for g in groups.values() for x in g]
preflight(ARGS,ENV,'Full unbounded design-only HV',assigned,expected,(R/'hv_supervisor.py').read_text(),supervised=True)
manifest=dict(created_at=now(),fresh_supervised_round=True,scope='Whole current DESIGN ONLY. No Google API surface/acquisition/extraction/population scoring. Schemas are read only as internal interface contracts, never deductions for unfinished source/data/runtime. All architecture, UI, contracts, build and scope requirements retained.',files=rows,byte_identical_aliases=aliases,binary_dependencies=binaries,passes={str(k):v for k,v in groups.items()},shared_context={k:['design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md'] for k in ('pass-02','pass-03')},coverage_complete=True,controls=dict(assistant_caps=None,requested_output_tokens=128000,continuation='Mandatory full visible capture and automatic provider-hard-limit continuation; no finite retry/global output/time/word/turn limit'),previous_unified_verdict=dict(round='022',accuracy=92,completeness=89,sha256=sha((old/'supervisor/unified-review-report.txt').read_bytes())),transport_partition_reason='Prior021 actual provider prompt_too_long with0 inference; coherent exhaustive artifact-family passes retain every body and byte-verified alias, followed by ONE unified verdict.')
(D/'input-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
for stage in ('pass-01','pass-02','pass-03','synthesis'):
    d=D/stage;d.mkdir();(d/'supervisor-reservation.json').write_text(json.dumps(dict(at=now(),known_unstarted=True,owner='hv_supervisor.py',no_prior_inference=True),indent=2)+'\n')
(D/'supervisor-handoff.json').write_text(json.dumps(dict(at=now(),reserved_unstarted_slots=['pass-01','pass-02','pass-03','synthesis'],no_old_coordinator=True,review_model='claude-fable-5-1',effort='max',assistant_caps=None),indent=2)+'\n')
(D/'process.json').write_text(json.dumps(dict(pid=os.getpid(),status='finished',role='snapshot-only preparation; no inference',completed_at=now()),indent=2)+'\n')
for p in sorted(S.rglob('*'),reverse=True):
    if p.is_dir():p.chmod(0o555)
S.chmod(0o555)
print(json.dumps(dict(prepared_at=now(),round=str(D),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),files=len(rows),distinct_text_files=len(assigned),passes={k:len(v) for k,v in groups.items()},assistant_caps=None,inference_launched=False)))
