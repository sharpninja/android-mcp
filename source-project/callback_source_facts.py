"""Callback candidates and structural endpoint facts; no runtime setup inference."""
import re,copy,hashlib,json
NAME=re.compile(r'(?:Callback|Listener|Observer|DeathRecipient|OutcomeReceiver|ResultReceiver|Runnable|Callable|Consumer|Function|Predicate|Continuation)(?:$|[<$])',re.I)
def named(value):return isinstance(value,str) and NAME.search(value) is not None
def type_candidate(doc):
 name=doc.get('name','');language=doc.get('extensions',{}).get('language');kind=doc.get('kind')
 annotation=any(v.get('name','').rsplit('.',1)[-1]=='FunctionalInterface' for v in doc.get('extensions',{}).get('annotations',[]))
 reasons=[]
 if named(name):reasons.append('callback-like declared type name')
 if annotation:reasons.append('FunctionalInterface annotation retained; invocation role not established')
 if language in ('aidl','hidl') and kind in ('aidl-interface','interface'):reasons.append('Binder/HIDL interface can be passed as an endpoint; callback direction unproven')
 return dict(is_candidate=bool(reasons),role='candidate' if reasons else 'unknown',reasons=reasons,
  callback_lifecycle_proven=False,inheritance=copy.deepcopy(doc.get('extensions',{}).get('declared_type_shape',{}).get('supertypes',[])))
def endpoint_method(doc,owner_doc=None):
 e=doc.get('extensions',{});owner=owner_doc.get('extensions',{}) if owner_doc else {}
 own=e.get('oneway');inherited=e.get('interface_oneway',owner.get('oneway'))
 language=e.get('language')
 async_mode=(True if own is True or inherited is True else False if own is False and inherited is False else None) if language=='aidl' else own if language=='hidl' and isinstance(own,bool) else None
 return dict(entry_id=doc['entry_id'],canonical_signature=doc['canonical_signature'],
  parameters=copy.deepcopy(e.get('parameters',[])),arguments=copy.deepcopy(doc.get('arguments',[])),
  return_type=copy.deepcopy(doc.get('return_type')),hidl_results=copy.deepcopy(e.get('results',[])),
  source_language=language,explicit_oneway=own,inherited_interface_oneway=inherited,
  reply_mode='oneway' if async_mode is True else 'synchronous' if async_mode is False else 'unknown',
  permission=copy.deepcopy(doc.get('permission')),provenance=copy.deepcopy(doc.get('provenance',[])),
  version_evidence=copy.deepcopy(e.get('device_resolution_variants',[])),runtime_authority_proven=False)
def registration_candidates(doc,resolved_types):
 result=[];parameters=doc.get('extensions',{}).get('parameters',[])
 for index,arg in enumerate(doc.get('arguments',[])):
  if arg.get('status')=='primitive':continue
  param=parameters[index] if index<len(parameters) else {};target=resolved_types.get(arg.get('resolved_binding_id'))
  classification=type_candidate(target) if target else None
  spelled=named(arg.get('text')) or named(param.get('name'))
  if not spelled and not (classification and classification['is_candidate']):continue
  name=doc.get('name','');role='unregistration-candidate' if re.match(r'^(?:unregister|remove|clear|unset|unsubscribe|cancel)',name,re.I) else 'registration-candidate' if re.match(r'^(?:register|add|set|subscribe|request|start|bind)',name,re.I) else 'callback-parameter-candidate'
  key=dict(binding_id=doc['entry_id'],parameter_index=index,type_id=arg.get('resolved_binding_id'),type_spelling=arg.get('text'))
  result.append(dict(candidate_id=hashlib.sha256(json.dumps(key,sort_keys=True,separators=(',',':')).encode()).hexdigest(),
   **key,owner=doc.get('owner'),language=doc.get('extensions',{}).get('language'),
   parameter_name=param.get('name'),direction=param.get('direction','unknown'),parameter_annotations=copy.deepcopy(param.get('all_annotations',param.get('annotations',[]))),
   role=role,confidence='source-structure-plus-role-heuristic' if target else 'name-heuristic-unresolved-type',
   evidence_reasons=(classification['reasons'] if classification else [])+(['callback-like parameter/type spelling'] if spelled else []),
   lifecycle_proven=False,registration_guaranteed=False,matching_unregistration_guaranteed=False,
   permission=copy.deepcopy(doc.get('permission')),provenance=copy.deepcopy(doc.get('provenance',[])),
   applicability=copy.deepcopy(doc.get('extensions',{}).get('device_resolution_variants',[])),
   unresolved=['Callback invocation direction, registration effects, executor/threading, lifetime, cancellation, ordering, buffering and backpressure require contract evidence']))
 return result
def possible_pairs(rows):
 pairs=[]
 for registration in rows:
  if registration['role']!='registration-candidate':continue
  for removal in rows:
   if removal['role']!='unregistration-candidate':continue
   if registration['owner']==removal['owner'] and registration['language']==removal['language'] and registration['type_spelling']==removal['type_spelling'] and registration['type_id']==removal['type_id']:
    pairs.append(dict(registration_candidate_id=registration['candidate_id'],unregistration_candidate_id=removal['candidate_id'],status='candidate',basis='same declared owner/language/callback parameter type',lifecycle_pair_proven=False))
 return pairs
