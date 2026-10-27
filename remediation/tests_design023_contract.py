"""Cross-artifact design regressions, independent of runtime/data completeness."""
from pathlib import Path
import json,re,unittest
R=Path(__file__).resolve().parent
class Design023Contracts(unittest.TestCase):
 def setUp(self):
  self.design=(R/'design-c4/docs/Android MCP Bridge Design.md').read_text()
  self.api=json.loads((R/'catalog/contracts/openapi.json').read_text())
 def test_version_rejection_matches_pinned_arc_source(self):
  facts=json.loads((R/'design-c4/architecture/arc-upstream-facts.json').read_text())
  self.assertEqual(5,facts['version_mismatch']['status_number']);self.assertEqual('UNSUPPORTED',facts['version_mismatch']['status_name'])
  self.assertNotIn('For invalid ARC envelope version or empty request ID, a bounded ERROR response',self.design)
  self.assertIn('Version mismatch, including a zero-length default envelope, returns UNSUPPORTED5',self.design)
 def test_contract_version_has_one_current_value(self):
  contract=(R/'catalog/API-CONTRACT.md').read_text()
  self.assertEqual('1.6.0',self.api['info']['version'])
  self.assertTrue(contract.startswith('# Catalog administration contract 1.6.0\n'))
  self.assertNotIn('OpenAPI specification version is1.4.0',contract)
 def test_admin_scheme_is_issued_bearer_and_owner_proof_is_server_owned(self):
  scheme=self.api['components']['securitySchemes']['catalogAuth']
  self.assertEqual('http',scheme['type']);self.assertEqual('bearer',scheme['scheme'])
  for route,operations in self.api['paths'].items():
   for method,op in operations.items():
    if method in ('get','post','put','delete','patch'):
     self.assertTrue(all(scopes==[] for x in op.get('security',[]) for scopes in x.values()))
     if method in ('post','put','delete','patch'):
      self.assertEqual('prearmed-local-exact-command',op['x-owner-authorization'])
      self.assertIn('403',op['responses'])
 def test_import_stage_routes_define_sealed_hash_size_expiry(self):
  route=self.api['paths']['/v1/catalog/import-stages']
  self.assertEqual({'get','post'},set(route))
  fields=self.api['components']['schemas']['ImportStage']['properties']
  for name in ('stage_handle','sha256','size_bytes','expires_at','state'):
   self.assertIn(name,fields)
  self.assertEqual('application/octet-stream',route['post']['requestBody']['content'].popitem()[0])
  self.assertIn('x-owner-authorization',route['post'])
 def test_stage_lifetime_is_capacity_aware_and_configurable(self):
  self.assertNotIn('min(created+15minutes, management-window end)',self.design)
  self.assertNotIn('15minutes after copy begins',self.design)
  self.assertIn('import_stage_ttl_seconds',self.design)
  self.assertIn('STAGE_LIFETIME_INSUFFICIENT',self.design)
 def test_standalone_docs_tasks_verify_pins(self):
  build=(R/'design-c4/build.gradle.kts').read_text()
  for name in ('generateUiAssets','renderStoryboards'):
   body=build.split('val '+name+' by ',1)[1].split('\nval ',1)[0]
   self.assertIn('verifyToolchain',body)
  body=build.split('val validateArchitecture by ',1)[1].split('\nval ',1)[0]
  self.assertIn('outputs.file',body)
 def test_all_native_arc_identities_hash_exact_bytes_and_use_pinned_payloads(self):
  import hashlib
  data=json.loads((R/'design-c4/architecture/native-identities.json').read_text())
  facts=json.loads((R/'design-c4/architecture/arc-upstream-facts.json').read_text())
  self.assertEqual(facts['proto_sha256'],data['arc_proto_sha256'])
  self.assertEqual(set(range(1,11)),{x['method'] for x in data['arc_records']})
  self.assertEqual(8,len(data['records']))
  ids=set()
  for record in data['records']+data['arc_records']:
   self.assertEqual(hashlib.sha256(record['canonical_signature'].encode()).hexdigest(),record['binding_id'])
   self.assertNotIn(record['binding_id'],ids);ids.add(record['binding_id'])
   if record.get('method',8)!=8:
    for field in ('request_type','result_type'):
     self.assertIn(record[field].rsplit('.',1)[-1],facts['message_names'])
 def test_dispatch_permit_and_storage_ports_are_explicit_in_sources(self):
  code=(R/'design-c4/architecture/code-diagrams.md').read_text()
  model=(R/'design-c4/architecture/workspace.dsl').read_text()
  self.assertIn('InvocationPlanner ..> AdmissionGate : beginDispatch',code)
  self.assertIn('SqliteCatalogRepository ..> OwnerCapabilityVerifier',code)
  self.assertNotIn('SqliteCatalogRepository ..> OwnerAuthenticationService',code)
  self.assertIn('beginDispatch: atomically recheck epochs/authority',model)
  self.assertIn('Invalidate affected MCP sessions/delivery',model)
 def test_watch_contract_is_finite_and_blob_quota_accounted(self):
  for token in ('finite subscription result','per-subscription16MiB','flush and seal blob, commit reference','ARC_V1_CANCEL_UNSUPPORTED'):
   self.assertTrue(token in self.design,token)
if __name__=='__main__':unittest.main()
