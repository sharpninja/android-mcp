from pathlib import Path
import subprocess,json,tempfile,unittest,os,shutil
R=Path(__file__).resolve().parent;P=R/'design-c4';NODE=os.environ.get('NODE_BIN') or shutil.which('node')
if not NODE:raise RuntimeError('Node is required; set NODE_BIN to an existing pinned Node runtime')
class ModuleGraph(unittest.TestCase):
 def run_model(self,model,source):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'m.json').write_text(json.dumps(model));(p/'s.md').write_text(source)
   return subprocess.run([NODE,str(P/'tooling/validate-modules.cjs'),str(p/'m.json'),str(p/'s.md')],capture_output=True,text=True)
 def test_reproduces_cyclic_old_crosswalk(self):
  result=self.run_model({'classes':{'DeviceViewResolver':'bindings','CallbackBroker':'application'},'dependencies':{'bindings':['application'],'application':['bindings']}},'class DeviceViewResolver\nclass CallbackBroker\nDeviceViewResolver ..> CallbackBroker')
  self.assertNotEqual(0,result.returncode);self.assertIn('Module cycle',result.stderr)
 def test_forbidden_concrete_edge_rejected(self):
  result=self.run_model({'classes':{'Repository':'storage','OwnerAuthenticationService':'application'},'dependencies':{'storage':['domain'],'application':['domain'],'domain':[]}},'class Repository\nclass OwnerAuthenticationService\nRepository ..> OwnerAuthenticationService')
  self.assertNotEqual(0,result.returncode);self.assertIn('Forbidden dependency',result.stderr)
 def test_entire_current_class_graph_is_owned_and_acyclic(self):
  result=self.run_model(json.loads((P/'architecture/module-contract.json').read_text()),(P/'architecture/code-diagrams.md').read_text())
  self.assertEqual(0,result.returncode,result.stderr)
if __name__=='__main__':unittest.main()
