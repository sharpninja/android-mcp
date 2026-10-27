import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tooling/dispatch_manifest.py';s=p.read_text().replace('def contract_digest(entry):', '''def wire_annotations(values):
    # Known type/wire semantics only; permission/lifecycle/proof metadata is separate.
    names={'nullable','Nullable','NonNull','NotNull','utf8InCpp','FixedSize','Backing','VintfStability','JavaOnlyStableParcelable'}
    return sorted([dict(name=v['name'],arguments=v.get('arguments','')) for v in values if v.get('name','').rsplit('.',1)[-1] in names],key=lambda v:(v['name'],v['arguments']))

def contract_digest(entry):''').replace('Version2 semantic contract','Version3 semantic contract').replace('contract_format_version=2','contract_format_version=3')
s=s.replace("    shape=entry.get('extensions',{}).get('declared_type_shape')", "    e=entry.get('extensions',{})\n    contract['parameter_semantics']=[dict(direction=p.get('direction'),wire_annotations=wire_annotations(p.get('all_annotations',p.get('annotations',[])))) for p in e.get('parameters',[])]\n    contract['wire_annotations']=wire_annotations(e.get('annotations',[]))\n    contract['oneway']=e.get('oneway',e.get('interface_oneway'))\n    contract['hidl_results']=[dict(name=p.get('name'),type=p.get('type'),wire_annotations=wire_annotations(p.get('all_annotations',p.get('annotations',[])))) for p in e.get('results',[])]\n    shape=e.get('declared_type_shape')")
s=s.replace("    if shape is not None:contract['declared_type_shape']={k:v for k,v in shape.items() if k!='header'}", "    if shape is not None:\n        normalized={k:v for k,v in shape.items() if k not in ('header','fields','supertypes')}\n        normalized['fields']=[dict(v,type=type_ref(v.get('type'))) for v in shape.get('fields',[])]\n        normalized['supertypes']=[dict(v,type=type_ref(v.get('type'))) for v in shape.get('supertypes',[])]\n        contract['declared_type_shape']=normalized")
p.write_text(s)
p=C/'tooling/catalog_projection.py';s=p.read_text().replace('from dispatch_manifest import contract_digest','from dispatch_manifest import contract_digest,wire_annotations')
anchor='class Projection:'
helper='''def structural_header(doc):
 e=doc['extensions'];supers=[];parameters=e.get('type_parameters');header=e.get('type_header','')
 if header:
  match=re.search(r'(?:\\b(?:class|interface|enum)|@interface)\\s+[\\w.$]+(.*)',header)
  tail=match.group(1).strip().rstrip('{').strip() if match else ''
  if tail.startswith('<'):
   depth=0
   for i,c in enumerate(tail):
    if c=='<':depth+=1
    elif c=='>':
     depth-=1
     if depth==0:parameters=tail[:i+1];tail=tail[i+1:];break
  for match in re.finditer(r'\\b(extends|implements)\\s+(.+?)(?=\\b(?:extends|implements)\\b|$)',tail):
   from parsers import split_top
   supers.extend(dict(relation=match[1],text=value.strip()) for value in split_top(match[2]) if value.strip())
 for name,relation in (('superclass','extends'),('interfaces','implements')):
  text=e.get(name)
  if text:
   from parsers import split_top
   text=re.sub(r'^\\s*(?:extends|implements)\\s+','',text)
   supers.extend(dict(relation=relation,text=value.strip()) for value in split_top(text) if value.strip())
 for value in e.get('supertypes',[]):
  if value.get('name'):supers.append(dict(relation=value.get('relation','unknown'),text=value['name'].replace('/','.').replace('$','.')))
 if e.get('declared_extends'):supers.append(dict(relation='hidl-declared-extends',text=e['declared_extends']))
 normalized={js(v):v for v in supers}
 return parameters,[normalized[k] for k in sorted(normalized)]

'''
s=s.replace(anchor,helper+anchor)
s=s.replace("constant=field['extensions'].get('constant_raw'))", "constant=field['extensions'].get('constant_raw'),wire_annotations=wire_annotations(field['extensions'].get('annotations',[])))")
s=s.replace("   doc['extensions']['declared_type_shape']=dict(kind=doc['kind'],qualified_name=doc.get('owner') or doc['canonical_signature'].split(':',1)[1],fields=sorted(shape,key=js))", "   parameters,supers=structural_header(doc)\n   super_refs=[dict(relation=v['relation'],type=self.type_ref(dict(text=v['text'],status='unknown',codec_status='unknown'),doc,sid)) for v in supers]\n   doc['extensions']['declared_type_shape']=dict(kind=doc['kind'],qualified_name=doc.get('owner') or doc['canonical_signature'].split(':',1)[1],type_parameters=parameters,supertypes=super_refs,wire_annotations=wire_annotations(doc['extensions'].get('annotations',[])),fields=sorted(shape,key=js))")
s=s.replace("refs.extend(v['type'] for v in doc['extensions'].get('declared_type_shape',{}).get('fields',[]) if v['type'])", "refs.extend(v['type'] for field in ('fields','supertypes') for v in doc['extensions'].get('declared_type_shape',{}).get(field,[]) if v['type'])")
p.write_text(s)
p=C/'tooling/effective_device_inventory.py';s=p.read_text().replace("refs.extend(field['type'] for field in doc['extensions'].get('declared_type_shape',{}).get('fields',[]) if field.get('type'))", "refs.extend(field['type'] for name in ('fields','supertypes') for field in doc['extensions'].get('declared_type_shape',{}).get(name,[]) if field.get('type'))");p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text().replace('contract format2','contract format3');s+='\nSemantic contract format3 normalizes structured field/supertype references without their proof prose/source revision metadata, retains type parameters and declared supertypes, and includes parameter directions, known wire type annotations, oneway metadata and HIDL result types. Permission/lifecycle provenance remains separate. All hashes are versioned inputs to closure3; format2 grants/codecs are never silently migrated to new schema hashes. Unknown type/import/compiler/ABI semantics still prevent typed invocation.\n';p.write_text(s)
print('Contract3 separates provenance from semantic type/wire identity; normalized inheritance participates in dependencies')
