"""Regression evidence for independent round011; no device or network work."""
import copy,hashlib,json,pathlib,sqlite3,sys,tempfile,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import catalog_projection as cp,device_resolution as dr,dispatch_manifest,build_catalog,test_build,test_device_resolution as f
class Round11(unittest.TestCase):
 def test_source_point_is_unknown_outside_proven_minor(self):
  rule=cp.lifecycle_rule({'extensions':{}},{'source_id':f.h('a'),'platform_api_version':'37.2'})
  for major,minor,state in [(36,0,'unknown'),(37,1,'unknown'),(37,2,'available'),(38,0,'unknown'),(37,None,'unknown')]:
   facts=f.facts();facts['platform']={'major':major,'minor':minor}
   with self.subTest(major=major,minor=minor):self.assertEqual(dr.applicability(rule,facts)[0],state)
 def test_inconsistent_since_sdks_is_unknown(self):
  for sdks in [[{'sdk_id':'0','minimum_version':'34'},{'sdk_id':'30','minimum_version':'4'}],[{'sdk_id':'30','minimum_version':'4'}]]:
   doc={'extensions':{'source_kind':'sdk-api-index','api_lifecycle':{'since':'35'},'sdk_extensions':sdks}}
   self.assertIn('unknown_reason',cp.lifecycle_rule(doc,{'source_id':f.h('a')}))
 def test_lifecycle_text_does_not_change_declared_type_contract(self):
  doc={'canonical_signature':'jvm:a/A','kind':'type','arguments':[],'codec_status':'unknown','extensions':{'declared_type_shape':{'header':'<class name="a/A" since="34"/>','fields':[]}}}
  other=copy.deepcopy(doc);other['extensions']['declared_type_shape']['header']='<class name="a/A" since="35" removed="39" sdks="30:4,0:35"/>'
  self.assertEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(other))
 def test_imported_aidl_type_and_noninvocable_fields(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text())
   for name,text in [('IFoo.aidl','package b; import a.P; interface I { void take(in P p); }'),('P.aidl','package a; parcelable P { int x; }')]:
    raw=text.encode();(root/name).write_bytes(raw);s=copy.deepcopy(spec['sources'][2]);s.update(path=name,local_path=name,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));
    if name=='IFoo.aidl':spec['sources'][2]=s
    else:spec['sources'].append(s)
   inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())};inventory={d['entry_id']:d for d in map(json.loads,(root/'out/device_inventory.jsonl').read_text().splitlines())}
   self.assertEqual(docs['aidl:b.I#take(P)']['arguments'][0]['status'],'resolved')
   self.assertEqual(inventory[docs['aidl:a.P#x']['entry_id']]['entity_kind'],'other')
 def test_same_count_foreign_inventory_cannot_activate(self):
  inventory=[f.entity('a')];other=[f.entity('b')];facts=f.facts()
  store=dr.ViewStore(f.h('f'),1,0,*[facts[k] for k in dr.SCOPE],inventory=inventory)
  snapshot=dr.resolve(other,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
  with self.assertRaisesRegex(ValueError,'inventory|Inventory'):store.publish(snapshot,expected_generation=0,facts=facts)
 def test_formal_name_cannot_resolve_to_package_class(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=b'package a {\n public class P {\n }\n public class A<P> {\n method public P take(P);\n }\n}'
   (root/'api.txt').write_bytes(raw);spec['sources'][0].update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   doc=next(d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()) if d['kind']=='method');self.assertEqual(doc['arguments'][0]['status'],'unknown');self.assertIn('formal',doc['arguments'][0]['reason'].lower())
if __name__=='__main__':unittest.main()
