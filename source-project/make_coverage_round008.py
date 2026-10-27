"""Freeze accurate next-release scope without rewriting earlier coverage receipts."""
import pathlib,json,collections,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent
out=R/'coverage-manifest-release008.json'
if out.exists():raise SystemExit('Preserve existing coverage manifest')
I=R/'source-inventory-release008.json';spec=json.loads(I.read_text());sources=spec['sources'];D=R/spec['cache_evidence_root']
audit_path=R/'outside-corpus-audit/classification-full985-v2.json';audit=json.loads(audit_path.read_text())
hidl_path=R/'outside-corpus-audit/hidl-declared-path-inventory-v1.json';hidl=json.loads(hidl_path.read_text())
doc=json.loads((R/'coverage-manifest-release007.json').read_text())
doc.update(generated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,status='Frozen release008 candidate; declared acquisition complete, semantic scope remains partial')
doc['inventory']=dict(path=I.name,bytes=I.stat().st_size,sha256=hashlib.sha256(I.read_bytes()).hexdigest())
doc['sdk_java_extraction']={k:v for k,v in json.loads((D/'sdk37.2/java-extraction-report.json').read_text()).items() if k!='source_reports'}
doc['sdk_api_lifecycle_index']={k:v for k,v in json.loads((D/'sdk37.2/api-index-report.json').read_text()).items() if k not in ('excluded','issues')}
doc['source_paths']=dict(total=len(sources),by_status=dict(collections.Counter(s['status'] for s in sources)),downloaded_by_kind=dict(collections.Counter(s['kind'] for s in sources if s['status']=='downloaded')))
aosp=[s for s in sources if not s['repository'].startswith('google-sdk-')];doc['aosp_paths']=dict(selected=len(aosp),by_status=dict(collections.Counter(s['status'] for s in aosp)))
doc['projects']['outside_original_corpus_audit']=dict(enumerated=985,counts=audit['counts'],snapshot=str(audit_path.relative_to(R)),sha256=hashlib.sha256(audit_path.read_bytes()).hexdigest(),unknown_scope_buckets=audit['unknown_scope_buckets'],retrieval_status='All 5255 declared supplemental paths acquired at this frozen checkpoint')
doc['gaps']=[g for g in doc['gaps'] if g['scope'] not in ('Discovered source paths not acquired at the frozen input checkpoint','Repositories outside original corpus')]
doc['gaps'] += [dict(scope='Native/generated/other Binder semantic coverage outside original corpus',counts=audit['counts'],reason='All 985 pinned trees inspected. 732 remain semantically unknown; filename absence is not irrelevance.',snapshot=str(audit_path.relative_to(R))),dict(scope='HIDL declarations',declared_paths=hidl['paths'],pinned_trees_enumerated=hidl['pinned_trees_enumerated'],repositories=hidl['repositories'],inventory=str(hidl_path.relative_to(R)),sha256=hashlib.sha256(hidl_path.read_bytes()).hexdigest(),status='Conservative public-source retrieval active separately; no HIDL semantic parser or wire schema guarantee in release008')]
doc['authority']='Source metadata grants neither Android OS authority nor MCP client capabilities'
out.write_text(json.dumps(doc,indent=2));print(json.dumps(doc['source_paths'],indent=2))
