"""Offline validation/normalization of source DTOs. No Android or HTTP service."""
import pathlib,json,copy,hashlib
import jsonschema
from callback_source_facts import type_candidate,endpoint_method,registration_candidates
from functional_source_facts import taxonomy
P=pathlib.Path(__file__).resolve().parents[1]
SCHEMA=json.loads((P/'contracts/SourceContractMetadata.schema.json').read_text())
_VALIDATORS={}
def validator(root):
 if root not in _VALIDATORS:
  schema=copy.deepcopy(SCHEMA);schema['$ref']='#/$defs/Meta'+root
  jsonschema.Draft202012Validator.check_schema(schema);_VALIDATORS[root]=jsonschema.Draft202012Validator(schema)
 return _VALIDATORS[root]
def validate(value,root):
 errors=list(validator(root).iter_errors(value))
 if errors:raise ValueError('Invalid '+root+' at '+repr(list(errors[0].absolute_path))+': '+errors[0].message)
 if root=='EndpointMethod':
  language=value['source_language'];own=value['explicit_oneway'];inherited=value['inherited_interface_oneway']
  asynchronous=(True if own is True or inherited is True else False if own is False and inherited is False else None) if language=='aidl' else own if language=='hidl' and isinstance(own,bool) else None
  expected='oneway' if asynchronous is True else 'synchronous' if asynchronous is False else 'unknown'
  if value['reply_mode']!=expected:raise ValueError('Endpoint reply mode contradicts its source-language/oneway evidence')
 return value
def digest(value):
 return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode()).hexdigest()
def validate_metadata(value):
 validate(value,'Envelope')
 callback=value['callback_facts'];kind=callback['type_disposition']
 if kind['is_candidate']!=(kind['role']=='candidate'):raise ValueError('Callback candidate flag and disposition disagree')
 endpoint=callback['endpoint_method']
 if endpoint is not None:validate(endpoint,'EndpointMethod')
 if endpoint is not None and endpoint['entry_id']!=value['entry_id']:raise ValueError('Endpoint identity differs from metadata identity')
 if value['functional_facts']['entry_id']!=value['entry_id']:raise ValueError('Functional identity differs from metadata identity')
 for row in callback['parameter_candidates']:
  if row['binding_id']!=value['entry_id']:raise ValueError('Parameter binding identity differs')
 for row in value['interface_evidence']:
  source=row['source']
  evidence=([row['declared_module']['source']] if row['declared_module'] else [])+[v['source'] for v in row['interface_candidates']]+[v['source'] for v in row['module_candidates']]
  if any(any(v[k]!=source[k] for k in ('repository','revision')) for v in evidence):raise ValueError('Cross-repository/revision source evidence')
 for row in value['hidl_source_constraints']:
  c=row['constraint']
  if 'unknown_reason' in c:continue
  interface=c['clause']['interface'];source=row['source']
  if row['declared_descriptor']!=interface['descriptor'] or interface['accepted_hashes']!=[source['sha256']]:raise ValueError('HIDL descriptor/source bytes mismatch')
  if not c['source_evidence'] or any(any(v[k]!=source[k] for k in ('repository','revision')) for v in c['source_evidence']):raise ValueError('HIDL evidence scope mismatch')
 return value
def produce_entry_metadata(doc,interface_evidence=(),hidl_source_constraints=(),native_abi_evidence=(),resolved_types=None,lifecycle_pairs=(),source_context=None,aidl_source_constraints=()):
 if not isinstance(doc.get('entry_id'),str):raise ValueError('Missing Entry identity')
 facts=type_candidate(doc)
 result=dict(schema_version='1.0.0',entry_id=doc['entry_id'],interface_evidence=copy.deepcopy(list(interface_evidence)),hidl_source_constraints=copy.deepcopy(list(hidl_source_constraints)),callback_facts=dict(type_disposition=facts,endpoint_method=endpoint_method(doc) if doc.get('kind') in ('method','aidl-member','constructor') else None,parameter_candidates=registration_candidates(doc,resolved_types or {}),lifecycle_pair_candidates=copy.deepcopy(list(lifecycle_pairs))),functional_facts=taxonomy(doc),native_abi_evidence=copy.deepcopy(list(native_abi_evidence)),runtime_observation=False,wire_codec_proven=False,authority_granted=False)
 result['source_context']=copy.deepcopy(source_context) if source_context is not None else dict(kind='aggregate-unresolved',source_ids=sorted({p['source_id'] for p in doc.get('provenance',[]) if p.get('source_id')}),declaration_record_hash=None,unknown_reason='Exact binding_declarations occurrence context not supplied; no source-version inheritance/registration inference')
 result['aidl_source_constraints']=copy.deepcopy(list(aidl_source_constraints))
 if result['source_context']['kind']=='aggregate-unresolved':
  result['callback_facts']['type_disposition']['inheritance']=[]
  endpoint=result['callback_facts']['endpoint_method']
  if endpoint is not None:endpoint.update(explicit_oneway=None,inherited_interface_oneway=None,reply_mode='unknown')
 return validate_metadata(result)
def attach(doc,metadata):
 if 'variants' not in metadata:metadata=dict(schema_version='1.0.0',entry_id=metadata.get('entry_id'),variants=[copy.deepcopy(metadata)])
 validate(metadata,'Set')
 if doc.get('entry_id')!=metadata['entry_id']:raise ValueError('Entry identity differs from metadata identity')
 contexts=set()
 for variant in metadata['variants']:
  validate_metadata(variant)
  if variant['entry_id']!=metadata['entry_id']:raise ValueError('Occurrence identity differs from metadata identity')
  key=digest(variant['source_context'])
  if key in contexts:raise ValueError('Duplicate source occurrence context')
  contexts.add(key)
 result=copy.deepcopy(doc);result.setdefault('extensions',{})['source_contract_metadata']=copy.deepcopy(metadata)
 return result
def attach_occurrences(doc,occurrences,evidence_lookup=None):
 """Offline builder hook before effective record hashing; reads immutable rows only.
 Rows are (source_id, exact document_json, record_sha256). No aggregated Entry
 flags are reused. Additional evidence is joined by the trusted importer's lookup.
 """
 variants=[]
 for source_id,raw,record_hash in occurrences:
  if hashlib.sha256(raw.encode()).hexdigest()!=record_hash:raise ValueError('Immutable occurrence record hash mismatch')
  occurrence=json.loads(raw)
  if occurrence.get('entry_id')!=doc.get('entry_id'):raise ValueError('Occurrence binding identity mismatch')
  if source_id not in {p.get('source_id') for p in occurrence.get('provenance',[])}:raise ValueError('Occurrence source identity absent from provenance')
  context=dict(kind='exact-occurrence',source_ids=[source_id],declaration_record_hash=record_hash,unknown_reason=None)
  extras=evidence_lookup(source_id,occurrence) if evidence_lookup else {}
  variants.append(produce_entry_metadata(occurrence,source_context=context,**extras))
 if not variants:variants=[produce_entry_metadata(doc)]
 return attach(doc,dict(schema_version='1.0.0',entry_id=doc['entry_id'],variants=variants))
def native_evidence(kind,document):
 result=dict(schema_version='1.0.0',kind=kind,confidence='structural-only',runtime_observation=False,wire_codec_proven=False,authority_granted=False,document=copy.deepcopy(document))
 return validate(result,'NativeEvidence')
def callback_registration_allowed(metadata):
 # Source metadata is never runtime-admission input; Descriptor plus current verified adapter/policy is separate.
 validate_metadata(metadata);return False
def materialization(shape,adapter=None):
 if adapter is not None:
  required={'adapter_id','adapter_version','adapter_sha256'}
  if set(adapter)!=required or not all(isinstance(adapter[k],str) and adapter[k] for k in required) or len(adapter['adapter_sha256'])!=64:raise ValueError('Reviewed adapter must have exact id/version/hash pin')
  return dict(strategy='prebuilt_adapter',adapter=copy.deepcopy(adapter))
 if shape=='abstract-class':return dict(strategy='unsupported_abstract_class',unknown_reason='ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS')
 if shape in ('class','nonfinal-class'):return dict(strategy='unsupported_class_subclass',unknown_reason='CLASS_CALLBACK_REQUIRES_REVIEWED_ADAPTER')
 if shape=='unknown':return dict(strategy='unknown',unknown_reason='Callback materialization shape is unresolved')
 if shape=='java-interface':return dict(strategy='interface_dynamic_proxy')
 if shape=='aidl-interface':return dict(strategy='binder_generic_stub')
 raise ValueError('Unknown callback materialization shape; no runtime subclassing inferred')
def validate_descriptor(value):
 validate(value,'CallbackDescriptor')
 if value['classification'] in ('candidate','unknown') and not value['unknown_reason']:raise ValueError('Unproven callback needs an explicit reason')
 if value['classification'] in ('proven_callback','proven_non_callback') and not value['evidence']:raise ValueError('Proven role requires source evidence')
 if len({v['method_id'] for v in value['methods']})!=len(value['methods']):raise ValueError('Duplicate callback method identity')
 cleanup=value['cleanup']
 if cleanup['status']=='proven' and (not cleanup['binding_id'] or not cleanup['contract_hash'] or not cleanup['evidence']):raise ValueError('Proven cleanup requires exact binding/contract/evidence')
 if value['materialization']['strategy']=='prebuilt_adapter':materialization('abstract-class',value['materialization'].get('adapter'))
 return value
def validate_group(value,known_groups=None):
 validate(value,'FunctionalGroup')
 members=value['members']
 if len({(v['binding_id'],v['role'],v['lifecycle_unit_id']) for v in members})!=len(members):raise ValueError('Duplicate group binding role/unit')
 hashes={}
 for item in members:
  if item['binding_id'] in hashes and hashes[item['binding_id']]!=item['contract_hash']:raise ValueError('One group cannot select incompatible contracts of one binding')
  hashes[item['binding_id']]=item['contract_hash']
 units={}
 for row in members:
  if row['role'] in ('register','cleanup'):
   if not row['lifecycle_unit_id']:raise ValueError('Lifecycle operation requires an atomic unit')
   units.setdefault(row['lifecycle_unit_id'],set()).add(row['role'])
 if any(roles!={'register','cleanup'} for roles in units.values()):raise ValueError('Registration and cleanup must be one coherent group unit')
 if len(set(value['required_type_ids']))!=len(value['required_type_ids']):raise ValueError('Duplicate type dependency')
 deps=[v['group_id'] for v in value['dependencies']]
 if len(set(deps))!=len(deps) or value['group_id'] in deps:raise ValueError('Duplicate/self group dependency')
 if value['estimated_tokens'] and not value['estimator_version']:raise ValueError('Nonzero token estimate needs estimator version')
 if known_groups is not None:
  path=set();done=set()
  def visit(group):
   key=group['group_id']
   if key in path:raise ValueError('Group dependency cycle')
   if key in done:return
   path.add(key)
   for pin in group['dependencies']:
    target=known_groups.get(pin['group_id'])
    if target is None:raise ValueError('Unknown group dependency')
    if target['revision']!=pin['revision'] or target['content_hash']!=pin['content_hash']:raise ValueError('Group dependency revision/hash mismatch')
    visit(target['document'])
   path.remove(key);done.add(key)
  visit(value)
 return value

