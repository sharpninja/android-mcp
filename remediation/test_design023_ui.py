from pathlib import Path
import json,re,unittest,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parent;P=R/'design-c4'
class UiBlueprint(unittest.TestCase):
 def setUp(self):self.m=json.loads((P/'docs/ui-asset-manifest.json').read_text());self.s={x['id']:x for x in self.m['screens']}
 def test_current_control_and_state_contracts_are_explicit(self):
  placeholder='Exact named action; protected mutations open SCR09; reads never change authority'
  for s in self.m['screens']:
   for cid,c in s['control_specs'].items():
    self.assertNotEqual(placeholder,c['behavior']);self.assertTrue(c['fields'] and c['success'] and c['error'] and c['cancel']);self.assertFalse(c['gates']['remote_press_allowed'])
   for state,c in s['state_specs'].items():
    self.assertNotEqual('Read-only until exact protected commit; error/conflict never applies a partial mutation.',c['effect'])
    self.assertTrue(c['effect'] and c['recovery'])
 def test_markdown_is_single_canonical_behavior_source(self):
  source=(P/'docs/ui-blueprint.md').read_text();m=json.loads(re.search(r'```json ui-blueprint-v2\n(.*?)\n```',source,re.S)[1]);projected=dict(self.m);projected.pop('state_svg_files')
  self.assertEqual(m,projected)
 def test_every_state_has_matching_vector_variant_and_exact_control_enablement(self):
  for s in self.m['screens']:
   for state,sp in s['state_specs'].items():
    r=ET.parse(P/'docs/screens/states'/(state+'.svg')).getroot();self.assertEqual(state,r.get('data-screen-state'))
    enabled={e.get('data-control-id') for e in r.iter() if e.get('data-enabled')=='true'};disabled={e.get('data-control-id') for e in r.iter() if e.get('data-enabled')=='false'}
    self.assertEqual(set(sp['enabled_controls']),enabled);self.assertEqual(set(sp['disabled_controls']),disabled)
  self.assertIn('Service Stopped',(P/'docs/screens/states/SCR01.S3.svg').read_text());self.assertNotIn('Service Running',(P/'docs/screens/states/SCR01.S3.svg').read_text())
 def test_storyboard_images_match_named_state_and_branch_success_does_not_force_error(self):
  deck=(P/'storyboards/storyboard.md').read_text();images=re.findall(r'!\[Screen]\(([^)]+)\)',deck);steps=[x for s in self.m['storyboards'] for x in s['steps']];self.assertEqual(len(steps),len(images))
  for st,img in zip(steps,images):
   r=ET.parse((P/'storyboards'/img).resolve()).getroot();self.assertEqual(st['state_id'],r.get('data-screen-state'));self.assertEqual(st['control_id'],r.get('data-focus-control'))
   if st['interaction']=='control':self.assertIn(st['control_id'],self.s[st['screen_id']]['state_specs'][st['state_id']]['enabled_controls'])
  index={x['step_id']:x for x in steps}
  for st in steps:
   if st['branch']=='main' and st['next_success'] in index:self.assertEqual('main',index[st['next_success']]['branch'])
   for dest in st['error_branches'].values():self.assertIn(dest,index)
 def test_every_rendered_state_health_uses_closed_domains_and_integer_revision(self):
  for s in self.m['screens']:
   for state in s['state_specs']:
    r=ET.parse(P/'docs/screens/states'/(state+'.svg')).getroot();h=json.loads(next(e.get('data-health-snapshot') for e in r.iter() if e.get('data-health-snapshot')))
    for key,domain in self.m['health_domains'].items():self.assertIn(h[key],domain,state+' '+key)
    self.assertIsInstance(h['revision'],int);self.assertIn(h['desired_state'],['running','stopped'])
if __name__=='__main__':unittest.main()
