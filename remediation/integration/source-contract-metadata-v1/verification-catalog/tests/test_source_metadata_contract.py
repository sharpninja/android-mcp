"""New local metadata contracts: source evidence never supplies runtime authority."""
import unittest,pathlib,sys,json,copy
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))

class SourceMetadataContracts(unittest.TestCase):
 def module(self):
  import source_metadata_reference
  return source_metadata_reference
 def doc(self):
  return dict(entry_id='a'*64,kind='interface',name='Listener',owner='p.Listener',package='p',canonical_signature='java:p.Listener',arguments=[],permission={'op':'unknown','reason':'fixture'},provenance=[],extensions={'language':'java','annotations':[]})
 def source(self):return dict(repository='fixture',revision='r1',path='IFoo.aidl',sha256='b'*64)
 def test_generated_schema_is_available(self):self.assertTrue((P/'contracts/SourceContractMetadata.schema.json').exists())
 def test_all_unclassified_entries_remain_unknown_and_searchable(self):
  m=self.module();doc=self.doc();doc['name']='Ordinary';v=m.produce_entry_metadata(doc)
  self.assertEqual(v['callback_facts']['type_disposition']['role'],'unknown');self.assertEqual(v['functional_facts']['classification'],'unclassified-searchable')
 def test_named_listener_is_candidate_never_registrable(self):
  m=self.module();v=m.produce_entry_metadata(self.doc());self.assertEqual(v['callback_facts']['type_disposition']['role'],'candidate');self.assertFalse(m.callback_registration_allowed(v))
 def test_producer_dto_retains_selector_and_ordered_hash_candidates(self):
  m=self.module();from interface_applicability_evidence import produce
  s=self.source();build=dict(s,path='Android.bp');link=dict(build_evidence=build,build_module_name='foo',module_kind='aidl_interface',declared_flags={'apex_available':['//apex_available:platform']},published_hash_lines=[dict(source=dict(s,path='.hash'),line=1,literal='d'*40,format='published-aidl-hash-hex40'),dict(source=dict(s,path='.hash'),line=2,literal='e'*40,format='published-aidl-hash-hex40')])
  facts=produce(s,link);v=m.produce_entry_metadata(self.doc(),[facts]);self.assertEqual(v['interface_evidence'][0],facts);self.assertIsNone(facts['module_candidates'][0]['version_range'])
 def test_source_metadata_cannot_claim_runtime_or_client_authority(self):
  m=self.module();v=m.produce_entry_metadata(self.doc());v['runtime_observation']=True
  with self.assertRaises(ValueError):m.validate_metadata(v)
 def test_entry_identity_mismatch_rejected_before_attach(self):
  m=self.module();doc=self.doc();v=m.produce_entry_metadata(doc);v['entry_id']='f'*64
  with self.assertRaisesRegex(ValueError,'identity'):m.attach(doc,v)
 def test_attach_preserves_inputs_and_uses_one_canonical_extension(self):
  m=self.module();doc=self.doc();old=copy.deepcopy(doc);v=m.produce_entry_metadata(doc);out=m.attach(doc,v)
  self.assertEqual(doc,old);self.assertEqual(out['extensions']['source_contract_metadata']['variants'],[v])
 def test_aggregate_metadata_does_not_borrow_oneway_from_another_version(self):
  m=self.module();doc=self.doc();doc.update(kind='method',name='changed');doc['extensions'].update(oneway=True,language='aidl')
  value=m.produce_entry_metadata(doc);self.assertEqual(value['callback_facts']['endpoint_method']['reply_mode'],'unknown')
  exact=m.produce_entry_metadata(doc,source_context=dict(kind='exact-occurrence',source_ids=['b'*64],declaration_record_hash='c'*64,unknown_reason=None));self.assertEqual(exact['callback_facts']['endpoint_method']['reply_mode'],'oneway')
 def test_duplicate_source_variant_context_cannot_be_flattened_into_one_entry(self):
  m=self.module();doc=self.doc();v=m.produce_entry_metadata(doc)
  with self.assertRaisesRegex(ValueError,'Duplicate'):m.attach(doc,dict(schema_version='1.0.0',entry_id=doc['entry_id'],variants=[v,v]))
 def test_unknown_fields_are_rejected_in_known_contract(self):
  m=self.module();v=m.produce_entry_metadata(self.doc());v['grant_all']=True
  with self.assertRaises(ValueError):m.validate_metadata(v)
 def test_native_evidence_identity_is_not_a_binding_id(self):
  m=self.module();record=dict(evidence_record_id='c'*64,identity_scope='source-file-local ABI evidence, not a stable MCP binding ID',permission_authority='unknown',record={'collection':'pointer_types','display_name':'fixture pointer','local_key':'p','ordinal':0,'metadata':{}},runtime_applicability='unknown',source=dict(self.source(),source_id='d'*64),source_registry_id='e'*64,wire_codec_proven=False)
  result=m.native_evidence('record',record);self.assertEqual(result['confidence'],'structural-only');self.assertFalse(result['runtime_observation'])
  record['wire_codec_proven']=True
  with self.assertRaises(ValueError):m.native_evidence('record',record)
 def test_materialization_does_not_auto_subclass_abstract_types(self):
  m=self.module();self.assertEqual(m.materialization('abstract-class'),{'strategy':'unsupported_abstract_class','unknown_reason':'ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS'})
  with self.assertRaises(ValueError):m.materialization('abstract-class',{'adapter_id':'fake'})
 def test_unknown_hidl_source_constraint_is_retained_without_positive_clause(self):
  m=self.module();row=dict(source=self.source(),declared_descriptor='p@1.0::IFoo',constraint={'unknown_reason':'No matching same-revision published source hash'})
  m.validate(row,'HidlConstraint')
  row['constraint']['clause']={'interface':{'descriptor':'p@1.0::IFoo'}}
  with self.assertRaises(ValueError):m.validate(row,'HidlConstraint')
 def test_null_source_parameter_direction_means_unresolved_not_input(self):
  m=self.module();doc=self.doc();doc.update(kind='method',name='registerListener',arguments=[dict(status='unknown',text='Listener',reason='fixture',codec_status='unknown')]);doc['extensions']['parameters']=[dict(name='listener',direction=None)]
  value=m.produce_entry_metadata(doc);candidate=value['callback_facts']['parameter_candidates'][0];self.assertIsNone(candidate['direction']);self.assertFalse(candidate['registration_guaranteed'])
 def test_reply_mode_cannot_invent_synchronous_from_missing_owner_flag(self):
  m=self.module();doc=self.doc();doc.update(kind='method');doc['extensions'].update(language='aidl',oneway=False)
  from callback_source_facts import endpoint_method
  endpoint=endpoint_method(doc);endpoint['reply_mode']='synchronous'
  with self.assertRaisesRegex(ValueError,'reply mode'):m.validate(endpoint,'EndpointMethod')
 def test_computed_native_descriptor_keeps_unresolved_expression(self):
  m=self.module();row=dict(source=self.source(),source_registry_id='d'*64,fact=dict(family='AIBinder_Class_define',line=1,character_span=[0,40],declared_symbol_token=None,descriptor_expression='chooseDescriptor()',descriptor_literal=None,status='computed-or-unresolved-argument',raw='AIBinder_Class_define(chooseDescriptor())',compiled_endpoint_proven=False,wire_transaction_schema=None,preprocessor_or_build_conditions='unresolved',runtime_registration='unknown'))
  value=m.native_evidence('descriptor-candidate',row);self.assertEqual(value['document'],row)
  row['fact']['compiled_endpoint_proven']=True
  with self.assertRaises(ValueError):m.native_evidence('descriptor-candidate',row)
 def test_immutable_builder_occurrences_attach_before_effective_record_hash(self):
  import tempfile,sqlite3,contextlib
  from test_build import BuildTests
  import build_catalog as builder,jsonschema
  m=self.module()
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);builder.build(BuildTests().fixture(root),root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    bid,raw=db.execute('SELECT entry_id,document_json FROM effective_entries ORDER BY entry_id LIMIT 1').fetchone()
    occurrences=db.execute('SELECT source_id,document_json,record_sha256 FROM binding_declarations WHERE binding_id=? ORDER BY source_id,line',(bid,)).fetchall()
   entry=json.loads(raw);out=m.attach_occurrences(entry,occurrences)
   jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text())).validate(out)
   self.assertEqual(len(out['extensions']['source_contract_metadata']['variants']),len(occurrences))
   unextended=copy.deepcopy(entry);unextended['extensions'].pop('source_contract_metadata',None)
   self.assertNotEqual(m.digest(unextended),m.digest(out));self.assertEqual(m.attach_occurrences(out,occurrences),out)
   broken=list(occurrences);broken[0]=(broken[0][0],broken[0][1]+' ',broken[0][2])
   with self.assertRaisesRegex(ValueError,'hash'):m.attach_occurrences(entry,broken)

if __name__=='__main__':unittest.main()
