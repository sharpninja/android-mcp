from pathlib import Path
import unittest,subprocess,json,tempfile,re,os,shutil
R=Path(__file__).resolve().parent;P=R/'design-c4';NODE=os.environ.get('NODE_BIN') or shutil.which('node')
if not NODE:raise RuntimeError('Node is required; set NODE_BIN to an existing pinned Node runtime')
class CrossArtifactGuards(unittest.TestCase):
 def fixture(self):
  return dict(code=(P/'architecture/code-diagrams.md').read_text(),dsl=(P/'architecture/workspace.dsl').read_text(),design=(P/'docs/Android MCP Bridge Design.md').read_text(),api=json.loads((R/'catalog/contracts/openapi.json').read_text()),blueprint=json.loads(re.search(r'```json ui-blueprint-v2\n(.*?)\n```',(P/'docs/ui-blueprint.md').read_text(),re.S)[1]),module_contract=json.loads((P/'architecture/module-contract.json').read_text()))
 def run_guard(self,data):
  with tempfile.TemporaryDirectory() as tmp:
   f=Path(tmp)/'input.json';f.write_text(json.dumps(data));script='const m=require(process.argv[1]),fs=require("node:fs");m.validate(JSON.parse(fs.readFileSync(process.argv[2],"utf8")));'
   return subprocess.run([NODE,'-e',script,str(P/'tooling/validate-contracts.cjs'),str(f)],capture_output=True,text=True)
 def test_current_cross_artifact_model(self):
  r=self.run_guard(self.fixture());self.assertEqual(0,r.returncode,r.stderr)
 def test_missing_dispatch_edge_and_revoke_bypass_fail(self):
  d=self.fixture();d['code']=d['code'].replace('InvocationPlanner ..> AdmissionGate : beginDispatch','InvocationPlanner ..> BackendAdapter : old dispatch');d['dsl']=d['dsl'].replace('ownerCommands -> bridge.app.policy "Enter exact protected commit','ownerCommands -> bridge.app.catalog "Enter exact protected commit');r=self.run_guard(d);self.assertNotEqual(0,r.returncode);self.assertIn('Missing dispatch caller',r.stderr);self.assertIn('Revocation bypasses',r.stderr)
 def test_placeholder_state_contract_and_missing_route_fail(self):
  d=self.fixture();s=d['blueprint']['screens'][0]
  for st in s['state_specs'].values():st['effect']='Read-only generic';st['recovery']='Retry generic'
  next(iter(s['control_specs'].values()))['behavior']='Exact named action; protected mutations open SCR09; reads never change authority'
  d['api']['paths'].pop('/v1/catalog/import-stages');r=self.run_guard(d);self.assertNotEqual(0,r.returncode);self.assertIn('Placeholder action',r.stderr);self.assertIn('Template states',r.stderr);self.assertIn('Missing sealed stage route',r.stderr)
if __name__=='__main__':unittest.main()
