"""Offline architecture consistency regressions from independent024pass2.

These checks concern design contracts, not an implemented Android service.
"""
from pathlib import Path
import unittest,json,re,hashlib
R=Path(__file__).resolve().parent
P=R/'design-c4'
class Contract024(unittest.TestCase):
 def setUp(self):
  self.code=(P/'architecture/code-diagrams.md').read_text()
  self.dsl=(P/'architecture/workspace.dsl').read_text()
  self.contract=json.loads((P/'architecture/module-contract.json').read_text())
 def test_revocation_fanout_has_callback_and_lease_sinks(self):
  for cls in ('CallbackBroker','ArcInputLeaseRegistry'):
   self.assertIn(cls+' ..|> PrincipalSessionSink',self.code)
  self.assertIn('BridgeService "1" *-- "1" SessionInvalidationFanout',self.code)
 def test_ask_is_exact_protected_owner_path(self):
  for item in ('AdmissionGate ..> OwnerAuthenticationService : exact prepared Ask consent', '+observePendingExecutionChallenges()', '+resolveExecutionConsent(challengeRef, decision)', '+reviewExactExecutionConsent(challengeRef, decision)'):
   self.assertIn(item,self.code)
 def test_concrete_codec_proxy_and_client_factory(self):
  for cls,port in (('VerifiedCodecRegistry','CodecRegistry'),('VerifiedCallbackProxyFactory','CallbackProxyFactory'),('ProtocolArcClientFactory','ArcClientFactory')):
   self.assertIn(cls+' ..|> '+port,self.code)
   self.assertIn(cls,self.contract['classes'])
  self.assertIn('ArcSessionManager ..> ArcClientFactory',self.code)
  self.assertIn('ArcClientAdapter "1" *-- "1" ArcFrameCodec',self.code)
 def test_interfaces_are_domain_ports(self):
  for cls in set(re.findall(r'^interface (\w+)',self.code,re.M)):
   self.assertEqual(self.contract['classes'][cls],'bridge.domain',cls)
 def test_component_paths_rendered(self):
  execution=re.search(r'component bridge.app ExecutionComponents[^\n]+\{(.*?)autoLayout',self.dsl,re.S)[1]
  self.assertIn('bridge.app.arcController',execution)
  for target in ('callbacks','transport','device'):
   self.assertIn('bridge.app.storage -> bridge.app.'+target,self.dsl)
 def test_host_direct_route_and_ownership(self):
  for item in ('ProtocolHttpRouter ..> DirectEventEmitter : negotiated direct-event route','BridgeService "1" *-- "1" ClientCredentialService'):
   self.assertIn(item,self.code)
 def test_explicit_close_resume_and_root_generations(self):
  for item in ('Close admitted SDK/Binder backend resources','Close ADB collectors and owned connection resources','Reopen only after last challenge reaches terminal disposition'):
   self.assertIn(item,self.dsl)
  for item in ('+registerRoot(root, classification)', '+selectRegisteredRoot(rootId)', 'Node IDs are never reused within the process boot epoch'):
   self.assertIn(item,self.code)
if __name__=='__main__':unittest.main()
