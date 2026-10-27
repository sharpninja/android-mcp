import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tooling/catalog_projection.py';s=p.read_text()
s=s.replace("if sdks:\n    for item", "if sdks:\n    platform_pairs=[item for item in sdks if item['sdk_id']=='0']\n    if len(platform_pairs)>1 or (platform_pairs and version(platform_pairs[0]['minimum_version'])!=introduced) or (not platform_pairs and 'since' in own):raise ValueError('Inconsistent explicit since/sdk platform evidence')\n    for item")
s=s.replace("  if point[1]==2147483647:raise ValueError('Source minor cannot form an exact discrete interval')\n  return dict(alternatives=[dict(platform=dict(introduced=point,removed=[point[0],point[1]+1]))],evidence_ids=evidence)","  return dict(alternatives=[dict(platform_point=point)],evidence_ids=evidence)")
s=s.replace('self.fields=collections.defaultdict(list)','self.fields=collections.defaultdict(list);self.formals=collections.defaultdict(set)')
s=s.replace("doc=json.loads(raw);self.index[", "doc=json.loads(raw)\n   from annotation_link import formal_names\n   self.formals[(sid,doc.get('owner',''))].update(formal_names(doc['provenance'][0]['raw_signature']))\n   self.index[")
s=s.replace("lang=doc['extensions']['language'];owner=doc.get('owner','');package=doc.get('package','');groups=[]", "lang=doc['extensions']['language'];owner=doc.get('owner','');package=doc.get('package','');groups=[]\n  from annotation_link import formal_names\n  formals=formal_names(doc['provenance'][0]['raw_signature']);parent=owner\n  while parent and parent!=package:\n   formals.update(self.formals.get((sid,parent),set()));parent=parent.rsplit('.',1)[0] if '.' in parent else ''\n  if base.split('.',1)[0] in formals:\n   result.update(status='unknown',reason='Formal type variable requires instantiation proof');return result")
s=s.replace("groups.append([(package+'.' if package else '')+base])", "groups.append([(package+'.' if package else '')+base])\n   if self.sources[sid]['kind'] in ('sdk-signature','source-signature'):\n    groups.append(['java.lang.'+base])")
s=s.replace("dict(header=doc['provenance'][0]['raw_signature'],fields=sorted(shape,key=js))", "dict(kind=doc['kind'],qualified_name=doc.get('owner') or doc['canonical_signature'].split(':',1)[1],fields=sorted(shape,key=js))")
s=s.replace("else 'other' if doc['kind'] in ('package','unsupported') else 'binding'", "else 'other' if doc['kind'] in ('package','unsupported','enum-constant') or (doc['kind']=='field' and (doc['extensions'].get('aidl_declaration_kind') or doc['extensions'].get('enum_constant') or doc['extensions']['language']=='hidl')) else 'binding'")
p.write_text(s)
p=C/'tooling/build_catalog.py';s=p.read_text();old="r['extras']['interface_annotations']=d['annotations']";print('member import anchor',old in s)
# Actual member enrichment occurs immediately before add; carry the file imports.
s=s.replace("for r in d['records']:","for r in d['records']:\n     r['extras']['imports']=d['imports']") if "for r in d['records']:" in s else s
if "r['extras']['imports']=d['imports']" not in s:
 s=s.replace("for r in d['members']:","for r in d['members']:\n     r['extras']['imports']=d['imports']")
p.write_text(s)
p=C/'tooling/dispatch_manifest.py';s=p.read_text().replace("if shape is not None:contract['declared_type_shape']=shape", "if shape is not None:contract['declared_type_shape']={k:v for k,v in shape.items() if k!='header'}")
p.write_text(s)
# An additive point key is a new minor inventory contract, never silently read as2.0.
for p in list((C/'tooling').glob('*.py'))+list((C/'tests').glob('*.py')):
 s=p.read_text()
 if '2.0.0' in s and ('DeviceResolution' in s or 'device_resolution_contract_version' in s or "contract='2.0.0'" in s):p.write_text(s.replace('2.0.0','2.1.0'))
p=C/'contracts/DeviceResolution.schema.json';s=json.loads(p.read_text());s['$id']='urn:android-binding-catalog:2.1.0:DeviceResolution';s['description']='Catalog inventory2.1 adds explicit point evidence: other versions are unknown, never inferred absent. Historical resolved2 definitions cannot activate as DeviceView3.'
s['$defs']['Clause']['properties']['platform_point']={'type':'array','prefixItems':[{'type':'integer','minimum':0,'maximum':2147483647}]*2,'items':False,'minItems':2,'maxItems':2}
p.write_text(json.dumps(s,indent=2)+'\n')
p=C/'contracts/DeviceView.schema.json';v=json.loads(p.read_text());v['$defs']['Clause']=s['$defs']['Clause'];p.write_text(json.dumps(v,indent=2)+'\n')
p=C/'tooling/device_resolution.py';s=p.read_text().replace('import copy,hashlib,json,pathlib','import copy,hashlib,json,pathlib,functools')
s=s.replace('def validate(value,name):\n schema=json.loads((P/', "@functools.lru_cache(maxsize=None)\ndef validator(name):\n schema=json.loads((P/")
s=s.replace("schema['$ref']='#/$defs/'+name;jsonschema.Draft202012Validator(schema).validate(value)\n if name=='Facts':", "schema['$ref']='#/$defs/'+name;return jsonschema.Draft202012Validator(schema)\ndef validate(value,name):\n validator(name).validate(value)\n if name=='Facts':")
s=s.replace(" if 'platform' in rule:results.append", " if 'platform_point' in rule:\n  point=rule['platform_point'];observed=facts['platform'];results.append(('available',['PLATFORM_POINT_MATCH']) if [observed['major'],observed['minor']]==point else ('unknown',['PLATFORM_APPLICABILITY_UNPROVEN']))\n if 'platform' in rule:results.append")
p.write_text(s)
p=C/'tests/test_device_catalog_pipeline.py';s=p.read_text().replace('self.assertGreaterEqual(len(raw),count)','self.assertEqual(len(raw),count)').replace("][\'state\'],\'unavailable\')", "][\'state\'],\'unknown\')")
# Change only the source-point assertion, preserve authoritative index exclusions.
s=p.read_text().replace('self.assertGreaterEqual(len(raw),count)','self.assertEqual(len(raw),count)').replace("if r['entry_id']==member)['state'],'unavailable')","if r['entry_id']==member)['state'],'unknown')")
p.write_text(s)
print('Producer corrections written; membership/storage follow separately')
