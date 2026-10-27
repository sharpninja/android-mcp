import unittest,copy
from aidl_source_constraint import exact_constraint,stable_module
class Constraint(unittest.TestCase):
 def fixture(self):
  source=dict(repository='r',revision='c',path='aidl_api/foo/1/p/I.aidl',sha256='a'*64)
  declaration=dict(declaration_kind='interface',descriptor='p.I',parse_status='methods-parsed',annotations=[])
  audit=dict(repository='r',revision='c',source_inputs=[source],hash_source=dict(repository='r',revision='c',path='aidl_api/foo/1/.hash',sha256='b'*64),result=dict(verified=True,computed_hash='c'*40,version_literal='1'))
  module=dict(kind='aidl_interface',source=dict(repository='r',revision='c',path='Android.bp',sha256='d'*64),properties=dict(kind='object',complete=True,items=[]))
  return source,declaration,audit,'c'*40+'\n',module,dict(status='frozen-dump-declared-version-linked')
 def test_exact_frozen_hash_forms_interface_only_predicate(self):
  value=exact_constraint(*self.fixture());self.assertEqual(value['clause']['interface'],dict(descriptor='p.I',minimum_version=None,accepted_hashes=['c'*40]));self.assertFalse(value['runtime_observation'])
 def test_unstable_or_unresolved_build_flag_rejected(self):
  for node in (dict(kind='literal',value=True),dict(kind='unresolved-expression')):
   args=list(self.fixture());args[4]['properties']['items']=[dict(name='unstable',value=node)];self.assertIn('unknown_reason',exact_constraint(*args))
 def test_backend_physical_last_line_must_match(self):
  args=list(self.fixture());args[3]+='\n';self.assertIn('unknown_reason',exact_constraint(*args))
 def test_other_source_bytes_not_borrowed(self):
  args=list(self.fixture());args[0]=dict(args[0],sha256='e'*64);self.assertIn('unknown_reason',exact_constraint(*args))
 def test_descriptor_override_remains_unknown(self):
  args=list(self.fixture());args[1]['annotations']=[dict(name='Descriptor')];self.assertIn('unknown_reason',exact_constraint(*args))
 def test_current_dump_does_not_borrow_frozen_version(self):
  args=list(self.fixture());args[5]['status']='development-dump-linked-version-unknown';self.assertIn('unknown_reason',exact_constraint(*args))
 def test_stable_absent_default_differs_from_unknown_expression(self):
  self.assertTrue(stable_module(self.fixture()[4]))
  args=list(self.fixture());args[4]['properties']['complete']=False;self.assertFalse(stable_module(args[4]))
