"""Local-only producer handoff; no upload or changes to remediation workspace."""
import pathlib,json,hashlib,datetime,collections,zipfile,shutil
from interface_applicability_evidence import produce
R=pathlib.Path(__file__).resolve().parent
D=R/'integration/interface-module-producer-v1'
D.mkdir(exist_ok=False)
rows=R/'outside-corpus-audit/interface-module-linkage-v2/source-links.jsonl'
hidl=collections.defaultdict(list)
for line in (R/'outside-corpus-audit/interface-hash-linkage-v1/hidl-links.jsonl').read_text().splitlines():
    item=json.loads(line)
    for source in item.get('type_source_candidates',[]):
        hidl[tuple(source[k] for k in ('repository','revision','path','sha256'))].append(item['fact'])
counts=collections.Counter()
with (D/'source-interface-evidence.jsonl').open('w') as stream:
    for line in rows.open():
        item=json.loads(line);s=item['source'];candidates=hidl[tuple(s[k] for k in ('repository','revision','path','sha256'))]
        scoped=[v for v in candidates if all(s[k]==v['source'][k] for k in ('repository','revision'))]
        for v in candidates:
            if v not in scoped:
                with (D/'cross-context-hidl-candidates.jsonl').open('a') as rejected:
                    rejected.write(json.dumps(dict(source=s,candidate=v,reason='Identifier match is outside exact repository/revision; not admitted'),sort_keys=True)+'\n')
                counts['cross_context_hidl_hash_candidates_not_admitted']+=1
        evidence=produce(s,item['linkage'],scoped)
        stream.write(json.dumps(evidence,sort_keys=True)+'\n')
        counts['sources']+=1;counts['declared_module']+=evidence['declared_module'] is not None
        counts['sources_with_published_hash_candidates']+=bool(evidence['interface_candidates'])
        counts['published_hash_candidate_links']+=len(evidence['interface_candidates'])
        counts['apex_availability_selector_links']+=len(evidence['module_candidates'])
files=['interface_module_facts.py','test_interface_module_facts.py','interface_hash_facts.py','test_interface_hash_facts.py','interface_module_linkage.py','test_interface_module_linkage.py','interface_applicability_evidence.py','test_interface_applicability_evidence.py','package_interface_module_handoff.py','integration/interface-module-producer-v1-tests.log',
'outside-corpus-audit/interface-module-facts-v1/report.json','outside-corpus-audit/interface-module-facts-v1/facts.jsonl','outside-corpus-audit/interface-hash-facts-v1/report.json','outside-corpus-audit/interface-hash-facts-v1/facts.jsonl','outside-corpus-audit/interface-module-linkage-v2/report.json','outside-corpus-audit/interface-module-linkage-v2/source-links.jsonl','outside-corpus-audit/interface-hash-linkage-v1/report.json','outside-corpus-audit/interface-hash-linkage-v1/hidl-links.jsonl']
for name in files:
    target=D/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/name,target)
readme='''# Interface/module producer handoff v1

Pinned source facts, produced from 1,301 metadata paths (1,300 distinct objects).
24 fixtures pass before the full corpus pass. The repository-root aidl_api hash
linkage defect is fixed in v2; immutable v1 evidence remains available separately.

Consume source-interface-evidence.jsonl by exact repository/revision/path/SHA256.
The declared_module object is in the Soong build namespace. module_candidates
contain literal apex_available selectors, including special selectors; they are
NOT installed package IDs or package version intervals. Never map AIDL frozen
version N to an APEX version or HIDL @1.0 to Android API 1.0.

This DTO exposes published hash candidates and source-level version/import links.
It deliberately emits unknown applicability until a consumer has proven the exact
interface descriptor, compiler hash selection/source-schema equivalence, and any
required type dependencies. All AIDL .hash lines remain ordered; multiple lines
are not silently collapsed. HIDL current.txt lines remain distinct compatibility
facts. A useful next proof is exact .hal SHA256 equality with a matching published
identifier, then a descriptor checked against the parsed interface declaration.
Reference: https://source.android.com/docs/core/architecture/hidl/hashing

Required builder integration: retain this DTO in hashed Entry extensions with
all referenced evidence source IDs; derive DeviceResolution interface clauses only
after the obligations above are satisfied. Keep unresolved applicability with
named unknown reasons. A declaration/interface match is not runtime reachability,
wire-proof admission, OS authority, or a client grant. OEM/build-fingerprint and
installed module versions remain unknown without separately evidenced facts.

No canonical contract files, active release011, installed settings or devices
were changed. This is a local handoff, not an uploaded or globally complete corpus.
'''
(D/'README.md').write_text(readme)
manifest=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),contract='source-interface-evidence/1',tests=24,counts=dict(counts),active_release011_unchanged=True,files=[])
for p in sorted(D.rglob('*')):
    if p.is_file():manifest['files'].append(dict(path=str(p.relative_to(D)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'manifest.json').write_text(json.dumps(manifest,indent=2))
Z=D.with_suffix('.zip')
with zipfile.ZipFile(Z,'w',zipfile.ZIP_DEFLATED) as archive:
    for p in sorted(D.rglob('*')):
        if p.is_file():archive.write(p,p.relative_to(D))
receipt=dict(at=manifest['at'],directory=str(D),archive=str(Z),archive_bytes=Z.stat().st_size,archive_sha256=hashlib.sha256(Z.read_bytes()).hexdigest(),counts=dict(counts),tests=24,uploaded=False)
(R/'integration/interface-module-producer-v1-actions.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps(receipt,indent=2))
