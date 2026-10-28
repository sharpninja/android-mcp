from pathlib import Path
import json,re,unittest,hashlib
R=Path(__file__).resolve().parent;P=R/'design-c4'
class Core024(unittest.TestCase):
 def setUp(self):
  self.design=(P/'docs/Android MCP Bridge Design.md').read_text();self.code=(P/'architecture/code-diagrams.md').read_text();self.api=json.loads((R/'catalog/contracts/openapi.json').read_text())
 def test_actual_controls_are_in_generated_design_table(self):
  m=json.loads((P/'docs/ui-asset-manifest.json').read_text())
  table=self.design.split('<!-- GENERATED UI NAVIGATION BEGIN -->')[1].split('<!-- GENERATED UI NAVIGATION END -->')[0]
  for s in m['screens']:
   for cid,label in s['controls'].items():self.assertIn(cid+' '+label,table)
  self.assertNotIn('all 62 slides',self.design)
 def test_delete_reason_and_metadata_etags(self):
  op=self.api['paths']['/v1/catalog/entries/{entry_id}']['delete']
  self.assertTrue(op['requestBody']['required'])
  self.assertIn('reason',op['requestBody']['content']['application/json']['schema']['required'])
  for path in ('/v1/catalog/callback-descriptors/{callback_id}','/v1/catalog/functional-groups/{group_id}'):
   self.assertIn('meta1',self.api['paths'][path]['put']['responses']['201']['headers']['ETag']['description'])
 def test_pinned_portable_metadata_contract(self):
  pins=json.loads((P/'contracts/source-pins.json').read_text())
  pin=next(x for x in pins['files'] if x['file']=='SOURCE-METADATA-CONTRACT.md')
  self.assertEqual(pin['sha256'],hashlib.sha256((R/'catalog/SOURCE-METADATA-CONTRACT.md').read_bytes()).hexdigest())
  self.assertEqual((R/'catalog/SOURCE-METADATA-CONTRACT.md').read_bytes(),(P/'contracts/SOURCE-METADATA-CONTRACT.md').read_bytes())
 def test_inbound_arc_counts_dispatch_and_lifecycle_uses_port(self):
  for text in ('ArcRuntime ..> AdmissionGate : beginDispatch/endDispatch','interface LifecycleCommands','BridgeService ..|> LifecycleCommands','ComposeManagementViewModel ..> SessionCommandFacade'):
   self.assertIn(text,self.code)
 def test_screen_result_schema_has_one_field_spelling(self):
  doc=json.loads((P/'contracts/ScreenStateResult.schema.json').read_text())
  self.assertTrue({'captureId','capturedAt','coordinateSpaceVersion','displayId'}<=set(doc['required']))
  self.assertNotIn('captured_at, coordinate_version, capture_id',self.design)
 def test_single_writer_credential_and_modifier_semantics(self):
  self.assertIn('KeystoreCredentialVault ..> CatalogRepository : credential verifier writes use the single serialized repository writer',self.code)
  self.assertIn('No modifier field exists on ARCv1 events',self.design)
 def test_generic_example_uses_canonical_argument_names(self):
  a=self.design.index('{\n  "jsonrpc": "2.0",\n  "id": 42,\n  "method": "tools/call"');b=self.design.index('\n\n',a)
  request=json.loads(self.design[a:b]);self.assertEqual(request['params']['name'],'binding.invoke')
  self.assertEqual(set(request['params']['arguments']),{'bindingId','schemaHash','targetHandle','arguments'})
  self.assertIn('placeholder strings are not valid executable pins',self.design)
 def test_pending_owner_state_is_paused_with_explicit_reason(self):
  import xml.etree.ElementTree as ET
  root=ET.parse(P/'docs/screens/states/SCR09.S0.svg').getroot()
  h=json.loads(next(e.get('data-health-snapshot') for e in root.iter() if e.get('data-health-snapshot')))
  self.assertEqual(h['service'],'Paused');self.assertEqual(h['pause_reason'],'OWNER_CHALLENGE')
if __name__=='__main__':unittest.main()
