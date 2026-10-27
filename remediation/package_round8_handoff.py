"""Pinned post-R8 integration delta against source owner's immutable007 code."""
import pathlib,json,hashlib,difflib,datetime,sys
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/build-snapshots/release007/catalog')
label='round008'+('-'+sys.argv[1] if len(sys.argv)>1 else '')
if not S.exists():raise ValueError('Expected immutable007 producer missing')
D=R/'integration'/label;D.mkdir(exist_ok=False);rows=[];patch=[]
for p in sorted(C.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts or p.suffix not in ('.py','.json','.sql','.md','.txt'):continue
 name=str(p.relative_to(C));base=S/name;old=base.read_bytes() if base.exists() else b'';new=p.read_bytes()
 if old==new:continue
 target=D/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(new)
 rows.append(dict(file=name,base_sha256=hashlib.sha256(old).hexdigest() if old else None,candidate_sha256=hashlib.sha256(new).hexdigest()))
 patch+=difflib.unified_diff(old.decode().splitlines(True),new.decode().splitlines(True),fromfile='catalog/'+name if old else '/dev/null',tofile='catalog/'+name)
dest=R/'integration'/(label+'-source-contract.patch');dest.write_text(''.join(patch))
sys.path.insert(0,str(C/'tooling'));import cache_fingerprint
receipt=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),base_root=str(S),candidate_root=str(D),patch=str(dest),patch_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),source_files_modified=False,
 tests='evidence/tests-regression-044.txt:141tests passed; evidence/round008-optimized-v2.txt:15tests passed',
 findings={'R8-01':'Recursive nested declarations inventoried, spans masked before parsing members/fields, merged callable heads reject; independent lexical counts gate builder and publication validator. Malformed source retained as explicit unsupported/unreconciled with issues. Adjacent one-way source metadata limited to own declaration header.',
 'R8-02':'Only direct java.lang.Simple imports retain abbreviation; java.lang subpackages explicitly qualified.',
 'R8-03':'Overlay insert/update bases must match active imported documents; copy migration restores active pointer before admitting overlays through these guards. Matching-base and rejected stale/null fixtures.',
 'R8-04':'README refreshed; hashed006size receipt and current25GiBpeak/17GiBadditional-free budget are separate design evidence, not executable source changes.',
 'R8-05':'Nested member types carry enclosing kind and implicit-public visibility; top-level/class-contained types remain package when appropriate.',
 'P8-01':'Offline semantic reader rejects malformed operator structure, quarantines future typed operators with original JSON; metadata extensions untouched.'},
 source_report_alignment='Archive unmapped count, platform and mapping_status fields retained. Own parse-status diagnostic occurs after mapping when unresolved targets remain without matching index; this differs from007missing-index pre-marking and is explicit in diff.',
 cache_contract=2,new_java_cache_fingerprint=cache_fingerprint.current('java-source'),new_index_cache_fingerprint=cache_fingerprint.current('sdk-api-index'),
 mandatory=['Merge into a new isolated candidate; do not overwrite active007snapshot or data','Regenerate Java caches using exact current producer; Java/parser changes alter fingerprint and may change bytes. Do not relabel007cache proof as current','Include full DDL/extractor/dependency/cache provenance, AIDL lexical/parsed status and annotation mapping distributions in next release','Complete current007checkpoint per parent coordination; later corrected production is still required. No instruction to stop or modify its running process'],
 pending_evidence=['New complete135508-entry copy migration validation','Independent next Claude agreement'],
 supersedes='integration/round008-source-actions.json' if label!='round008' else None,
 counter_correction='The initial intermediate handoff counted comment punctuation as statements and failed929valid-source checks. comment_mask now excludes comments while preserving quoted data; no-drop checks remain enforced. Both failed audit and failing comment regression remain retained.',
 corpus_audit='evidence/round008-current-parser-corpus-audit-v2.json',files=rows)
actions=R/'integration'/(label+'-source-actions.json');actions.write_text(json.dumps(receipt,indent=2)+'\n')
state=json.loads((R/'LIVE-STATUS.json').read_text());state.update(updated_at=receipt['created_at'],current_phase='R8fiveartifact fixes+reader correction pass141tests; sourcev2handoff frozen; full copy validation running',tests_passed=141,tests_evidence='evidence/tests-regression-044.txt',source_actions=str(actions.relative_to(R)),source_patch=str(dest.relative_to(R)),migration_session=80751,source_production='007running;006remains completed current evidence; no source writes')
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n')
print(json.dumps(dict(changed_files=len(rows),source_actions=str(actions),java_fingerprint=receipt['new_java_cache_fingerprint'])))
