import unittest
from freeze_combined009 import merge_sources

class Merge(unittest.TestCase):
 def source(self,sha='abc',**kw):
  return dict(repository='repo',revision='commit',path='a',sha256=sha,status='downloaded',kind='evidence',scope='source',**kw)
 def test_exact_overlap_preserves_origins_and_raw_scope(self):
  a=self.source();b=self.source();b['scope']='aosp-native-binder-search-candidate-evidence'
  merged=merge_sources([('old',[a]),('new',[b])]);self.assertEqual(len(merged),1)
  self.assertEqual(merged[0]['scope'],b['scope']);self.assertEqual(len(merged[0]['selection_origins']),2)
 def test_conflicting_bytes_rejected(self):
  with self.assertRaises(ValueError):merge_sources([('old',[self.source()]),('new',[self.source('def')])])
 def test_revision_is_part_of_identity(self):
  a=self.source();b=self.source();b['revision']='other'
  self.assertEqual(len(merge_sources([('old',[a]),('new',[b])])),2)
 def test_missing_channel_is_explicitly_unknown(self):
  s=self.source();merged=merge_sources([('new',[s])])[0]
  self.assertEqual(merged['channel'],'unknown');self.assertIsNone(merged['api_level']);self.assertIn('not established',merged['acquisition_normalizations'][0])
 def test_declared_channel_preserved(self):
  s=self.source(channel='stable',api_level=37);merged=merge_sources([('new',[s])])[0]
  self.assertEqual((merged['channel'],merged['api_level']),('stable',37))
 def test_boolean_api_level_rejected(self):
  with self.assertRaises(ValueError):merge_sources([('new',[self.source(api_level=True)])])
 def test_previous_origin_evidence_survives_next_freeze(self):
  s=self.source();s['selection_origins']=[dict(inventory='original',scope='source',kind='evidence')]
  result=merge_sources([('next',[s])])[0];self.assertEqual([v['inventory'] for v in result['selection_origins']],['original','next'])
