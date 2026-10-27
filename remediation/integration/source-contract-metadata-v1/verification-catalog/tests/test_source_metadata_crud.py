import unittest,pathlib,sys,sqlite3,copy
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
from source_metadata_store import MetadataCatalog,Error
from source_metadata_reference import validate_group,validate_descriptor

class MetadataCrud(unittest.TestCase):
 def setUp(self):
  self.db=sqlite3.connect(':memory:');self.addCleanup(self.db.close);self.store=MetadataCatalog(self.db)
 def group(self,name='fg:location'):
  return dict(schema_version='1.0.0',group_id=name,title='Location',description='Fixture group, no OS invocation',labels=['location'],members=[],required_type_ids=[],dependencies=[],evidence=[],unknown_reasons=['No source membership established'],estimated_tokens=0,estimator_version=None)
 def create(self,doc=None):
  doc=doc or self.group();return self.store.create('functional-group',doc['group_id'],doc,'fixture owner mapping',owner=True,if_none_match='*')
 def error(self,status,fn):
  with self.assertRaises(Error) as cm:fn()
  self.assertEqual(cm.exception.status,status)
 def test_owner_and_creation_preconditions(self):
  g=self.group();self.error(403,lambda:self.store.create('functional-group',g['group_id'],g,'fixture',if_none_match='*'))
  self.error(428,lambda:self.store.create('functional-group',g['group_id'],g,'fixture',owner=True))
  self.create();self.error(409,lambda:self.create())
 def test_put_cas_and_reader_cursor_invalidation(self):
  old=self.create();cursor=self.store.cursor('reader-A');doc=self.group();doc['description']='new local mapping'
  new=self.store.put('functional-group',doc['group_id'],doc,old['etag'],'fixture update',owner=True)
  self.assertEqual(new['revision'],2);self.assertNotEqual(new['etag'],old['etag']);self.error(412,lambda:self.store.put('functional-group',doc['group_id'],doc,old['etag'],'stale',owner=True));self.error(410,lambda:self.store.continue_page(cursor,'reader-A'))
 def test_cursor_cannot_transfer_to_another_reader(self):
  self.create();cursor=self.store.cursor('reader-A');self.error(410,lambda:self.store.continue_page(cursor,'reader-B'))
 def test_delete_is_tombstone_and_idempotent_with_fresh_cas(self):
  old=self.create();new=self.store.delete('functional-group',old['resource_id'],old['etag'],'fixture delete',owner=True);self.assertEqual(new['state'],'deleted')
  same=self.store.delete('functional-group',new['resource_id'],new['etag'],'fixture retry',owner=True);self.assertEqual(new['etag'],same['etag']);self.assertEqual(len(self.store.history('functional-group',new['resource_id'])),2)
 def test_imported_base_is_immutable_and_local_suppression_does_not_grant(self):
  doc=self.group();row=self.store.import_fixture('functional-group',doc['group_id'],doc,trusted_import=True);self.assertEqual(row['origin'],'imported')
  row=self.store.delete('functional-group',doc['group_id'],row['etag'],'fixture suppress',owner=True);self.assertEqual(row['state'],'suppressed')
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute('DELETE FROM metadata_bases')
 def test_new_import_conflicts_with_overlay_and_requires_explicit_rebase(self):
  doc=self.group();base=self.store.import_fixture('functional-group',doc['group_id'],doc,trusted_import=True);edit=copy.deepcopy(doc);edit['title']='Local';self.store.put('functional-group',doc['group_id'],edit,base['etag'],'fixture overlay',owner=True)
  imported=copy.deepcopy(doc);imported['title']='Next source';conflict=self.store.import_fixture('functional-group',doc['group_id'],imported,trusted_import=True);self.assertEqual(conflict['state'],'conflict');self.assertEqual(conflict['document']['title'],'Local')
  self.error(409,lambda:self.store.put('functional-group',doc['group_id'],edit,conflict['etag'],'no rebase',owner=True))
  fixed=self.store.put('functional-group',doc['group_id'],edit,conflict['etag'],'fixture rebase',owner=True,resolution={'action':'rebase','expected_base_hash':conflict['base_hash']});self.assertEqual(fixed['state'],'active')
 def test_failed_schema_validation_rolls_back_without_generation_change(self):
  row=self.create();generation=self.store.generation();bad=self.group();bad['authority_granted']=True
  self.error(422,lambda:self.store.put('functional-group',bad['group_id'],bad,row['etag'],'invalid',owner=True));self.assertEqual(self.store.generation(),generation);self.assertEqual(self.store.get('functional-group',row['resource_id'])['etag'],row['etag'])
 def test_lifecycle_pair_is_an_atomic_group_unit(self):
  g=self.group();g['members']=[dict(binding_id='a'*64,contract_hash='b'*64,lifecycle_unit_id='u',role='register')]
  with self.assertRaisesRegex(ValueError,'coherent'):validate_group(g)
  g['members'].append(dict(binding_id='c'*64,contract_hash='d'*64,lifecycle_unit_id='u',role='cleanup'));validate_group(g)
 def test_unknown_dependency_rejected_and_does_not_create_partial_group(self):
  g=self.group();g['dependencies']=[dict(group_id='fg:absent',revision=1,content_hash='a'*64)]
  self.error(422,lambda:self.create(g));self.assertIsNone(self.store.row('functional-group',g['group_id']))
 def test_history_and_selected_head_cannot_be_deleted_or_rewritten(self):
  self.create()
  for sql in ['DELETE FROM metadata_revisions','UPDATE metadata_revisions SET reason="changed"','DELETE FROM metadata_heads']:
   with self.assertRaises(sqlite3.IntegrityError):self.db.execute(sql)
 def test_json_null_schema_cannot_pass_sql_check(self):
  import json
  doc=self.group();doc['schema_version']=None
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute('INSERT INTO metadata_revisions VALUES(?,?,?,?,?,?,?,?,?)',('functional-group',doc['group_id'],1,'local','active',None,'a'*64,json.dumps(doc),'fixture'))
 def test_unknown_catalog_reference_cannot_be_saved_as_effective_group(self):
  g=self.group();g['members']=[dict(binding_id='a'*64,contract_hash='b'*64,lifecycle_unit_id=None,role='operation')]
  self.error(422,lambda:self.create(g));self.assertIsNone(self.store.row('functional-group',g['group_id']))
 def test_exact_trusted_contract_pin_is_required_for_group_membership(self):
  self.store.catalog_index={'a'*64:{'kind':'method','contract_hash':'b'*64}}
  g=self.group();g['members']=[dict(binding_id='a'*64,contract_hash='c'*64,lifecycle_unit_id=None,role='operation')]
  self.error(422,lambda:self.create(g));g['members'][0]['contract_hash']='b'*64;self.create(g)
 def test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization(self):
  self.store.catalog_index={'a'*64:{'kind':'class','contract_hash':'b'*64}}
  doc=dict(schema_version='1.0.0',callback_id='c'*64,type_id='a'*64,type_contract_hash='b'*64,classification='candidate',unknown_reason='Role not proven',methods=[],registration_binding_ids=[],materialization={'strategy':'unsupported_abstract_class','unknown_reason':'ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS'},cleanup={'binding_id':None,'contract_hash':None,'status':'unknown','argument_mapping':[],'evidence':[],'unknown_reason':'Cleanup unknown'},evidence=[],source_applicability={'unknown_reason':'No observed device'})
  row=self.store.create('callback-descriptor',doc['callback_id'],doc,'fixture candidate; no runtime registration',owner=True,if_none_match='*')
  self.assertEqual(row['document']['materialization']['strategy'],'unsupported_abstract_class')
 def test_public_representation_matches_local_openapi_component(self):
  import json,jsonschema
  row=self.create();doc=self.store.representation('functional-group',row['resource_id']);api=json.loads((P/'contracts/openapi.json').read_text())
  schema={'$ref':'#/components/schemas/MetadataRepresentation','components':api['components']}
  jsonschema.Draft202012Validator(schema).validate(doc);self.assertNotIn('document_json',doc)
 def test_metadata_openapi_has_only_resolvable_local_references(self):
  import json
  api=json.loads((P/'contracts/openapi.json').read_text())
  def visit(value):
   if isinstance(value,dict):
    if '$ref' in value:
     target=api
     for part in value['$ref'].removeprefix('#/').split('/'):target=target[part]
    for item in value.values():visit(item)
   elif isinstance(value,list):
    for item in value:visit(item)
  visit(api)
  for path in ('callback-descriptors/{callback_id}','functional-groups/{group_id}'):
   self.assertEqual(set(api['paths']['/v1/catalog/'+path]),{'get','put','delete'})

if __name__=='__main__':unittest.main()
