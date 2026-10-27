"""Round010 independent findings; regression fixtures before remediation."""
import copy,json,pathlib,sys,tempfile,unittest,zipfile,hashlib
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import build_catalog,test_build,test_device_resolution as fixtures,device_resolution as dr
class Review10(unittest.TestCase):
    def test_annotations_require_an_explicit_platform_version(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());path=root/'annotations.zip'
            with zipfile.ZipFile(path,'w') as z:z.writestr('a.xml','<root/>')
            raw=path.read_bytes();source=dict(spec['sources'][0],kind='sdk-annotations',path=path.name,local_path=path.name,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));source.pop('platform_api_version',None);spec['sources'].append(source);inv.write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError,'Explicit platform_api_version required'):build_catalog.build(inv,root/'out')
            self.assertFalse((root/'out/catalog.sqlite').exists())
    def test_non_authoritative_positive_observations_are_unknown(self):
        facts=fixtures.facts();facts['modules']['m']['authoritative']=False;facts['interfaces']['a.I']['authoritative']=False
        a=fixtures.entity('a',availability=fixtures.rule(modules={'m':dict(introduced='1',removed=None)}));b=fixtures.entity('b',availability=fixtures.rule(interface=dict(descriptor='a.I',minimum_version=1,accepted_hashes=[])))
        rows=fixtures.resolved([a,b],facts);self.assertEqual([rows[fixtures.h(k)]['state'] for k in ('a','b')],['unknown','unknown'])
    def test_malformed_interval_is_quarantined_without_hiding_other_rows(self):
        a=fixtures.entity('a',availability=fixtures.rule(platform=dict(introduced=[38,0],removed=[37,0])));b=fixtures.entity('b');rows=fixtures.resolved([a,b]);self.assertEqual(rows[fixtures.h('a')]['state'],'unknown');self.assertIn('APPLICABILITY_MALFORMED',rows[fixtures.h('a')]['reason_codes']);self.assertEqual(rows[fixtures.h('b')]['state'],'available')
    def test_invalidation_reason_is_retained(self):
        store=dr.ViewStore(fixtures.h('f'),1,0,'fixture','platform:local',0,'backend-fixture','boot-fixture');store.invalidate('module-replaced');self.assertEqual(store.history[-1],{'epoch':1,'reason':'module-replaced'})
    def test_facts_cannot_be_relabelled_with_a_new_epoch(self):
        facts=fixtures.facts();facts['capability_epoch']=0;facts['observed_at']='2026-10-27T01:00:00Z'
        with self.assertRaisesRegex(ValueError,'epoch'):dr.resolve([fixtures.entity('a')],facts,registry_id=fixtures.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=1,view_generation=1)
    def test_device_paths_use_declared_scopes_and_response_sets(self):
        api=json.loads((P/'contracts/openapi.json').read_text());scopes=set(api['components']['securitySchemes']['catalogAuth']['flows']['clientCredentials']['scopes'])
        for path,item in api['paths'].items():
            if '/device-views/' not in path:continue
            for method,operation in item.items():
                if method not in ('get','post'):continue
                for security in operation['security']:
                    self.assertTrue(set(security['catalogAuth'])<=scopes)
                self.assertIn('400',operation['responses']);self.assertIn('401',operation['responses'])
        self.assertIn('ETag',api['paths']['/v1/catalog/device-views/refresh']['post']['responses']['412']['headers'])
    def test_graph_digest_is_recomputed_at_publication(self):
        facts=fixtures.facts();snapshot=dr.resolve([fixtures.entity('a')],facts,registry_id=fixtures.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
        snapshot['entries'][0]['graph_digest']=fixtures.h('b');snapshot['view_id']=dr.digest({k:v for k,v in snapshot.items() if k!='view_id'})
        store=dr.ViewStore(fixtures.h('f'),1,0,'fixture','platform:local',0,'backend-fixture','boot-fixture',inventory=[fixtures.entity('a')])
        with self.assertRaisesRegex(ValueError,'graph digest'):store.publish(snapshot,expected_generation=0,facts=facts)
        self.assertIsNone(store.active)

if __name__=='__main__':unittest.main()
