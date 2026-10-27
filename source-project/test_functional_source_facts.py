import unittest
from functional_source_facts import taxonomy
class Taxonomy(unittest.TestCase):
 def doc(self,name='android.annotation.SystemService',arguments='"clipboard"'):
  return dict(entry_id='a'*64,owner='android.content.ClipboardManager',package='android.content',provenance=[dict(source_id='s')],extensions=dict(annotations=[dict(name=name,arguments=arguments,raw='source annotation')],imports=[]))
 def test_explicit_service_annotation_retained_without_automatic_group_policy(self):
  row=taxonomy(self.doc())['functional_evidence'][0];self.assertEqual(row['service_string_literal'],'clipboard');self.assertTrue(row['annotation_namespace_proven']);self.assertFalse(row['functional_group_membership_proven'])
 def test_custom_annotation_name_not_borrowed_as_android_annotation(self):
  self.assertFalse(taxonomy(self.doc('custom.SystemService'))['functional_evidence'][0]['annotation_namespace_proven'])
 def test_short_annotation_requires_import(self):
  doc=self.doc('SystemService','Context.CLIPBOARD_SERVICE');self.assertFalse(taxonomy(doc)['functional_evidence'][0]['annotation_namespace_proven']);doc['extensions']['imports']=['android.annotation.SystemService'];self.assertTrue(taxonomy(doc)['functional_evidence'][0]['annotation_namespace_proven'])
 def test_symbol_kept_separate_from_string_literal(self):
  row=taxonomy(self.doc(arguments='value=Context.CLIPBOARD_SERVICE'))['functional_evidence'][0];self.assertIsNone(row['service_string_literal']);self.assertEqual(row['service_symbol_expression'],'Context.CLIPBOARD_SERVICE')
 def test_package_namespace_does_not_become_functional_group(self):
  doc=self.doc();doc['extensions']['annotations']=[];result=taxonomy(doc);self.assertEqual(result['classification'],'unclassified-searchable');self.assertEqual(result['imported_group_memberships'],[]);self.assertFalse(result['namespace_search_evidence']['functional_group_inferred'])
 def test_expression_not_evaluated(self):
  row=taxonomy(self.doc(arguments='selectService()'))['functional_evidence'][0];self.assertIsNone(row['service_string_literal']);self.assertIsNone(row['service_symbol_expression'])
