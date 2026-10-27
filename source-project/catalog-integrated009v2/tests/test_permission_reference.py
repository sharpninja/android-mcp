import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from permission_reference import parse
def field(name,value='',attrs='data-version-added="37.2"'):
 return f'<div {attrs}><h3 class="api-name" id="{name}">{name}</h3><div>Added in 37.2</div><pre class="api-signature">public static final <a>String</a> {name}</pre><p>Protection level: internal|role</p>{value}</div>'
class PermissionReferenceFacts(unittest.TestCase):
 def test_literals_and_major_minor_facts_not_guessed(self):
  rows=parse(field('X','<p>Constant Value: &quot;other.permission.X&quot;</p>'))['fields']
  self.assertEqual(rows[0]['value'],'other.permission.X');self.assertEqual(rows[0]['introduced_raw'],'37.2');self.assertEqual(rows[0]['protection_level_raw'],'internal|role')
 def test_missing_value_and_deprecated_are_not_permission_absence_or_removal(self):
  row=parse(field('X',attrs='data-version-added="1" data-version-deprecated="16"'))['fields'][0]
  self.assertIsNone(row['value']);self.assertEqual(row['deprecated_raw'],'16');self.assertIsNone(row['removed_raw'])
 def test_duplicate_names_remain_evidence_and_are_flagged(self):
  result=parse(field('X','Constant Value: "a.permission.X"')+field('X','Constant Value: "b.permission.X"'))
  self.assertEqual(len(result['fields']),2);self.assertEqual(result['duplicate_field_names'],['X'])
 def test_script_text_cannot_create_a_documented_field(self):
  result=parse('<script>'+field('FAKE','Constant Value: "a.permission.FAKE"')+'</script>'+field('REAL','Constant Value: "a.permission.REAL"'))
  self.assertEqual([f['name'] for f in result['fields']],['REAL'])
