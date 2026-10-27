import pathlib,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
from parsers import aidl
from aidl_members import enrich
class AidlMembers(unittest.TestCase):
 def parse(self,text):return enrich(text,aidl(text))
 def test_parcelable_fields_annotations_and_values(self):
  result=self.parse('package a; parcelable P {\n @nullable String name;\n int[] values={1,2};\n const String S="a;b";\n}')
  self.assertEqual([m['name'] for m in result['members']],['name','values','S']);self.assertTrue(result['members'][0]['annotations']);self.assertEqual(result['members'][1]['return_type'],'int[]');self.assertEqual(result['members'][2]['extras']['constant_raw'],'"a;b"');self.assertTrue(all(m['extras']['wire_tag'] is None for m in result['members']))
 def test_enum_shifts_and_union_declaration_ordinals_not_tags(self):
  result=self.parse('package a; enum E { A=1<<0, B=1<<1, C, }');self.assertEqual([m['name'] for m in result['members']],['A','B','C']);self.assertEqual(result['members'][1]['extras']['constant_raw'],'1<<1')
  union=self.parse('package a; union U { int x; String y; }');self.assertEqual([m['extras']['declared_ordinal'] for m in union['members']],[0,1]);self.assertTrue(all(m['extras']['wire_tag'] is None for m in union['members']))
 def test_external_parcelable_and_unknown_nested_schema_stay_visible(self):
  self.assertEqual(self.parse('package a; parcelable P cpp_header "P.h";')['members'],[])
  result=self.parse('package a; parcelable P { new_syntax; int good; }');self.assertEqual(result['members'][0]['kind'],'unsupported');self.assertEqual(result['members'][1]['name'],'good');self.assertIn('partial',result['parse_status'])
 def test_interface_constant_is_metadata_and_repeated_lines_are_distinct(self):
  result=self.parse('package a; interface I { const int C=3; void x(); }');self.assertEqual(result['members'][0]['kind'],'field');self.assertEqual(result['members'][0]['name'],'C')
  fields=self.parse('package a; parcelable P {\n int x;\n int y;\n}')['members'];self.assertEqual([m['line'] for m in fields],[2,3])
if __name__=='__main__':unittest.main()
