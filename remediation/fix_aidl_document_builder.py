import pathlib
p=pathlib.Path(__file__).resolve().parent/'catalog/tooling/build_catalog.py';s=p.read_text().replace('from parsers import signature,aidl,permission','from parsers import signature,aidl,aidl_all,permission')
start=s.index("  elif kind=='aidl':");end=s.index("  elif kind=='sdk-annotations':",start)
replacement='''  elif kind=='aidl':
   text=path.read_text()
   try:
    parsed=aidl_all(text);declarations=[]
    for item in parsed:
     # Mask all other declarations while keeping full physical line numbering.
     span=item.get('declaration_span');fragment=text
     if span:
      fragment=''.join('\\n' if c=='\\n' else ' ' for c in text[:span[0]])+text[span[0]:span[1]]
     declarations.append(enrich_aidl(fragment,item))
   except ValueError as error:
    declarations=[dict(package='',descriptor=None,declaration_kind=None,imports=[],annotations=[],members=[],parse_status='unsupported',reason='AIDL annotation/syntax unresolved: '+str(error))]
   states=[d['parse_status'] for d in declarations]
   status=states[0] if len(states)==1 else 'partial' if any(v in ('unsupported','partial','field-metadata-partial') for v in states) else 'declarations-inventoried'
   first=declarations[0]
   metadata=dict(imports=first['imports'],wire_status='unknown',declarations=[{k:d.get(k) for k in ('descriptor','declaration_kind','parse_status','reason','declaration_line','declaration_ordinal','declaration_span')} for d in declarations])
   db.execute('INSERT INTO aidl_schemas VALUES (?,?,?,?,?,?,?,?)',(sid,first['package'],first['descriptor'] if len(declarations)==1 and first['declaration_kind']=='interface' else None,first['declaration_kind'] if len(declarations)==1 else 'multiple',text,status,js(metadata),'unknown'))
   member_count=0
   for d in declarations:
    if d['descriptor']:
     root_kind='unsupported' if d['parse_status']=='unsupported' else 'aidl-'+d['declaration_kind']
     r=dict(kind=root_kind,canonical=d['descriptor'],package=d['package'],owner=d['descriptor'],name=d['descriptor'].split('.')[-1],return_type=None,parameters=[],annotations=d['annotations'],all_annotations=d['annotations'],raw=d.get('declaration_raw',text).split('{',1)[0].strip(),line=d.get('declaration_line',1),extras=dict(aidl_kind=d['declaration_kind'],imports=d['imports'],parse_status=d['parse_status'],oneway=d.get('oneway'),schema_resolution_reason=d['reason'],declaration_ordinal=d.get('declaration_ordinal',0)))
     if root_kind=='unsupported':r['extras']['parse_error']=d['reason']
    else:r=dict(kind='unsupported',canonical='unparsed-source:'+s['repository']+':'+s['path']+':'+s['sha256'],owner=None,package=d['package'],name=s['path'].rsplit('/',1)[-1],return_type=None,parameters=[],annotations=[],all_annotations=[],raw=text,line=1,extras=dict(parse_error=d['reason'],source_bytes_retained=True))
    add(r,s,'aidl');report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported'
    for r in d['members']:
     r['extras']['interface_annotations']=d['annotations'];r['extras']['interface_permission']=permission(d['annotations'],symbols,True)
     add(r,s,'aidl');report['declarations']+=1;report['unsupported']+=r['kind']=='unsupported';member_count+=1
    if d['parse_status'] not in ('methods-parsed','field-metadata-parsed'):issue(s,d.get('declaration_line'),d['reason'],d.get('declaration_raw',text),'aidl-semantic-gap')
   report.update(status=status,member_chunks=member_count,declaration_count=len(declarations))
'''
s=s[:start]+replacement+s[end:];p.write_text(s)
