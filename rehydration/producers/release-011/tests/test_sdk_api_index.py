import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import sdk_api_index as api
class ApiIndexTests(unittest.TestCase):
 def test_minor_lifecycle_and_removed_members(self):
  xml=b'''<api version="4"><class name="a/Outer$Inner" since="37.1"><method name="&lt;init>()V"/><method name="f([Ljava/lang/String;I)J" since="37.2"/><field name="OLD" removed="37.2"/><field name="FUTURE" since="37.3"/></class></api>'''
  result=api.parse(xml,'37.2');rs=result['records']
  self.assertEqual(len(rs),5);self.assertEqual(result['excluded_history_or_future'],2)
  self.assertEqual(sum(r['extras']['lifecycle_status']=='current-at-target' for r in rs),3)
  method=next(r for r in rs if r['name']=='f');self.assertEqual(method['return_type'],'long')
  self.assertEqual([p['type'] for p in method['parameters']],['java.lang.String[]','int'])
  self.assertEqual(method['owner'],'a.Outer.Inner');self.assertEqual(method['extras']['jvm_owner'],'a/Outer$Inner')
 def test_field_type_and_type_kind_are_not_invented(self):
  rows=api.parse(b'<api version="4"><class name="android/Manifest$permission"><field name="CAMERA"/></class></api>','37.2')['records']
  self.assertEqual(rows[0]['kind'],'type');self.assertIsNone(rows[1]['return_type'])
  self.assertEqual(rows[1]['extras']['field_type_status'],'unknown')
 def test_invalid_method_descriptor_retained_unknown(self):
  rows=api.parse(b'<api version="4"><class name="a/C"><method name="broken(wat)"/></class></api>','37.2')['records']
  self.assertEqual(rows[-1]['kind'],'unsupported');self.assertIn('descriptor',rows[-1]['extras']['parse_error'])
