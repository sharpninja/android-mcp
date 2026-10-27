"""Design regression checks for independently reproduced round022 defects."""
from pathlib import Path
from html.parser import HTMLParser
import base64,json,re,unittest,xml.etree.ElementTree as ET
P=Path(__file__).resolve().parent/'design-c4'

class DeckParser(HTMLParser):
    def __init__(self):super().__init__();self.figures=[];self.literal_images=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='figure':self.figures.append(a.get('style',''))
    def handle_data(self,data):
        if '![bg ' in data:self.literal_images.append(data[:80])

class Design022Contract(unittest.TestCase):
    def test_slides_contain_actual_vector_backgrounds_not_raw_markdown(self):
        m=json.loads((P/'docs/ui-asset-manifest.json').read_text())
        steps=[step for story in m['storyboards'] for step in story['steps']]
        parser=DeckParser();parser.feed((P/'docs/storyboards.html').read_text())
        self.assertFalse(parser.literal_images,'Marp rejected an SVG data-URI Markdown image')
        self.assertEqual(len(steps),len(parser.figures))
        for style,step in zip(parser.figures,steps):
            encoded=re.search(r'data:image/svg\+xml;base64,([A-Za-z0-9+/=]+)',style)
            self.assertIsNotNone(encoded,'Background must be an embedded vector SVG')
            raw=base64.b64decode(encoded[1])
            self.assertEqual((P/'docs/screens/steps'/(step['step_id']+'.svg')).read_bytes(),raw)
            state_panel=ET.fromstring(raw)
            self.assertEqual(step['state_id'],state_panel.get('data-screen-state'))
            self.assertEqual(step['control_id'],state_panel.get('data-focus-control'))
    def test_error_status_fixture_uses_actual_closed_health_domains(self):
        m=json.loads((P/'docs/ui-asset-manifest.json').read_text())
        screen=next(x for x in m['screens'] if x['id']=='SCR10');h=screen['health_example']
        self.assertEqual('Error',h['service']);self.assertEqual('error',h['mcp'])
        self.assertEqual('quarantined',h['catalog']);self.assertEqual('stale',h['device_view'])
        root=ET.parse(P/'ui-svg/SCR10.svg').getroot()
        chip=next(x for x in root.iter() if x.get('data-status-id')=='SCR10.STATUS')
        self.assertNotIn('Running',''.join(chip.itertext()))
        self.assertIn('desiredState',(P/'architecture/code-diagrams.md').read_text())
    def test_controls_states_and_external_actors_have_explicit_contracts(self):
        m=json.loads((P/'docs/ui-asset-manifest.json').read_text());ss={x['id']:x for x in m['screens']}
        for sid,s in ss.items():
            root=ET.parse(P/'ui-svg'/(sid+'.svg')).getroot()
            self.assertEqual(set(s['controls']),{x.get('data-control-id') for x in root.iter() if x.get('data-control-id')})
            self.assertEqual(set(s['controls']),set(s['control_specs']))
            for state,spec in s['state_specs'].items():
                self.assertEqual(set(s['controls']),set(spec['enabled_controls'])|set(spec['disabled_controls']))
                self.assertFalse(set(spec['enabled_controls'])&set(spec['disabled_controls']))
        steps=[x for story in m['storyboards'] for x in story['steps']]
        self.assertTrue(any(x['actor']=='External MCP client' and x['interaction']=='external_event' for x in steps))
        for x in steps:
            self.assertTrue(x['error'] and x['cancel']);self.assertEqual(ss[x['screen_id']]['controls'][x['control_id']],x['action'])
        self.assertIn('SCR09.C01',ss['SCR09']['state_specs']['SCR09.S1']['disabled_controls'])
        self.assertIn('SCR07.C07',ss['SCR07']['state_specs']['SCR07.S0']['disabled_controls'])
    def test_arc_input_lifetime_matches_pinned_singleton_not_unary_socket(self):
        d=(P/'docs/Android MCP Bridge Design.md').read_text();c=(P/'architecture/code-diagrams.md').read_text()
        self.assertIn('press in requestA, move inB, release inC',d)
        self.assertIn('ArcInputLeaseRegistry "1" *-- "0..1" ArcInputDispatcher',c)
        self.assertNotIn('ArcConnectionSession "1" *-- "0..1" ArcInputDispatcher',c)
        self.assertNotIn('stale node/frame targets',d)
    def test_arc_backend_and_health_paths_exist_across_views(self):
        c=(P/'architecture/code-diagrams.md').read_text();w=(P/'architecture/workspace.dsl').read_text()
        self.assertIn('ArcTargetBackend ..|> BackendAdapter',c)
        self.assertIn('bridge.app.invoke -> bridge.app.arcController',w)
        self.assertIn('+arcListener',c)
        self.assertIn('SCR08.C07',json.loads((P/'docs/ui-asset-manifest.json').read_text())['screens'][7]['controls'])
    def test_unknown_state_has_phase_specific_cleanup_exits(self):
        d=(P/'docs/Android MCP Bridge Design.md').read_text()
        self.assertIn('uncertain_phase=cleanup) + confirmed unregister success',d)
        self.assertIn('uncertain_phase=cleanup) + confirmed unregister failure',d)
        self.assertIn('| Reserved |',d)
        self.assertNotIn('kind CHECK mcp/admin;',d)

if __name__=='__main__':unittest.main()
