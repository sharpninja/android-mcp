"""Cross-artifact design regressions from completed020, not Android runtime tests."""
from pathlib import Path
import os,json,re,unittest
P=Path(os.environ.get('DESIGN_CHECK_ROOT',str(Path(__file__).resolve().parent/'design-c4')))
class Design020(unittest.TestCase):
 def setUp(self):
  self.main=(P/'docs/Android MCP Bridge Design.md').read_text();self.model=(P/'architecture/workspace.dsl').read_text();self.code=(P/'architecture/code-diagrams.md').read_text();self.ui=json.loads((P/'docs/ui-asset-manifest.json').read_text())
 def test_arc_authentication_is_explicitly_an_adapter_to_shared_registry(self):
  self.assertTrue('ArcAuthenticator ..> ClientAuthenticator' in self.code)
  self.assertTrue('bridge.app.arc -> bridge.app.auth' in self.model)
 def test_component_writes_converge_on_single_sqlite_coordinator(self):
  writers=re.findall(r'bridge\.app\.(\w+)\s*->\s*bridge\.db',self.model)
  self.assertEqual(['storage'],writers)
 def test_owner_log_read_and_export_have_both_model_and_code_path(self):
  self.assertTrue('bridge.app.ownerCommands -> bridge.app.audit' in self.model)
  self.assertTrue('+exportApprovedSnapshot' in self.code and '+queryRedactedSnapshot' in self.code)
 def test_unknown_callback_late_result_and_cancel_are_closed_in_contract(self):
  for event in ('late confirmed registration result','late confirmed failure','late success after cancel','callback event','callback.cancel without'):
   self.assertTrue(event in self.main,event)
 def test_nonresumable_stage_expiry_has_one_policy(self):
  self.assertFalse('retaining only an explicitly resumable stage' in self.main)
  self.assertTrue('No resumable-stage mode is selected' in self.main)
 def test_migration_precedes_live_schema(self):
  self.assertTrue('Migration precedes the live CHECK' in self.main)
  self.assertFalse('names are compatibility labels' in self.main)
 def test_screen_tables_and_story_actions_resolve_to_declared_ids(self):
  screens={x['id']:x for x in self.ui['screens']}
  for x in self.ui['screens']+self.ui['storyboards']:
   self.assertTrue('['+x['id']+'](screens/'+x['id']+'.svg)' in self.main,x['id'])
  for story in self.ui['storyboards']:
   for step in story['steps']:
    screen=screens[step['screen_id']]
    self.assertTrue(step['control_id'] in screen['controls'],step['control_id'])
    self.assertTrue(step['state_id'] in screen['states'],step['state_id'])
 def test_self_adb_pairing_is_an_explicit_story_not_a_grant(self):
  story=next((x for x in self.ui['storyboards'] if x['id']=='STB06'),None)
  self.assertIsNotNone(story)
  self.assertTrue('does not grant' in story['boundary'])
  self.assertTrue(any(x['screen_id']=='SCR09' for x in story['steps']))
if __name__=='__main__':unittest.main()
