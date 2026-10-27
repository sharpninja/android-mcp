import pathlib,json,hashlib,zipfile,shutil,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/exact-interface-constraints-v1';D.mkdir(exist_ok=False)
files=['aidl_frozen_hash.py','test_aidl_frozen_hash.py','aidl_source_constraint.py','test_aidl_source_constraint.py','hidl_source_constraint.py','test_hidl_source_constraint.py','test_hidl_source_constraint_resolver.py','audit_aidl_frozen_hashes.py','audit_aidl_source_constraints.py','audit_hidl_source_constraints.py','integration/aidl-frozen-hash-after.log','integration/aidl-source-constraint-v1-tests.log','integration/hidl-source-constraint-resolver-tests.log']
for name in files:
 p=D/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/name,p)
for name in ('aidl-frozen-hashes-v1','aidl-source-constraints-v1','hidl-source-constraints-v1'):shutil.copytree(R/'integration'/name,D/name)
for batch in ('aidl-hash-generator-v1','aidl-hash-backend-v1'):
 inventory=json.loads((R/('outside-corpus-audit/source-inventory-batch'+batch+'.json')).read_text());receipts=json.loads((R/('outside-corpus-audit/source-receipts-batch'+batch+'-lowrate001.json')).read_text());rows=[]
 for source in inventory['sources']:
  receipt=receipts[source['git_blob']];raw=(R/receipt['local_path']).read_bytes()
  if hashlib.sha256(raw).hexdigest()!=receipt['sha256']:raise ValueError('Pinned compiler source changed')
  path=D/'google-compiler-source'/source['path'];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw);rows.append({**source,**receipt})
 (D/(batch+'-provenance.json')).write_text(json.dumps(rows,indent=2))
(D/'README.md').write_text('''# Exact interface source predicates v1

All530 AIDL frozen dump hashes verified against full pinned-tree membership and
9691 acquired source files. The new hash verifier is a read-only Python
reproduction of the pinned Google build algorithm; downloaded code was not run.
The pinned aidl_gen_rule.go selects physical tail -1 and passes it as --hash;
the AIDL constraint helper checks this separately from hash tool last-nonempty
verification. It rejects ambiguous/unstable module flags, unlinked frozen module
versions, partial parsing, nonexact source inputs and Descriptor overrides.
1199/1607 parsed interface declarations have exact predicates,408remain unknown.

The HIDL predicate requires exact current.txt descriptor plus source SHA256
equality in the same repository/revision and a parsed interface declaration.
592/765 interfaces satisfy this;173remain unknown. The normative reference is
https://source.android.com/docs/core/architecture/hidl/hashing . Runtime device
facts are never supplied by this source producer; the resolver fixture only
simulates trusted versus missing/untrusted/hash-mismatching observations.

Builder integration must map every source_evidence identity to actual imported
source IDs, preserve the predicate and its proof in hashed Entry metadata, and
include all producer code/data fingerprints in immutable input provenance. The
audited full-tree membership proof and pinned compiler metadata must remain
hash-bound inputs; do not trust an arbitrary external verified:true JSON flag.
These source predicates are orthogonal to complete type-dependency checks,
runtime reachability, transport/wire codecs, OS access and per-client grants.
Neither frozen AIDL N nor HIDL @major.minor is an Android/APEX version interval.

This is a consumer integration handoff, not a claim these predicates already
exist in running immutable release011. It must be wired into a fresh builder
fixture and frozen candidate together with the canonical resolver schema.
14 AIDL tests and10 distinct HIDL/resolver tests pass. The HIDL log executes15
because unittest also collects the imported5 fixture cases; none failed.
No device/system/Claude action or upload occurred.
''')
reports={name:json.loads((R/'integration'/name/'report.json').read_text()) for name in ('aidl-frozen-hashes-v1','aidl-source-constraints-v1')};manifest=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),reports=reports,files=[])
for p in sorted(D.rglob('*')):
 if p.is_file():manifest['files'].append(dict(path=str(p.relative_to(D)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'manifest.json').write_text(json.dumps(manifest,indent=2));Z=D.with_suffix('.zip')
with zipfile.ZipFile(Z,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(D.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(D))
receipt=dict(at=manifest['at'],directory=str(D),archive=str(Z),bytes=Z.stat().st_size,sha256=hashlib.sha256(Z.read_bytes()).hexdigest(),aidl_verified_dump_directories=530,aidl_exact_interface_constraints=1199,hidl_exact_interface_constraints=592,uploaded=False,active_release011_unchanged=True)
(R/'integration/exact-interface-constraints-v1-actions.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
