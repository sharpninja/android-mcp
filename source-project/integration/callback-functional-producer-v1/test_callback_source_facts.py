import unittest,copy
from callback_source_facts import type_candidate,endpoint_method,registration_candidates,possible_pairs
class Callback(unittest.TestCase):
 def test_missing_sdk_parameter_name_is_preserved_without_crash(self):
  method=self.method();method['extensions']['parameters'][0]['name']=None
  result=registration_candidates(method,self.types());self.assertIsNone(result[0]['parameter_name'])
 def method(self,name='registerListener'):
  return dict(entry_id='a'*64,canonical_signature='java:a.A#'+name+'(a.Listener)',name=name,owner='a.A',kind='method',arguments=[dict(text='a.Listener',status='resolved',resolved_binding_id='b'*64)],permission=dict(op='unknown'),provenance=[dict(source_id='source')],extensions=dict(language='java',parameters=[dict(name='listener',type='a.Listener')]))
 def types(self):return {'b'*64:dict(name='Listener',kind='interface',extensions=dict(language='java'))}
 def test_name_never_proves_callback_lifecycle(self):
  value=type_candidate(self.types()['b'*64]);self.assertTrue(value['is_candidate']);self.assertFalse(value['callback_lifecycle_proven'])
 def test_parameter_position_and_permission_are_retained(self):
  rows=registration_candidates(self.method(),self.types());self.assertEqual(rows[0]['parameter_index'],0);self.assertEqual(rows[0]['permission'],dict(op='unknown'));self.assertFalse(rows[0]['registration_guaranteed'])
 def test_binder_endpoint_candidate_does_not_require_callback_name(self):
  method=self.method();method['arguments'][0]['text']='a.IEndpoint';method['extensions']['parameters'][0]['name']='endpoint';types=self.types();types['b'*64]=dict(name='IEndpoint',kind='aidl-interface',extensions=dict(language='aidl'))
  self.assertEqual(len(registration_candidates(method,types)),1)
 def test_callback_executor_annotation_alone_is_not_callback_parameter(self):
  method=self.method();method['arguments']=[dict(text='Executor',status='unknown')];method['extensions']['parameters']=[dict(name='executor',annotations=[dict(name='CallbackExecutor')])]
  self.assertEqual(registration_candidates(method,{}),[])
 def test_overloads_and_parameter_positions_have_different_ids(self):
  first=self.method();second=self.method();second['entry_id']='c'*64
  self.assertNotEqual(registration_candidates(first,self.types())[0]['candidate_id'],registration_candidates(second,self.types())[0]['candidate_id'])
 def test_unregistration_pair_remains_candidate(self):
  first=registration_candidates(self.method(),self.types());second=self.method('unregisterListener');second['entry_id']='c'*64;rows=first+registration_candidates(second,self.types());pairs=possible_pairs(rows)
  self.assertEqual(len(pairs),1);self.assertFalse(pairs[0]['lifecycle_pair_proven'])
 def test_inherited_oneway_controls_endpoint_reply_mode(self):
  method=self.method('onEvent');method['extensions']['oneway']=False
  self.assertEqual(endpoint_method(method,dict(extensions=dict(oneway=True)))['reply_mode'],'oneway')
 def test_missing_oneway_not_invented_synchronous(self):
  self.assertEqual(endpoint_method(self.method('onEvent'))['reply_mode'],'unknown')
 def test_same_short_name_does_not_prove_type_resolution(self):
  method=self.method();method['arguments'][0]=dict(text='Listener',status='unknown');item=registration_candidates(method,self.types())[0]
  self.assertIsNone(item['type_id']);self.assertEqual(item['confidence'],'name-heuristic-unresolved-type')
