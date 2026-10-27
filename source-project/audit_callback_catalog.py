"""Read-only callback discovery over an immutable local catalog, never a device."""
import pathlib,sys,json,sqlite3,collections,datetime,hashlib
from callback_source_facts import type_candidate,endpoint_method,registration_candidates,possible_pairs
R=pathlib.Path(__file__).resolve().parent
release=sys.argv[1]
if len(release)!=3 or not release.isdecimal():raise ValueError('Three digit release')
D=R/('integration/callback-audit-release'+release+'-v2');D.mkdir(exist_ok=False)
database=R/('catalog/output/release-'+release+'/catalog.sqlite')
db=sqlite3.connect(database.as_uri()+'?mode=ro&immutable=1',uri=True)
try:
 types={};owners=collections.defaultdict(list);counts=collections.Counter();groups=collections.defaultdict(list)
 with (D/'type-candidates.jsonl').open('w') as out:
  for (raw,) in db.execute("SELECT document_json FROM registry_entries WHERE entry_id IN (SELECT binding_id FROM bindings WHERE kind IN ('class','interface','enum','annotation','type','aidl-interface','aidl-parcelable','aidl-union','aidl-enum'))"):
   doc=json.loads(raw);counts['declared_types_scanned']+=1;role=type_candidate(doc)
   if not role['is_candidate']:continue
   counts['type_candidates']+=1;e=doc['extensions'];identity=doc['entry_id'];name=doc.get('owner') or doc['canonical_signature'].split(':',1)[-1]
   types[identity]=dict(name=doc['name'],kind=doc['kind'],extensions={k:e[k] for k in ('language','annotations','declared_type_shape') if k in e});owners[(e['language'],name)].append(types[identity])
   out.write(json.dumps(dict(entry_id=identity,canonical_signature=doc['canonical_signature'],classification=role,permission=doc.get('permission'),provenance=doc.get('provenance'),version_evidence=e.get('device_resolution_variants',[])),sort_keys=True)+'\n')
 print('types scanned',counts['declared_types_scanned'],'candidates',counts['type_candidates'],flush=True)
 with (D/'parameter-candidates.jsonl').open('w') as params,(D/'endpoint-methods.jsonl').open('w') as endpoints:
  for (raw,) in db.execute("SELECT document_json FROM registry_entries WHERE entry_id IN (SELECT binding_id FROM bindings WHERE kind IN ('method','aidl-member'))"):
   doc=json.loads(raw);counts['methods_scanned']+=1;arguments=doc.get('arguments',[]);counts['parameters_scanned']+=len(arguments);counts['nonprimitive_parameters']+=sum(a.get('status')!='primitive' for a in arguments)
   candidates=registration_candidates(doc,types);counts['parameter_candidates']+=len(candidates)
   for item in candidates:
    counts[item['role']]+=1;params.write(json.dumps(item,sort_keys=True)+'\n');groups[(item['owner'],item['language'],item['type_spelling'],item['type_id'])].append(item)
   role_types=owners.get((doc['extensions']['language'],doc.get('owner')),[])
   if role_types:
    owner=role_types[0] if len(role_types)==1 else None
    # The aggregate owner may span versions; do not borrow oneway flags from it.
    endpoint=endpoint_method(doc);endpoint.update(callback_role='candidate',owner_role_ambiguous=len(role_types)!=1)
    endpoints.write(json.dumps(endpoint,sort_keys=True)+'\n');counts['candidate_endpoint_methods']+=1
   if counts['methods_scanned']%20000==0:print('callback methods scanned',counts['methods_scanned'],flush=True)
 with (D/'lifecycle-pair-candidates.jsonl').open('w') as out:
  for rows in groups.values():
   for pair in possible_pairs(rows):out.write(json.dumps(pair,sort_keys=True)+'\n');counts['lifecycle_pair_candidates']+=1
 counts['nonprimitive_parameters_without_callback_classification']=counts['nonprimitive_parameters']-counts['parameter_candidates']
 registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0]
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=release,database=str(database),registry_id=registry,counts=dict(counts),producer_sha256=hashlib.sha256((R/'callback_source_facts.py').read_bytes()).hexdigest(),tests=13,scope='All normalized catalog type/method/parameter rows scanned; candidate roles do not prove all callbacks or lifecycle',complete=False,limitations=['Source catalog predates new callback producer and may contain unresolved types, hidden/native/generated sources and older oneway metadata','Absent inherited AIDL oneway flag never proves synchronous; both flags false required; HIDL method flag handled separately','No automatic global short-name resolution or callback lifecycle inferred','Registration/removal pairs are candidates; no runtime invocation, stream or device actions performed','SDK delegates/functional callback roles without names or resolved interface metadata remain explicit unclassified reference parameters'],files=[])
 for p in sorted(D.glob('*.jsonl')):report['files'].append(dict(path=p.name,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
 (D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
finally:db.close()
