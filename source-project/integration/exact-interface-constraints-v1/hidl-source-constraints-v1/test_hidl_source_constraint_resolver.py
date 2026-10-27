import unittest,sys,pathlib
P=pathlib.Path(__file__).resolve().parent/'catalog-integrated012-sourcev1'
sys.path[:0]=[str(P/'tooling'),str(P/'tests')]
import device_resolution as dr,test_device_resolution as fixtures
from test_hidl_source_constraint import Constraint
from hidl_source_constraint import exact_constraint
class Resolver(unittest.TestCase):
 def setUp(self):
  f=Constraint();self.result=exact_constraint(f.source(),f.record(),[f.fact()]);self.facts=fixtures.facts();self.descriptor=self.result['clause']['interface']['descriptor']
 def test_schema_accepts_producer_predicate(self):
  dr.validate(dict(alternatives=[self.result['clause']],evidence_ids=['fixture-declaration','fixture-published-hash']),'Rule')
 def test_no_runtime_observation_remains_unknown(self):
  self.facts['interfaces']={};self.assertEqual(dr.clause(self.result['clause'],self.facts)[0],'unknown')
 def test_exact_observed_interface_hash_satisfies_source_predicate(self):
  self.facts['interfaces']={self.descriptor:dict(state='reachable',authoritative=True,version=None,hash='a'*64)}
  self.assertEqual(dr.clause(self.result['clause'],self.facts)[0],'available')
 def test_hash_mismatch_remains_unknown_not_invented_absence(self):
  self.facts['interfaces']={self.descriptor:dict(state='reachable',authoritative=True,version=None,hash='c'*64)}
  self.assertEqual(dr.clause(self.result['clause'],self.facts)[0],'unknown')
 def test_untrusted_reachability_never_activates(self):
  self.facts['interfaces']={self.descriptor:dict(state='reachable',authoritative=False,version=None,hash='a'*64)}
  self.assertEqual(dr.clause(self.result['clause'],self.facts)[0],'unknown')
