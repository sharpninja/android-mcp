"""Guarded task-local candidate copy; never edits frozen011 or owner files."""
import pathlib,shutil,json,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent
O=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation')
D=R/'catalog-integrated012-sourcev1'
if D.exists():raise ValueError('New isolated candidate already exists')
shutil.copytree(R/'catalog-integrated011v3',D)
rows=[]
def record(name,before):
    p=D/name;rows.append(dict(file=name,before_sha256=before,after_sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
def replace(name,old,new):
    p=D/name;raw=p.read_text();before=hashlib.sha256(p.read_bytes()).hexdigest()
    if raw.count(old)!=1:raise ValueError('Unique patch anchor missing: '+name)
    p.write_text(raw.replace(old,new));record(name,before)
replace('tooling/build_catalog.py',"     r['extras']['imports']=d['imports']\n", "     r['extras']['imports']=d['imports']\n     r['extras']['interface_oneway']=bool(d.get('oneway'))\n")
name='tests/test_round11.py';before=hashlib.sha256((D/name).read_bytes()).hexdigest()
shutil.copyfile(O/'catalog'/name,D/name);record(name,before)
wire=json.loads((O/'integration/postfreeze012-wire-actions.json').read_text())
for entry in wire['files']:
    name=entry['file'];source=pathlib.Path(wire['candidate_files'])/name
    if hashlib.sha256(source.read_bytes()).hexdigest()!=entry['candidate_sha256']:raise ValueError('Wire handoff changed')
    if name=='API-CONTRACT.md':
        paragraph=source.read_text().split('Wire tuple identity is immutable/unique;',1)[1]
        p=D/name;before=hashlib.sha256(p.read_bytes()).hexdigest()
        if 'Wire tuple identity is immutable/unique;' in p.read_text():raise ValueError('Already applied')
        p.write_text(p.read_text().rstrip()+'\n\nWire tuple identity is immutable/unique;'+paragraph);record(name,before)
    else:
        p=D/name;before=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
        if before!=entry['base_sha256']:raise ValueError('Wire base mismatch '+name)
        p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,p);record(name,before)
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=str(D),active011_unchanged=True,files=rows,source_processing_changed=True,parser_cache_fingerprints_changed=False,full_build_launched=False,remaining_gate='Interface/module producer activation mapping must be integrated with canonical resolver contract before next full build')
(R/'integration/round012-source-local-integration.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
