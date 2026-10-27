import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import sdk_api_index as api
class SdkHistoryPreservation(unittest.TestCase):
 def test_removed_owner_and_members_are_inventory_not_current(self):
  raw=b'<api version="4"><class name="a/Old" since="2" removed="30"><method name="m()V" since="3"/></class><class name="a/Now" since="31"/></api>'
  result=api.parse(raw,'37.2',preserve_history=True)
  self.assertEqual(len(result['records']),3)
  self.assertEqual([r['extras']['active_at_target'] for r in result['records']],[False,False,True])
  self.assertEqual(result['excluded_history_or_future'],0)
  self.assertEqual(result['retained_inactive_or_unknown'],2)
  self.assertEqual(len(api.parse(raw,'37.2')['records']),1)
 def test_owner_and_member_extension_constraints_remain_separate(self):
  raw=b'<api version="4"><class name="a/C" since="30" removed="35" sdks="30:4" module="m"><method name="f()V" since="31" removed="36" sdks="31:5"/></class></api>'
  rows=api.parse(raw,'37.2',preserve_history=True)['records']
  self.assertEqual(rows[1]['extras']['api_owner_attributes']['removed'],'35')
  self.assertEqual(rows[1]['extras']['api_index_attributes']['removed'],'36')
  self.assertEqual(rows[1]['extras']['api_owner_attributes']['sdks'],'30:4')
  self.assertEqual(rows[1]['extras']['api_index_attributes']['sdks'],'31:5')
 def test_unknown_lifecycle_not_assumed_current_or_dropped(self):
  row=api.parse(b'<api version="4"><class name="a/C" since="unresolved"/></api>','37.2',preserve_history=True)['records'][0]
  self.assertIsNone(row['extras']['active_at_target'])
  self.assertEqual(row['extras']['api_index_attributes']['since'],'unresolved')
