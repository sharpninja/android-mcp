"""Coverage evidence for the isolated local-only next generation."""
import pathlib,json,collections,hashlib,datetime,sys
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1];out=R/('coverage-manifest-release'+v+'.json')
if out.exists():raise ValueError('Preserve immutable coverage evidence')
inventory=R/('source-inventory-release'+v+'.json');spec=json.loads(inventory.read_text());sources=spec['sources'];D=R/spec['cache_evidence_root'];java=json.loads((D/'sdk37.2/java-extraction-report.json').read_text());index=json.loads((D/'sdk37.2/api-index-report.json').read_text())
outside=json.loads((R/spec['inventories'][1]['path']).read_text());audit_relative=outside['audit_snapshot'];audit_path=(R/audit_relative).resolve()
if R not in audit_path.parents:raise ValueError('Audit path escapes task root')
audit=json.loads(audit_path.read_text());doc=json.loads((R/'coverage-manifest-release005.json').read_text())
doc.update(generated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,status='next candidate; explicit frozen acquisition checkpoint and semantic gaps')
doc['sdk_java_extraction']={k:v for k,v in java.items() if k!='source_reports'};doc['sdk_java_extraction']['empty_source_members']=[s['path'] for s in sources if s['kind']=='java-source' and s['size_bytes']==0]
doc['sdk_api_lifecycle_index']={k:v for k,v in index.items() if k not in ('excluded','issues')}
doc['source_paths']=dict(total=len(sources),by_status=dict(collections.Counter(s['status'] for s in sources)),downloaded_by_kind=dict(collections.Counter(s['kind'] for s in sources if s['status']=='downloaded')))
aosp=[s for s in sources if not s['repository'].startswith('google-sdk-')];doc['aosp_paths']=dict(selected=len(aosp),by_status=dict(collections.Counter(s['status'] for s in aosp)))
doc['projects']['outside_original_corpus_audit']=dict(enumerated=sum('revision' in p for p in audit['projects']),counts=audit['counts'],matching_paths=sum(p.get('matching_paths',0) for p in audit['projects']),snapshot=audit_relative,retrieval_status='This catalog preserves its frozen acquisition checkpoint, not later downloads')
doc['inventory']=dict(path=inventory.name,bytes=inventory.stat().st_size,sha256=hashlib.sha256(inventory.read_bytes()).hexdigest())
doc['gaps']=[g for g in doc['gaps'] if g['scope'] not in ('AOSP selected source paths unavailable','Repositories outside declared acquisition corpus','AIDL semantic coverage')]
doc['gaps']+=[dict(scope='Discovered source paths not acquired at the frozen input checkpoint',count=sum(s['status']!='downloaded' for s in sources),paths=[dict(repository=s['repository'],revision=s['revision'],path=s['path'],status=s['status']) for s in sources if s['status']!='downloaded']),dict(scope='Repositories outside original corpus',counts=audit['counts'],reason='Uninspected or native/generated/other Binder sources remain unknown; only verified empty pinned trees are classified irrelevant',snapshot=audit_relative),dict(scope='AIDL semantic coverage',reason='Structured field/enumerant/constant declaration metadata is supported by reviewed helper. Raw schemas and unsupported statements remain retained; imports, generated transaction constants, wire layouts/codecs and runtime authority remain unknown where unproven.')]
out.write_text(json.dumps(doc,indent=2));print(json.dumps(doc['source_paths'],indent=2))
