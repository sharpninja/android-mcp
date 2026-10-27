"""Lossless structured facts from public ABI-dump JSON, not Binder wire codecs.
Architecture/library/version labels remain source context, not OS availability.
"""
import json,collections
TYPE_COLLECTIONS={'array_types','builtin_types','enum_types','function_types','lvalue_reference_types','pointer_types','qualified_types','record_types','rvalue_reference_types'}
COLLECTIONS=TYPE_COLLECTIONS|{'functions','global_vars','elf_functions','elf_objects'}
def parse(text):
 def pairs(items):
  result={}
  for key,value in items:
   if key in result:raise ValueError('Duplicate JSON key '+key)
   result[key]=value
  return result
 def constant(value):raise ValueError('Non-finite JSON token '+value)
 try:root=json.loads(text,object_pairs_hook=pairs,parse_constant=constant)
 except ValueError as error:return dict(status='unsupported-json',reason=str(error),raw=text,wire_or_runtime_proven=False)
 if not isinstance(root,dict):return dict(status='unsupported-json',reason='Expected ABI dump object',raw=text,wire_or_runtime_proven=False)
 records=[];types=collections.defaultdict(list);unknown={}
 for collection,items in root.items():
  if collection not in COLLECTIONS or not isinstance(items,list):unknown[collection]=items;continue
  for ordinal,item in enumerate(items):
   if not isinstance(item,dict):unknown.setdefault(collection+':nonobject-records',[]).append(dict(ordinal=ordinal,raw=item));continue
   key=item.get('linker_set_key');record=dict(collection=collection,ordinal=ordinal,local_key=key if isinstance(key,str) else None,display_name=item.get('function_name',item.get('name')),metadata=item)
   records.append(record)
   if collection in TYPE_COLLECTIONS and isinstance(key,str):types[key].append(record)
 duplicate=sorted(key for key,values in types.items() if len(values)>1);references=[];ref_counts=collections.Counter()
 def refs(value,path):
  if isinstance(value,dict):
   for key,target in value.items():
    if key in ('referenced_type','return_type','underlying_type') and isinstance(target,str):
     matches=types.get(target,[]);status='resolved' if len(matches)==1 else 'ambiguous' if matches else 'unresolved';ref_counts[status]+=1;references.append(dict(path=path+[key],target_local_key=target,status=status))
    else:refs(target,path+[key])
  elif isinstance(value,list):
   for i,item in enumerate(value):refs(item,path+[i])
 for record in records:refs(record['metadata'],[record['collection'],record['ordinal']])
 def function_target(key):
  seen=set();chain=[]
  while isinstance(key,str) and key not in seen:
   seen.add(key);entries=types.get(key,[])
   if len(entries)!=1:return None
   entry=entries[0];chain.append(dict(collection=entry['collection'],local_key=key))
   if entry['collection']=='function_types':return dict(function_type_key=key,chain=chain,signature=entry['metadata'])
   if entry['collection'] not in ('pointer_types','qualified_types','lvalue_reference_types','rvalue_reference_types'):return None
   key=entry['metadata'].get('referenced_type')
  return None
 callbacks=[]
 for record in records:
  if record['collection']!='functions':continue
  parameters=record['metadata'].get('parameters',[])
  if not isinstance(parameters,list):continue
  for position,parameter in enumerate(parameters):
   if not isinstance(parameter,dict):continue
   target=function_target(parameter.get('referenced_type'))
   if target:
    callbacks.append(dict(function_local_key=record['local_key'],function_name=record['display_name'],parameter_index=position,parameter_metadata=parameter,**target,role='structural-function-pointer-parameter-candidate',lifecycle_proven=False,registration_proven=False,streaming_semantics='unknown',direction='unknown',permission_metadata='unknown; absence is not permission-free'))
 return dict(status='parsed-source-abi-json',records=records,references=references,reference_counts=dict(ref_counts),duplicate_type_keys=duplicate,unknown_root_fields=unknown,callback_parameter_candidates=callbacks,wire_or_runtime_proven=False,permission_authority='unknown',compiled_or_installed_version_range=None)
