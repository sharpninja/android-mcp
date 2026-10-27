import pathlib,json,hashlib,difflib,datetime
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';B=R/'reviews/round-007/input/catalog';D=R/'integration/round005-v2';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/build-snapshots/release005/catalog')
if D.exists():raise SystemExit('Pinned handoff already exists')
D.mkdir();rows=[];patch=[]
for p in sorted(B.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts:continue
 name=str(p.relative_to(B));base=S/name;old=base.read_bytes() if base.exists() else b'';new=p.read_bytes()
 if old==new:continue
 target=D/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(new);rows.append(dict(file=name,base_sha256=hashlib.sha256(old).hexdigest() if old else None,candidate_sha256=hashlib.sha256(new).hexdigest()))
 patch+=difflib.unified_diff(old.decode().splitlines(True),new.decode().splitlines(True),fromfile='catalog/'+name if old else '/dev/null',tofile='catalog/'+name)
dest=R/'integration/round005-v2-source-contract.patch';dest.write_text(''.join(patch));data=json.loads((R/'integration/round005-source-actions.json').read_text());data.update(pinned_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),handoff_revision=2,candidate_root=str(D),patch=str(dest),patch_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),tests='tests-regression-032.txt:108pass, no ResourceWarning; optimizedRound5 fixtures12pass and SDK3pass',source_scope='Exact round007 frozen code, against immutable production005 code; supersedes105-test handoff and preserves it.',files=rows,independent_review='Round007 in progress, not accepted yet')
(R/'integration/round005-v2-source-actions.json').write_text(json.dumps(data,indent=2));status=json.loads((R/'LIVE-STATUS.json').read_text());status.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_actions='integration/round005-v2-source-actions.json',source_patch='integration/round005-v2-source-contract.patch');(R/'LIVE-STATUS.json').write_text(json.dumps(status,indent=2));print(json.dumps({'files':len(rows),'patch_sha256':data['patch_sha256']}))
