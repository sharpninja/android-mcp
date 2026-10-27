"""Focused post-freeze correction; never alters the source owner's candidate."""
import datetime,difflib,hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent;base=R/'reviews/round-008/input/catalog';current=R/'catalog'
D=R/'integration/round008-reader';D.mkdir(exist_ok=True);changes=[];patch=[]
for name in ('tooling/permission_reader.py','tests/test_round8.py'):
    old=(base/name).read_bytes() if (base/name).exists() else b'';new=(current/name).read_bytes()
    target=D/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(new)
    changes.append(dict(file=name,base_sha256=hashlib.sha256(old).hexdigest() if old else None,candidate_sha256=hashlib.sha256(new).hexdigest()))
    patch.extend(difflib.unified_diff(old.decode().splitlines(True),new.decode().splitlines(True),fromfile='catalog/'+name if old else '/dev/null',tofile='catalog/'+name))
dest=R/'integration/round008-reader.patch';dest.write_text(''.join(patch))
doc=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),id='P8-01',found_by='Codex static inspection and failing regression, not a Claude verdict',
 issue='Malformed nonobject/missing/nonstrings semantic ops were quarantined as future operators, contradicting malformed-known-node rejection. Invocation remained fail-closed, but invalid source structures were mislabeled.',
 fix='Require object/string op on schema-defined semantic edges; preserve genuinely future string operators as raw unknown nodes; leave x-* metadata untouched.',
 evidence_before='evidence/round008-reader-before.txt:3tests10failing subcases',evidence_after='evidence/tests-regression-038.txt:128tests; evidence/round008-reader-optimized.txt:3tests',
 patch=str(dest),patch_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),candidate_root=str(D),files=changes,source_files_modified=False,
 production_impact='Offline reader fixture only; builder-generated AST validation is unchanged, as are DDL/contracts/Java/AIDL/cache producers. No request to interrupt an active production build. Independent next review remains required.',
 frozen_round008_modified=False)
(R/'integration/round008-reader-actions.json').write_text(json.dumps(doc,indent=2)+'\n')
state=json.loads((R/'LIVE-STATUS.json').read_text());state.update(updated_at=doc['created_at'],tests_passed=128,tests_evidence='evidence/tests-regression-038.txt',postfreeze_candidate_change='P8-01 reader structural validation; candidate128tests, frozen008still125',postfreeze_reader_actions='integration/round008-reader-actions.json',postfreeze_reader_patch='integration/round008-reader.patch')
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n')
print(json.dumps(dict(patch=str(dest),actions='integration/round008-reader-actions.json',tests=128,next_review_required=True)))
