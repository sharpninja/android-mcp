import pathlib,json,gzip,collections,hashlib,datetime,shutil,zipfile
from hidl_source_constraint import exact_constraint
R=pathlib.Path(__file__).resolve().parent
D=R/'integration/hidl-source-constraints-v1';D.mkdir(exist_ok=False)
facts=collections.defaultdict(list)
for line in (R/'outside-corpus-audit/interface-hash-facts-v1/facts.jsonl').read_text().splitlines():
 item=json.loads(line)
 if item['format']=='published-hidl-hash-hex64-and-identifier':facts[(item['source']['repository'],item['source']['revision'])].append(item)
counts=collections.Counter();reasons=collections.Counter()
with gzip.open(R/'outside-corpus-audit/hidl-structural-audit-v3/declarations.jsonl.gz','rt') as source,(D/'constraints.jsonl').open('w') as out:
 for line in source:
  item=json.loads(line);s=item['source'];record=item['record']
  if record['kind']!='interface':continue
  result=exact_constraint(s,record,facts[(s['repository'],s['revision'])]);counts['interface_declarations']+=1
  counts['exact_source_constraints']+='clause' in result
  if 'unknown_reason' in result:reasons[result['unknown_reason']]+=1
  out.write(json.dumps(dict(source=s,declared_descriptor=record['canonical'],constraint=result),sort_keys=True)+'\n')
for name in ['hidl_source_constraint.py','test_hidl_source_constraint.py','test_hidl_source_constraint_resolver.py','audit_hidl_source_constraints.py','integration/hidl-source-constraint-before.log','integration/hidl-source-constraint-after.log','integration/hidl-source-constraint-resolver-tests.log']:
 p=D/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/name,p)
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),unknown_reasons=dict(reasons),tests=10,source_only=True,device_operations=False,full_build_required_after_builder_integration=True,
 builder_integration='Exact repository/revision/path/SHA256 join. Map constraint source_evidence to imported source IDs; use clause for HIDL interface and contained methods, with type-dependency validation unchanged. Source/interface predicate does not establish runtime wire codec or authority.',
 active_release011_unchanged=True,files=[])
for p in sorted(D.rglob('*')):
 if p.is_file():report['files'].append(dict(path=str(p.relative_to(D)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'manifest.json').write_text(json.dumps(report,indent=2))
Z=D.with_suffix('.zip')
with zipfile.ZipFile(Z,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(D.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(D))
receipt=dict(directory=str(D),archive=str(Z),bytes=Z.stat().st_size,sha256=hashlib.sha256(Z.read_bytes()).hexdigest(),counts=dict(counts),tests=10,uploaded=False)
(R/'integration/hidl-source-constraints-v1-actions.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
