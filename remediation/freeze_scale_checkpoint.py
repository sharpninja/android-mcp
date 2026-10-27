import pathlib,shutil,json,hashlib
R=pathlib.Path(__file__).resolve().parent;D=R/'evidence/round012-scale-attempt2';report=json.loads((D/'result.json').read_text());O=D/'code-checkpoint';O.mkdir(exist_ok=False)
base=R/'reviews/round-011/input/catalog';shutil.copytree(base,O/'catalog')
for layer in ('round011-v1','round011-v2'):
 for p in (R/'integration'/layer).rglob('*'):
  if p.is_file():q=O/'catalog'/p.relative_to(R/'integration'/layer);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
for name,value in report['producer_files'].items():assert hashlib.sha256((O/name).read_bytes()).hexdigest()==value,name
files={str(p.relative_to(O)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(O.rglob('*')) if p.is_file()}
(D/'code-checkpoint-manifest.json').write_text(json.dumps(dict(exact_measured_files_verified=True,code_layers=['round011 immutable input','round011-v1','round011-v2'],files=files,current_candidate_supersedes_this_checkpoint=True,note='Full scale/state-yield receipt is exact for this preserved pre-overlay, contract-format2 checkpoint; later contract3/inheritance/metadata composition not retrospectively measured.'),indent=2)+'\n');print('Preserved exact measured code checkpoint; all five runtime file hashes agree')
