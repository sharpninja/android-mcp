import unittest
from hidl_source_constraint import exact_constraint

class Constraint(unittest.TestCase):
 def source(self):return dict(repository='r',revision='c',path='foo/1.0/IFoo.hal',sha256='a'*64)
 def fact(self):return dict(source=dict(repository='r',revision='c',path='current.txt',sha256='b'*64),declared_identifier='android.hardware.foo@1.0::IFoo',literal='a'*64,format='published-hidl-hash-hex64-and-identifier',line=2)
 def record(self):return dict(kind='interface',canonical='android.hardware.foo@1.0::IFoo')
 def test_exact_published_identifier_and_source_hash_form_interface_clause(self):
  result=exact_constraint(self.source(),self.record(),[self.fact()]);clause=result['clause']['interface']
  self.assertEqual(clause,dict(descriptor='android.hardware.foo@1.0::IFoo',minimum_version=None,accepted_hashes=['a'*64]))
  self.assertEqual(len(result['source_evidence']),2);self.assertFalse(result['runtime_observation'])
 def test_other_hash_cannot_describe_this_source_even_if_identifier_matches(self):
  fact=self.fact();fact['literal']='c'*64
  self.assertIn('unknown_reason',exact_constraint(self.source(),self.record(),[fact]))
 def test_other_repository_cannot_be_borrowed(self):
  fact=self.fact();fact['source']['repository']='other'
  self.assertIn('unknown_reason',exact_constraint(self.source(),self.record(),[fact]))
 def test_types_file_is_not_invented_as_an_interface(self):
  record=self.record();record['kind']='type'
  self.assertIn('unknown_reason',exact_constraint(self.source(),record,[self.fact()]))
 def test_another_declared_identifier_not_used(self):
  record=self.record();record['canonical']='android.hardware.foo@1.0::IBar'
  self.assertIn('unknown_reason',exact_constraint(self.source(),record,[self.fact()]))
