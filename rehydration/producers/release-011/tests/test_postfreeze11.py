"""Post-freeze011 audit: downstream type changes must invalidate schema grants."""
import json,pathlib,sys,tempfile,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import build_catalog,test_round8,device_resolution as dr,test_device_resolution as f,policy_reference
class TransitiveSchemaPolicy(unittest.TestCase):
    def test_downstream_type_shape_changes_selected_binding_schema_hash(self):
        hashes=[];graphs=[]
        for field_type in ('int','long'):
            with tempfile.TemporaryDirectory() as td:
                root=pathlib.Path(td);inv=test_round8.Review8().fixture(root,'package a; interface I { parcelable P { '+field_type+' x; } void take(in P p); }');build_catalog.build(inv,root/'out')
                docs=[json.loads(s) for s in (root/'out/entries.jsonl').read_text().splitlines()];key=next(d['entry_id'] for d in docs if d['canonical_signature']=='aidl:a.I#take(P)');inventory=[json.loads(s) for s in (root/'out/device_inventory.jsonl').read_text().splitlines()]
                facts=f.facts();facts['platform']={'major':37,'minor':0};snapshot=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);row=next(r for r in snapshot['entries'] if r['entry_id']==key);self.assertEqual(row['state'],'available');hashes.append(row['schema_hash']);graphs.append(row['graph_digest'])
        self.assertNotEqual(graphs[0],graphs[1]);self.assertNotEqual(hashes[0],hashes[1],'A widened dependent type must not reuse an existing binding schema grant')
        old=policy_reference.Rule('allow',hashes[0],frozenset({'fixture'}),1);entry=policy_reference.Rule('allow','tool',frozenset({'fixture'}),1)
        self.assertEqual(policy_reference.evaluate([(key,hashes[1],'fixture',1)],{key:old},entry,entry_schema='tool',authority=True),'deny')
if __name__=='__main__':unittest.main()
