"""Offline reproducible catalog import. No network, devices, or bridge application.
Usage: python build_catalog.py INVENTORY OUTPUT
Sources' local_path is relative to inventory.parent. Refuses populated output.
"""
import pathlib,json,sqlite3,hashlib,datetime,sys,xml.etree.ElementTree as ET,zipfile,collections,os,re,gzip
from parsers import signature,aidl,permission,unknown,sha,split_top,typ
from aidl_members import enrich as enrich_aidl
P=pathlib.Path(__file__).resolve().parents[1]
def js(x):return json.dumps(x,sort_keys=True,separators=(',',':'),ensure_ascii=False)
def hb(b):return hashlib.sha256(b).hexdigest()
def processing_fingerprint():
 return sha(js({name:hb((P/name).read_bytes()) for name in ('tooling/build_catalog.py','tooling/parsers.py','tooling/aidl_members.py','tooling/aidl_lex.py','tooling/foundation_extract.py','tooling/java_source.py','tooling/sdk_api_index.py','tooling/jvm_source_link.py','tooling/java_context.py','tooling/annotation_link.py','contracts/Entry.schema.json','contracts/PermissionExpression.schema.json')}))
def ref(text):return dict(text=text,status='unknown',codec_status='unknown',reason='Source type retained; runtime resolver/codec not verified')
def cached_records(s,root):
 cache=s.get('metadata_cache')
 if not cache:raise ValueError('Missing declaration cache '+s['path'])
 path=root/cache['path']
 if hb(path.read_bytes())!=cache['sha256']:raise ValueError('Metadata cache SHA256 mismatch '+s['path'])
 count=0
 with gzip.open(path,'rt') as f:
  for line in f:count+=1;yield json.loads(line)
 if count!=cache['records']:raise ValueError('Metadata cache count mismatch '+s['path'])
def _build(inventory,output,connections):
 invpath=pathlib.Path(inventory).resolve();root=invpath.parent;spec=json.loads(invpath.read_text());out=pathlib.Path(output).resolve()
 out.mkdir(parents=True,exist_ok=True)
 if list(out.iterdir()):raise ValueError('Output must be empty; preserve immutable earlier publication')
 meta=spec.get('catalog')
 if not isinstance(meta,dict) or any(k not in meta for k in ('registry_version','android_version','api_level','coverage')):raise ValueError('Required catalog metadata is absent')
 if not isinstance(meta['registry_version'],int) or isinstance(meta['registry_version'],bool) or meta['registry_version']<1:raise ValueError('registry_version must be a positive integer')
 if not isinstance(meta['api_level'],int) or isinstance(meta['api_level'],bool) or meta['api_level']<1:raise ValueError('Target catalog api_level must be a positive integer')
 if not all(isinstance(meta[k],str) and meta[k] for k in ('android_version','coverage')):raise ValueError('Catalog version/coverage must be nonempty strings')
 sources=[s for s in spec['sources'] if s['status']=='downloaded'];processor_hash=processing_fingerprint()
 sources.sort(key=lambda s:0 if s['kind']=='sdk-api-index' else 1)
 for source in sources:
  if 'api_level' not in source or not isinstance(source.get('channel'),str) or not source['channel']:raise ValueError('Explicit source api_level/channel metadata required')
  level=source['api_level']
  if level is not None and (not isinstance(level,int) or isinstance(level,bool) or level<1):raise ValueError('Source api_level must be positive or null (unknown)')

 db=sqlite3.connect(out/'catalog.staging.sqlite');db.execute('PRAGMA foreign_keys=ON');db.execute('PRAGMA cache_size=-524288');db.execute('PRAGMA temp_store=MEMORY')
 connections.append(db)
 db.executescript((P/'migrations/001-initial.sql').read_text());db.executescript((P/'migrations/002-evidence-and-publication.sql').read_text())
 db.execute('BEGIN IMMEDIATE')
 for s in sources:
  b=(root/s['local_path']).read_bytes()
  if hb(b)!=s['sha256']:raise ValueError('SHA256 mismatch '+s['path'])
  if hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()!=s['git_blob']:raise ValueError('Git blob mismatch '+s['path'])
  acquisition={k:s.get(k) for k in ('url','revision','sha256','retrieved_at','path','api_level','channel','kind','license','scope')}
  source_processor=processor_hash
  if s.get('metadata_cache'):
   source_processor=sha(js(dict(processor=processor_hash,cache_sha256=s['metadata_cache']['sha256'],cache_parser_sha256=s['metadata_cache'].get('parser_sha256'),java_file_context=s.get('java_file_context'))))
  s['source_id']=sha(js(dict(acquisition=acquisition,processing_sha256=source_processor)));s['x-source-identity-version']=3;s['x-processing-sha256']=source_processor
  db.execute('INSERT INTO sources VALUES (?,?,?,?,?,?,?,?,?,?)',(s['source_id'],s['path'],s['url'],s['revision'],s['retrieved_at'],s['sha256'],s['api_level'],s['channel'],s['kind'],s['license']))
  db.execute('INSERT INTO source_details VALUES (?,?)',(s['source_id'],js(s)))
 set_hash=sha(js(sorted(s['source_id'] for s in sources)));registry_id=sha(js(dict(source_set_hash=set_hash,metadata=meta)))
 # This private staging database is not published until every validation/export gate passes.
 db.execute('INSERT INTO registries VALUES (?,?,?,?,?,?,?,?,?)',(registry_id,meta['registry_version'],'1.2.0',meta['android_version'],meta['api_level'],set_hash,datetime.datetime.now(datetime.timezone.utc).isoformat(),'staged','{}'))
 db.execute('UPDATE catalog_view SET registry_generation=? WHERE singleton=1',(meta['registry_version'],))
 source_by_id={s['source_id']:s for s in sources};symbols_by_value=collections.defaultdict(set);symbol_rows=[]
 # Separate constant pass avoids source order changing symbolic permission resolution.
 for s in sources:
  if s['kind'] not in ('sdk-signature','source-signature','java-source'):continue
  records=cached_records(s,root) if s['kind']=='java-source' else signature((root/s['local_path']).read_text())
  for r in records:
   if r['kind']=='field' and r['extras'].get('constant_raw'):
    value=r['extras']['constant_raw']
    try:value=json.loads(value)
    except ValueError:continue
    if isinstance(value,str) and ('.permission.' in value or (r['owner'] or '').endswith('.permission')):
     symbol=r['owner']+'.'+r['name'];symbols_by_value[symbol].add(value);symbol_rows.append((symbol,value,s['source_id'],r['line']))
 symbols={k:next(iter(v)) for k,v in symbols_by_value.items() if len(v)==1}
 db.executemany('INSERT OR IGNORE INTO permission_symbols VALUES (?,?,?,?)',symbol_rows)
 exact_index_targets={};index_versions={};jvm_owners={};jvm_keys={};linked_permissions=0
 for s in sources:
  if s['kind']=='sdk-api-index':
   detected=set()
   for r in cached_records(s,root):
    platform=str(r['extras'].get('target_platform_api',''))
    if not platform:raise ValueError('API index cache lacks target platform version')
    if s.get('platform_api_version') is not None and str(s['platform_api_version'])!=platform:raise ValueError('API index target platform mismatch')
    detected.add(platform)
    if '#' not in r['canonical']:jvm_owners.setdefault(platform,{})[r['owner']]=r['extras']['jvm_owner']
    jvm_keys.setdefault(platform,{})[r['canonical']]=sha('jvm:'+r['canonical'])
   if len(detected)>1:raise ValueError('API index cache mixes target platform versions')
   index_versions[s['source_id']]=str(s.get('platform_api_version') or next(iter(detected),''))
   exact_index_targets.setdefault(index_versions[s['source_id']],{})
 def issue(s,line,reason,raw,category='unsupported'):
  db.execute('INSERT INTO issues(source_id,line,category,reason,raw) VALUES (?,?,?,?,?)',(s['source_id'],line,category,reason,raw))
 def add(r,s,lang='java'):
  nonlocal linked_permissions
  canonical=lang+':'+('package:' if r['kind']=='package' else '')+r['canonical'];bid=sha(canonical);line=r['line'];ast=permission(r['annotations'],symbols,lang=='aidl')
  extensions=dict(language=lang,source_kind=s['kind'],source_path=s['path'],source_scope=s['scope'],annotations=r['all_annotations'],parameters=r['parameters']);extensions.update(r['extras']);target_jvm=None
  if lang=='java-source':
   from jvm_source_link import key as source_jvm_key
   platform=str(s.get('platform_api_version',''));candidate=source_jvm_key(r,jvm_owners.get(platform,{}))
   if candidate in jvm_keys.get(platform,{}):extensions['proven_current_sdk_jvm_identity']=candidate;target_jvm=jvm_keys[platform][candidate]
   extensions['identity_scope']='Source-spelled type identity; wildcard/type-variable resolution unknown; no JVM equivalence inferred without proof'
  elif lang=='jvm':extensions['identity_scope']='Published JVM owner/member key; methods include descriptors; field descriptor/type and Java type kind may be unknown'
  doc=dict(schema_version='1.0.0',entry_id=bid,canonical_signature=canonical,kind=r['kind'],name=r['name'],package=r['package'],permission=ast,provenance=[dict(source_id=s['source_id'],line=line,raw_signature=r['raw'],source_revision=s['revision'])],origin='imported',state='active',registry_version=meta['registry_version'],overlay_revision=0,codec_status='unknown',unresolved=['OS authority, MCP client grants and wire codecs are not established by catalog metadata'],required_semantics=[],extensions=extensions)
  if r['kind'] in ('aidl-parcelable','aidl-union','aidl-enum','type'):doc['schema_version']='1.1.0'
  if r['owner'] is not None:doc['owner']=r['owner']
  for parameter in doc['extensions']['parameters']:parameter['permission']=permission(parameter['annotations'],symbols,lang=='aidl')
  doc['arguments']=[ref(x['type']) for x in r['parameters']]
  if r['return_type'] is not None:doc['return_type']=ref(r['return_type'])
  if r['kind']=='unsupported':doc['unresolved'].append(r['extras'].get('parse_error','Structured AIDL members unresolved'))
  db.execute('INSERT OR IGNORE INTO bindings(binding_id,signature,package,owner,kind,name,return_type,arguments_json,raw_signature) VALUES (?,?,?,?,?,?,?,?,?)',(bid,canonical,r['package'],r['owner'],r['kind'],r['name'],r['return_type'],js([x['type'] for x in r['parameters']]),r['raw']))
  # Duplicate declaration at the exact same locator is a defect, never silently ignored.
  db.execute('INSERT INTO occurrences VALUES (?,?,?)',(bid,s['source_id'],line))
  db.execute('INSERT INTO binding_declarations VALUES (?,?,?,?,?)',(bid,s['source_id'],line,js(doc),sha(js(doc))))
  db.execute('INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES (?,?,?,?,?,?,?)',(bid,s['source_id'],line,','.join(a['name'] for a in r['annotations']) or None,ast['op'],js(ast),js(r['annotations'])))
  if target_jvm and ast['op']!='unknown':
   db.execute('INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES (?,?,?,?,?,?,?)',(target_jvm,s['source_id'],line,'source declaration; exact JVM descriptor link',ast['op'],js(ast),js(r['annotations'])));linked_permissions+=1
  if lang=='jvm':
   target=r['owner'] if '#' not in r['canonical'] else r['owner']+'#'+r['name']+('('+','.join(p['type'] for p in r['parameters'])+')' if r['kind'] in ('method','constructor') else '')
   targets=exact_index_targets[index_versions[s['source_id']]]
   if target in targets and targets[target]!=bid:targets[target]=None
   else:targets[target]=bid
  if r['kind']=='unsupported':issue(s,line,r['extras'].get('parse_error','Structured AIDL body retained without semantic parse'),r['raw'])
  return bid
 totals=collections.Counter();per_source=[]
 for i,s in enumerate(sources):
  kind=s['kind'];sid=s['source_id'];path=root/s['local_path'];report=dict(source_id=sid,kind=kind,input_bytes=s['size_bytes'],declarations=0,unsupported=0,status='retained')
  if kind in ('sdk-signature','source-signature'):
   text=path.read_text();expected=sum(bool(l.strip()) and not l.strip().startswith('//') and l.strip()!='}' for l in text.splitlines())
   for r in signature(text):add(r,s);report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported'
   if expected!=report['declarations']:raise ValueError('Signature reconciliation failed '+s['path'])
   report.update(expected_declarations=expected,status='partial' if report['unsupported'] else 'parsed')
  elif kind in ('java-source','sdk-api-index'):
   for r in cached_records(s,root):
    add(r,s,'jvm' if kind=='sdk-api-index' else 'java-source');report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported'
   report.update(expected_declarations=s['metadata_cache']['records'],status='partial' if report['unsupported'] else 'parsed',semantic_scope='Source declarations; public membership not inferred from visibility' if kind=='java-source' else 'Current platform lifecycle index; field types and type kinds may be unknown')
  elif kind=='manifest':
   try:
    xml=ET.fromstring(path.read_bytes());ns='{http://schemas.android.com/apk/res/android}'
    defs=list(xml.iter('permission'));report['permission_elements']=len(defs)
    for node in defs:
     name=node.attrib.get(ns+'name')
     if name is None:issue(s,None,'Permission element lacks name',ET.tostring(node,encoding='unicode'));continue
     try:db.execute('INSERT INTO permission_definitions VALUES (?,?,?,?)',(name,sid,node.attrib.get(ns+'protectionLevel'),js(dict(attributes=node.attrib,children=[ET.tostring(c,encoding='unicode') for c in node]))))
     except sqlite3.IntegrityError:issue(s,None,'Duplicate permission name in one manifest; raw element retained',ET.tostring(node,encoding='unicode'),'duplicate')
    report['status']='parsed'
   except ET.ParseError as e:issue(s,None,'Manifest XML unresolved: '+str(e),path.read_text(errors='replace'));report['status']='unsupported'
  elif kind=='aidl':
   text=path.read_text()
   try:d=enrich_aidl(text,aidl(text))
   except ValueError as e:
    d=dict(package='',descriptor=None,declaration_kind=None,imports=[],annotations=[],members=[],parse_status='unsupported',reason='AIDL annotation/syntax unresolved: '+str(e))
    add(dict(kind='unsupported',canonical='unparsed-source:'+s['repository']+':'+s['path']+':'+s['sha256'],owner=None,package='',name=s['path'].rsplit('/',1)[-1],return_type=None,parameters=[],annotations=[],all_annotations=[],raw=text,line=1,extras=dict(parse_error=d['reason'],source_bytes_retained=True)),s,'aidl');report['declarations']+=1;report['unsupported']+=1
   db.execute('INSERT INTO aidl_schemas VALUES (?,?,?,?,?,?,?,?)',(sid,d['package'],d['descriptor'] if d['declaration_kind']=='interface' else None,d['declaration_kind'],text,d['parse_status'],js(dict(imports=d['imports'],declaration_annotations=d['annotations'],descriptor_candidate=d['descriptor'],oneway=d.get('oneway'),wire_status='unknown')), 'unknown'))
   if d['descriptor']:
    root_kind='unsupported' if d['parse_status']=='unsupported' else 'aidl-'+d['declaration_kind']
    r=dict(kind=root_kind,canonical=d['descriptor'],package=d['package'],owner=d['descriptor'],name=d['descriptor'].split('.')[-1],return_type=None,parameters=[],annotations=d['annotations'],all_annotations=d['annotations'],raw=text.split('{',1)[0].strip(),line=1,extras=dict(aidl_kind=d['declaration_kind'],imports=d['imports'],parse_status=d['parse_status'],oneway=d.get('oneway'),schema_resolution_reason=d['reason']))
    if root_kind=='unsupported':r['extras']['parse_error']=d['reason']
    add(r,s,'aidl');report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported'
   for r in d['members']:
    r['extras']['interface_annotations']=d['annotations'];r['extras']['interface_permission']=permission(d['annotations'],symbols,True)
    add(r,s,'aidl');report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported'
   report.update(status=d['parse_status'],member_chunks=len(d['members']))
   if d['parse_status'] not in ('methods-parsed','field-metadata-parsed'):issue(s,None,d['reason'],text,'aidl-semantic-gap')
  elif kind=='sdk-annotations':report['status']='deferred-external-annotation-pass'
  elif kind in ('evidence','sdk-history'):report['status']='retained-evidence-not-current-declarations'
  else:issue(s,None,'Unrecognized source kind',kind);report['status']='unsupported'
  per_source.append(report);totals[kind]+=1
  if i%200==0:print('parsed sources',i,'/',len(sources),flush=True)
 # External annotations are retained in full and mapped only when an exact source signature proves the target.
 for s in sources:
  if s['kind']!='sdk-annotations':continue
  report=next(x for x in per_source if x['source_id']==s['source_id']);report.update(xml_documents=0,items=0,annotations=0,mapped_annotations=0,status='parsed')
  with zipfile.ZipFile(root/s['local_path']) as archive:
   for filename in sorted(archive.namelist()):
    if not filename.endswith('.xml'):issue(s,None,'Unrecognized annotation archive member',filename);continue
    try:data=ET.fromstring(archive.read(filename))
    except ET.ParseError as error:
     issue(s,None,'Malformed external annotation XML '+filename+': '+str(error),archive.read(filename).decode(errors='replace'),'annotation-xml');report['status']='partial';continue
    report['xml_documents']+=1
    for ordinal,item in enumerate(data.findall('item')):
     report['items']+=1;name=item.attrib.get('name','');scope='declaration';target=name
     pidx=re.search(r'\s+(\d+)$',target)
     if pidx:scope='parameter:'+pidx.group(1);target=target[:pidx.start()]
     owner,sep,tail=target.partition(' ')
     canonical=None
     if not sep:canonical=owner
     elif '(' in tail:
      a=tail.find('(');b=tail.rfind(')');m=re.search(r'([\w$]+)$',tail[:a]);method=m.group(1) if m else None
      if method:canonical=owner+'#'+('<init>' if method==owner.split('.')[-1] else method)+'('+','.join(typ(x) for x in split_top(tail[a+1:b]) if x.strip())+')'
     else:canonical=owner+'#'+tail.split()[-1]
     target_match_kind='exact';matched_canonical=canonical
     candidates=[canonical] if canonical else []
     if sep and canonical and '(' in canonical:
      head,args=canonical.split('(',1)
      short=re.sub(r'(?<![\w.$])java\.lang\.', '',args)
      if short!=args:candidates.append(head+'('+short)
     elif not sep:candidates=[owner,'package:'+owner]
     if s.get('platform_api_version') is not None:
      platform=str(s['platform_api_version']);current=exact_index_targets.get(platform,{}).get(canonical)
      matches=[(canonical,current)] if current else []
      if not sep:
       package_candidate='java-source:package:'+owner;package_bid=sha(package_candidate)
       if any(item['kind']=='java-source' and str(item.get('platform_api_version',''))==platform for item in sources) and db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(package_bid,)).fetchone():matches.append((package_candidate,package_bid))
      target_match_kind='current-sdk-index-exact'
     else:
      from annotation_link import fallback_supported
      matches=[(candidate,sha('java:'+candidate)) for candidate in dict.fromkeys(candidates) if db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(sha('java:'+candidate),)).fetchone() and (candidate==canonical or not sep or fallback_supported(db,sha('java:'+candidate),canonical,source_by_id,sha))]
     bid=None
     if len(matches)==1:
      matched_canonical,bid=matches[0]
      if matched_canonical!=canonical:target_match_kind='java-lang-spelling-equivalence' if sep else 'package-namespace'
     else:target_match_kind='unresolved-or-ambiguous'
     anns=[]
     for ai,node in enumerate(item.findall('annotation')):
      a=dict(name=node.attrib.get('name',''),arguments=','.join(v.attrib.get('name','')+'='+v.attrib.get('val','') for v in node.findall('val')),raw=ET.tostring(node,encoding='unicode'),values=[dict(v.attrib) for v in node.findall('val')]);anns.append(a)
      a['permission']=permission([a],symbols)
      evidence=dict(item_name=name,archive_member=filename,item_ordinal=ordinal,annotation=a,target_resolution='exact' if bid else 'unknown',target_match_kind=target_match_kind,matched_canonical_signature=matched_canonical if bid else None,target_resolution_reason=None if bid else 'Target absent or ambiguous in the acquired declaration inventory')
      db.execute('INSERT INTO annotation_evidence VALUES (?,?,?,?,?)',(s['source_id'],filename+':'+str(ordinal)+':'+str(ai),bid,scope,js(evidence)));report['annotations']+=1;report['mapped_annotations']+=bid is not None
     if bid and scope=='declaration':
      ast=permission(anns,symbols)
      if ast['op']!='unknown':db.execute('INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES (?,?,?,?,?,?,?)',(bid,s['source_id'],ordinal+1,'external',ast['op'],js(ast),js(anns)))
 for report in per_source:db.execute('INSERT INTO parse_reports VALUES (?,?)',(report['source_id'],js(report)))
 # One conservative discovery entry; all occurrence documents remain authoritative evidence.
 for (bid,) in db.execute('SELECT binding_id FROM bindings ORDER BY binding_id').fetchall():
  rows=db.execute('SELECT source_id,line,document_json FROM binding_declarations WHERE binding_id=? ORDER BY source_id,line',(bid,)).fetchall()
  def rank(row):
   s=source_by_id[row[0]]
   return (0 if s['kind']=='sdk-signature' else 1 if s['kind']=='source-signature' and s['path'].split('/')[-1]=='current.txt' else 2 if s['kind']=='source-signature' else 3,s['path'])
  rows.sort(key=rank);doc=json.loads(rows[0][2]);doc['provenance']=[p for row in rows for p in json.loads(row[2])['provenance']]
  expressions=[json.loads(row[0]) for row in db.execute('SELECT expression_json FROM permission_relations WHERE binding_id=?',(bid,))]
  def semantic(a):
   return {k:semantic(v) for k,v in a.items() if not k.startswith('x-')} if isinstance(a,dict) else [semantic(v) for v in a] if isinstance(a,list) else a
  known={js(semantic(a)):a for a in expressions if not (a['op']=='unknown' and a.get('reason','').startswith('No declared recognized'))}
  if len(known)==1:doc['permission']=next(iter(known.values()))
  elif len(known)>1:doc['permission']=unknown('Source-dependent or conflicting permission metadata; select exact declaration evidence');doc['extensions']['permission_variants']=list(known.values())
  variants={}
  for source_id,locator,document in rows:
   declaration=json.loads(document)
   shape={k:declaration.get(k) for k in ('kind','arguments','return_type')}
   shape['modifiers']=declaration['extensions'].get('type_header') or sorted(set(re.findall(r'\b(public|protected|private|static|final|abstract|default|synchronized|native|strictfp|deprecated|oneway)\b',declaration['provenance'][0]['raw_signature'])))
   variants.setdefault(js(shape),dict(shape=shape,occurrences=[]))['occurrences'].append(dict(source_id=source_id,line=locator))
  if len(variants)>1:doc['extensions']['declaration_variants']=list(variants.values());doc['unresolved'].append('Source-dependent declaration shape/modifiers; select exact occurrence before invocation')
  absence=[a for a in expressions if a['op']=='unknown' and a.get('reason','').startswith('No declared recognized')]
  if len(known)>1 or (known and absence):
   evidence_variants={}
   for expression,source_id,line,annotation in db.execute('SELECT expression_json,source_id,line,annotation FROM permission_relations WHERE binding_id=?',(bid,)):
    node=json.loads(expression);key=js(semantic(node))
    variant=evidence_variants.setdefault(key,dict(node,**{'x-occurrences':[]}))
    variant['x-occurrences'].append(dict(source_id=source_id,line=line,annotation=annotation))
    if node['op']=='unknown' and node.get('reason','').startswith('No declared recognized'):variant['x-absence-of-annotation']=True
   doc['extensions']['permission_variants']=list(evidence_variants.values())
  doc['extensions']['occurrence_count']=len(rows);content=js(doc);digest=sha(content)
  db.execute('INSERT INTO registry_entries VALUES (?,?,?,?)',(registry_id,bid,content,digest))
 set_hash=sha(js(sorted(s['source_id'] for s in sources)));registry_id=sha(js(dict(source_set_hash=set_hash,metadata=meta)))
 counts={name:db.execute('SELECT count(*) FROM '+('registry_entries' if name=='effective_entries' else name)).fetchone()[0] for name in ('sources','bindings','occurrences','binding_declarations','permission_definitions','permission_symbols','permission_relations','aidl_schemas','annotation_evidence','issues','effective_entries')}
 validation=dict(integrity_check=db.execute('PRAGMA integrity_check').fetchone()[0],foreign_key_check=db.execute('PRAGMA foreign_key_check').fetchall(),counts=counts,schema_user_version=db.execute('PRAGMA user_version').fetchone()[0],source_reports=per_source,unavailable_paths=sum(s['status']!='downloaded' for s in spec['sources']),proven_jvm_source_permission_links=linked_permissions,complete=False)
 # Schema validation is an import gate, not a post-publication reporting step.
 import jsonschema
 entry_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text()))
 ast_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/PermissionExpression.schema.json').read_text()))
 for bid,document in db.execute('SELECT entry_id,document_json FROM registry_entries WHERE registry_id=?',(registry_id,)):
  errors=list(entry_validator.iter_errors(json.loads(document)))
  if errors:raise ValueError('Entry schema rejected '+bid+': '+errors[0].message)
 for (expression,) in db.execute('SELECT expression_json FROM permission_relations'):
  errors=list(ast_validator.iter_errors(json.loads(expression)))
  if errors:raise ValueError('Permission schema rejected: '+errors[0].message)
 validation['json_schema_failures']=[]
 if validation['integrity_check']!='ok' or validation['foreign_key_check']:raise ValueError('Integrity/FK validation failed')
 if counts['bindings']!=counts['effective_entries'] or counts['occurrences']!=counts['binding_declarations']:raise ValueError('Record reconciliation failed')
 db.executemany('INSERT INTO registry_sources VALUES (?,?)',[(registry_id,s['source_id']) for s in sources])
 db.execute("UPDATE registries SET state='validated',validation_json=? WHERE registry_id=?",(js(validation),registry_id))
 db.execute("UPDATE registries SET state='published' WHERE registry_id=?",(registry_id,))
 db.execute('INSERT INTO active_registry(singleton,registry_id) VALUES (1,?)',(registry_id,))
 db.executemany('INSERT INTO catalog_meta VALUES (?,?)',[('schema_version','1.2.0'),('entry_contract_version','1.1.0'),('rebuilt_with_corrected_extractor','true'),('source_set_hash',set_hash),('coverage',meta['coverage']),('source_inventory_sha256',hb(invpath.read_bytes())),('authority','Metadata only; no OS capability or MCP client grant'),('sdk_api_level',str(meta.get('sdk_api_level','unknown'))),('current_sdk_metadata_api_level',str(meta.get('current_sdk_metadata_api_level','unknown')))])
 db.commit();db.execute('PRAGMA optimize');db.close()
 db=sqlite3.connect(out/'catalog.staging.sqlite');db.row_factory=sqlite3.Row
 connections.append(db)
 for table in ('sources','source_details','bindings','occurrences','binding_declarations','permission_definitions','permission_symbols','permission_relations','aidl_schemas','annotation_evidence','issues','parse_reports'):
  with (out/(table+'.jsonl')).open('w') as f:
   for row in db.execute('SELECT * FROM '+table):f.write(js(dict(row))+'\n')
 with (out/'entries.jsonl').open('w') as f:
  for row in db.execute('SELECT document_json FROM effective_entries ORDER BY entry_id'):f.write(row[0]+'\n')
 # JSON export is streamed to avoid loading the catalog into memory.
 with (out/'catalog.json').open('w') as f:
  f.write('{"schema_version":"1.2.0","complete":false,"entries":[');first=True
  for row in db.execute('SELECT document_json FROM effective_entries ORDER BY entry_id'):
   if not first:f.write(',')
   f.write(row[0]);first=False
  f.write(']}')
 db.close();(out/'validation.json').write_text(json.dumps(validation,indent=2))
 # catalog.sqlite is the final publication marker, after schema validation and exports.
 os.replace(out/'catalog.staging.sqlite',out/'catalog.sqlite')
 print(json.dumps(dict(counts=counts,integrity_check=validation['integrity_check'],unavailable_paths=validation['unavailable_paths']),indent=2),flush=True)
 return validation
def build(inventory,output):
 out=pathlib.Path(output).resolve()
 if out.exists() and list(out.iterdir()):raise ValueError('Output must be empty; preserve immutable earlier publication')
 connections=[]
 try:return _build(inventory,output,connections)
 except Exception as error:
  out.mkdir(parents=True,exist_ok=True)
  (out/'validation-failure.json').write_text(json.dumps(dict(published=False,error_type=type(error).__name__,error=str(error)),indent=2))
  raise
 finally:
  for connection in connections:connection.close()
if __name__=='__main__':build(sys.argv[1],sys.argv[2])
