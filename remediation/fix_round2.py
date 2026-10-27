import pathlib,json,ast,re
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'

# Remove the obsolete executable importer, retaining only parser helper functions.
p=C/'tooling/foundation_extract.py';s=p.read_text();s=s[:s.index('def permission_logic(')];s=s.replace('Offline, conservative ingestion foundation. Does not fetch or claim complete coverage.','Legacy permission-expression helpers only; no importer or executable entry point.');p.write_text(s)

p=C/'tooling/parsers.py';s=p.read_text().replace(' package=None;owner=None\n for line,raw', ' package=None;owner=None;scopes=[]\n for line,raw')
s=s.replace("  if s=='}':\n   if owner is not None:owner=None\n   else:package=None\n   continue", "  if s=='}':\n   if scopes:package,owner=scopes.pop()\n   else:package=None;owner=None\n   continue\n  if s.endswith('{'):scopes.append((package,owner))")
p.write_text(s)

p=C/'tooling/dispatch_manifest.py';s=p.read_text()
anchor="        digest=hashlib.sha256(json.dumps(entry,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()"
s=s.replace(anchor,"""        document_hash=hashlib.sha256(json.dumps(entry,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        contract={key:entry.get(key) for key in ('canonical_signature','kind','arguments','return_type','codec_status')}
        contract['codec_identity']=entry.get('extensions',{}).get('codec_identity')
        digest=hashlib.sha256(json.dumps(contract,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        permission_hash=hashlib.sha256(json.dumps(entry.get('permission'),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()""")
s=s.replace("schema_hash=digest,", "schema_hash=digest,document_hash=document_hash,permission_hash=permission_hash,")
p.write_text(s)

p=C/'tooling/build_catalog.py';s=p.read_text()
anchor=" sources=[s for s in spec['sources'] if s['status']=='downloaded']"
s=s.replace(anchor,""" meta=spec.get('catalog')
 if not isinstance(meta,dict) or any(k not in meta for k in ('registry_version','android_version','api_level','coverage')):raise ValueError('Required catalog metadata is absent')
 if not isinstance(meta['registry_version'],int) or isinstance(meta['registry_version'],bool) or meta['registry_version']<1:raise ValueError('registry_version must be a positive integer')
 if not isinstance(meta['api_level'],int) or isinstance(meta['api_level'],bool) or meta['api_level']<1:raise ValueError('Target catalog api_level must be a positive integer')
 if not all(isinstance(meta[k],str) and meta[k] for k in ('android_version','coverage')):raise ValueError('Catalog version/coverage must be nonempty strings')
"""+anchor+"""
 for source in sources:
  if 'api_level' not in source or not isinstance(source.get('channel'),str) or not source['channel']:raise ValueError('Explicit source api_level/channel metadata required')
  level=source['api_level']
  if level is not None and (not isinstance(level,int) or isinstance(level,bool) or level<1):raise ValueError('Source api_level must be positive or null (unknown)')
""")
s=s.replace("s['sha256'],37,'stable',s['kind']", "s['sha256'],s['api_level'],s['channel'],s['kind']")
s=s.replace("(registry_id,1,'1.2.0','android-17.0.0_r1',37,set_hash", "(registry_id,meta['registry_version'],'1.2.0',meta['android_version'],meta['api_level'],set_hash")
s=s.replace(" db.execute('INSERT INTO active_registry VALUES (1,?)',(registry_id,))", " db.execute('UPDATE catalog_view SET registry_generation=? WHERE singleton=1',(meta['registry_version'],))\n db.execute('INSERT INTO active_registry VALUES (1,?)',(registry_id,))")
s=s.replace("registry_version=1,overlay_revision=0", "registry_version=meta['registry_version'],overlay_revision=0")
s=s.replace("('coverage','PARTIAL: SDK37.0 complete declared snapshot; current SDK37.2 not acquired; AIDL/manifests bounded partial source corpus')", "('coverage',meta['coverage'])")
s=s.replace("('sdk_api_level','37.0'),('current_sdk_metadata_api_level','37.2')", "('sdk_api_level',str(meta.get('sdk_api_level','unknown'))),('current_sdk_metadata_api_level',str(meta.get('current_sdk_metadata_api_level','unknown')))")
s=s.replace("    data=ET.fromstring(archive.read(filename));report['xml_documents']+=1", """    try:data=ET.fromstring(archive.read(filename))
    except ET.ParseError as error:
     issue(s,None,'Malformed external annotation XML '+filename+': '+str(error),archive.read(filename).decode(errors='replace'),'annotation-xml');report['status']='partial';continue
    report['xml_documents']+=1""")
s=s.replace("  doc['extensions']['occurrence_count']=len(rows);content=js(doc);digest=sha(content)", """  variants={}
  for source_id,locator,document in rows:
   declaration=json.loads(document)
   shape={k:declaration.get(k) for k in ('kind','arguments','return_type')}
   shape['modifiers']=declaration['extensions'].get('type_header') or sorted(set(re.findall(r'\\b(public|protected|private|static|final|abstract|default|synchronized|native|strictfp|deprecated|oneway)\\b',declaration['provenance'][0]['raw_signature'])))
   variants.setdefault(js(shape),dict(shape=shape,occurrences=[]))['occurrences'].append(dict(source_id=source_id,line=locator))
  if len(variants)>1:doc['extensions']['declaration_variants']=list(variants.values());doc['unresolved'].append('Source-dependent declaration shape/modifiers; select exact occurrence before invocation')
  doc['extensions']['occurrence_count']=len(rows);content=js(doc);digest=sha(content)""")
p.write_text(s)

# Allow documented 1.x minor envelopes; additive fields stay under reserved namespaces.
for p in (C/'contracts').glob('*.json'):
 d=json.loads(p.read_text())
 def walk(x):
  if isinstance(x,dict):
   for k,v in x.items():
    if k=='schema_version' and isinstance(v,dict) and v.get('const')=='1.0.0':x[k]={'type':'string','pattern':r'^1\.[0-9]+\.[0-9]+$'}
    else:walk(v)
  elif isinstance(x,list):
   for v in x:walk(v)
 walk(d)
 defs=d.get('$defs',{}) or d.get('components',{}).get('schemas',{})
 if 'Source' in defs:defs['Source']['properties']['api_level']['type']=['integer','null']
 if p.name=='openapi.json':
  d['paths']['/v1/catalog/entries']['get']['responses'].pop('404',None)
  d['paths']['/v1/catalog/registries/{registry_id}/publish']['post']['responses']['404'].pop('headers',None)
 p.write_text(json.dumps(d,indent=2)+'\n')

# Required inventory metadata in all fixture builders, with explicit unknown applicability.
p=C/'tests/test_build.py';s=p.read_text().replace("scope='source',repository='fixture'", "scope='source',repository='fixture',api_level=37,channel='stable'")
s=s.replace('json.dumps(dict(sources=sources))', "json.dumps(dict(catalog=dict(registry_version=1,android_version='fixture',api_level=37,coverage='fixture partial'),sources=sources))")
s=s.replace("('local-overlay',bid,'fixture',1,None,None,'suppressed','{}',now,now)", "(bid,bid,db.execute('SELECT signature FROM bindings WHERE binding_id=?',(bid,)).fetchone()[0],1,None,None,'suppressed','{}',now,now)")
s=s.replace("db.execute('UPDATE overlays SET state=\"active\",document_json=? WHERE entry_id=\"local-overlay\"',(json.dumps({'fixture':'override'}),))", "db.execute('UPDATE overlays SET state=\"active\",revision=2,document_json=? WHERE entry_id=?',(json.dumps({'fixture':'override'}),bid))")
p.write_text(s)

# No correctness check disappears under python -O.
class ExplicitChecks(ast.NodeTransformer):
 def visit_Assert(self,node):
  return ast.copy_location(ast.If(test=ast.UnaryOp(op=ast.Not(),operand=node.test),body=[ast.Raise(exc=ast.Call(func=ast.Name(id='ValueError',ctx=ast.Load()),args=[node.msg or ast.Constant(value='Validation failed: '+ast.unparse(node.test))],keywords=[]),cause=None)],orelse=[]),node)
for name in ('validate_catalog.py','fetch_snapshot.py'):
 p=C/'tooling'/name;s=p.read_text()
 if name=='validate_catalog.py':
  s=s.replace(" failures=[];kinds=", " generation=db.execute('SELECT registry_generation FROM catalog_view WHERE singleton=1').fetchone()[0]\n failures=[];kinds=")
  s=s.replace("'\"cat1:1:'+digest", "'\"cat1:'+str(generation)+':'+digest")
 p.write_text(ast.unparse(ast.fix_missing_locations(ExplicitChecks().visit(ast.parse(s))))+'\n')
