import unittest,pathlib,sys,sqlite3,json,hashlib,contextlib,copy
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from catalog_projection import Projection
from dispatch_manifest import contract_digest
class StructuredSourceScopes(unittest.TestCase):
 def project(self,variants,lang='aidl',kind='aidl-parcelable',extra=None):
  owner='p.Data' if lang=='aidl' else 'p@1.0::Data';bid=hashlib.sha256((lang+':'+owner).encode()).hexdigest();sources={};types={}
  with contextlib.closing(sqlite3.connect(':memory:')) as db:
   db.execute('CREATE TABLE binding_declarations(binding_id TEXT,source_id TEXT,document_json TEXT)')
   for index,variant in enumerate(variants):
    sid='source-'+str(index);fields=variant['fields'];sources[sid]=dict(source_id=sid,repository='fixture',revision='same-pinned-commit',kind='aidl' if lang=='aidl' else 'evidence',api_level=None,channel='unknown',path='aidl_api/module/'+str(index+1)+'/p/Data.aidl');e=dict(language=lang);e.update(extra or {});e.update(variant.get('extra',{}));doc=dict(entry_id=bid,canonical_signature=lang+':'+owner,kind=kind,name='Data',owner=owner,package='p' if lang=='aidl' else 'p@1.0',arguments=[],codec_status='unknown',extensions=e,provenance=[dict(raw_signature=variant.get('header','parcelable Data'),line=1)]);types[sid]=doc;db.execute('INSERT INTO binding_declarations VALUES (?,?,?)',(bid,sid,json.dumps(doc)))
    for order,field in enumerate(fields):
     name,type_text,*constant=field;extras=dict(language=lang,aidl_declaration_kind='field' if lang=='aidl' else None,hidl_declaration_kind='field',source_character_span=[order*20,order*20+10]);extras.update(variant.get('field_extra',{}))
     if constant:extras['constant_expression_raw']=constant[0]
     value=dict(entry_id='field-'+name,canonical_signature=lang+':'+owner+'#'+name,kind='field',name=name,owner=owner,package=doc['package'],arguments=[],return_type=dict(text=type_text,status='unknown',codec_status='unknown'),extensions=extras,provenance=[dict(raw_signature=type_text+' '+name,line=order+2)]);db.execute('INSERT INTO binding_declarations VALUES (?,?,?)',('field-'+name,sid,json.dumps(value)))
   projection=Projection(db,sources);return [(projection.project(doc,sid),contract_digest(projection.project(doc,sid))) for sid,doc in types.items()]
 def test_versioned_source_fields_are_not_combined(self):
  result=self.project([dict(fields=[('x','int')]),dict(fields=[('x','long')])]);self.assertEqual([len(doc['extensions']['declared_type_shape']['fields']) for doc,h in result],[1,1]);self.assertNotEqual(result[0][1],result[1][1])
 def test_hidl_field_type_changes_semantic_digest(self):
  result=self.project([dict(fields=[('x','int32_t')]),dict(fields=[('x','int64_t')])],lang='hidl',kind='type',extra=dict(hidl_declaration_kind='struct'));self.assertNotEqual(result[0][1],result[1][1])
 def test_serialized_field_order_changes_digest(self):
  result=self.project([dict(fields=[('x','int'),('y','long')]),dict(fields=[('y','long'),('x','int')])]);self.assertNotEqual(result[0][1],result[1][1])
 def test_hidl_typedef_target_changes_digest(self):
  result=self.project([dict(fields=[],extra=dict(target_type_raw='int32_t')),dict(fields=[],extra=dict(target_type_raw='int64_t'))],lang='hidl',kind='type',extra=dict(hidl_declaration_kind='typedef'));self.assertNotEqual(result[0][1],result[1][1])
 def test_hidl_enum_storage_type_changes_digest(self):
  result=self.project([dict(fields=[],extra=dict(storage_type_raw=':uint32_t')),dict(fields=[],extra=dict(storage_type_raw=':uint64_t'))],lang='hidl',kind='enum',extra=dict(hidl_declaration_kind='enum'));self.assertNotEqual(result[0][1],result[1][1])
 def test_hidl_enum_expression_changes_digest(self):
  result=self.project([dict(fields=[('A','p@1.0::Data','1')]),dict(fields=[('A','p@1.0::Data','2')])],lang='hidl',kind='enum',extra=dict(hidl_declaration_kind='enum'));self.assertNotEqual(result[0][1],result[1][1])
 def test_raw_header_comments_and_proof_prose_do_not_change_digest(self):
  result=self.project([dict(fields=[('x','int')],header='parcelable Data'),dict(fields=[('x','int')],header='/* comment and no layout change */ parcelable Data')]);self.assertEqual(result[0][1],result[1][1])
