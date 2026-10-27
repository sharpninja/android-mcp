"""Startup version/type view requirements: fixture facts only, no device probes."""
import copy,json,pathlib,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
try:import device_resolution as resolver
except ImportError:resolver=None

def h(value):return value*64
def facts():
    return dict(profile_id='fixture',target_key='platform:local',android_user_id=0,backend_instance_id='backend-fixture',device_instance_id='boot-fixture',identity_verified=True,platform=dict(major=37,minor=1),extensions={'30':4},modules={'m':dict(state='present',version='12',authoritative=True)},interfaces={'a.I':dict(state='reachable',version=2,hash='interface-hash-v2',authoritative=True)},oem='fixture-oem',build_fingerprint='fixture-build',evidence_ids=['fixture-observation'])
def rule(**clause):return dict(alternatives=[clause],evidence_ids=['fixture-source'])
def entity(key,kind='binding',availability=None,schema='a',dependencies=None):
    return dict(entry_id=h(key),entity_kind=kind,variants=[dict(schema_hash=h(schema),availability=availability or rule(platform=dict(introduced=[37,0],removed=None)),dependencies=dependencies or [])])
def resolved(items,observations=None):
    result=resolver.resolve(items,observations or facts(),registry_id=h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
    return {e['entry_id']:e for e in result['entries']}

class DeviceResolution(unittest.TestCase):
    def setUp(self):self.assertIsNotNone(resolver,'Startup device/type resolver reference is missing')
    def test_major_minor_and_removed_apply_to_bindings_and_types(self):
        rows=resolved([entity('a',availability=rule(platform=dict(introduced=[37,2],removed=None))),entity('b','type',availability=rule(platform=dict(introduced=[36,0],removed=[37,1])))])
        self.assertEqual(rows[h('a')]['state'],'unavailable');self.assertEqual(rows[h('b')]['state'],'unavailable')
    def test_minor_unknown_does_not_become_zero(self):
        f=facts();f['platform']['minor']=None
        rows=resolved([entity('a',availability=rule(platform=dict(introduced=[37,2],removed=None))),entity('b','type')],f)
        self.assertEqual(rows[h('a')]['state'],'unknown');self.assertEqual(rows[h('b')]['state'],'available')
    def test_extension_alternative_is_not_android_minor(self):
        item=entity('a');item['variants'][0]['availability']=dict(alternatives=[dict(platform=dict(introduced=[38,0],removed=None)),dict(extensions={'30':4})],evidence_ids=['fixture-source'])
        self.assertEqual(resolved([item])[h('a')]['state'],'available')
        f=facts();f['extensions']={};self.assertEqual(resolved([item],f)[h('a')]['state'],'unknown')
    def test_modules_and_oem_builds_are_independent_axes(self):
        a=entity('a',availability=rule(modules={'m':dict(introduced='13',removed=None)}));b=entity('b','type',availability=rule(oem=['other-oem']));c=entity('c','type',availability=rule(build_fingerprints=['fixture-build']))
        rows=resolved([a,b,c]);self.assertEqual([rows[h(k)]['state'] for k in ('a','b','c')],['unavailable','unavailable','available'])
    def test_interface_version_hash_and_denied_probe(self):
        item=entity('a',availability=rule(interface=dict(descriptor='a.I',minimum_version=2,accepted_hashes=['interface-hash-v2'])))
        self.assertEqual(resolved([item])[h('a')]['state'],'available')
        f=facts();f['interfaces']['a.I']['state']='denied';self.assertEqual(resolved([item],f)[h('a')]['state'],'unknown')
        f=facts();f['interfaces']['a.I']['hash']='unrecognized';self.assertEqual(resolved([item],f)[h('a')]['state'],'unknown')
    def test_authoritative_absence_differs_from_unreachable(self):
        item=entity('a',availability=rule(interface=dict(descriptor='a.I',minimum_version=1,accepted_hashes=[])))
        for observed,expected in [('absent','unavailable'),('unreachable','unknown'),('unsupported','unknown')]:
            f=facts();f['interfaces']['a.I']['state']=observed;self.assertEqual(resolved([item],f)[h('a')]['state'],expected)
    def test_type_dependencies_and_missing_schema_are_consistent(self):
        a=entity('a',dependencies=[dict(entry_id=h('b'),schema_hash=h('b'))]);b=entity('b','type',schema='b',availability=rule(platform=dict(introduced=[38,0],removed=None)))
        rows=resolved([a,b]);self.assertEqual(rows[h('a')]['state'],'unavailable');self.assertEqual(len(rows),2)
        self.assertEqual(resolved([a])[h('a')]['state'],'unknown')
    def test_recursive_types_resolve_as_one_consistent_graph(self):
        a=entity('a','type',schema='a',dependencies=[dict(entry_id=h('b'),schema_hash=h('b'))]);b=entity('b','type',schema='b',dependencies=[dict(entry_id=h('a'),schema_hash=h('a'))]);rows=resolved([a,b]);self.assertTrue(all(r['state']=='available' for r in rows.values()))
    def test_available_variant_does_not_hide_ambiguous_unknown_variant(self):
        item=entity('a');item['variants'].append(dict(schema_hash=h('b'),availability=rule(modules={'unknown':dict(introduced='1',removed=None)}),dependencies=[]))
        self.assertEqual(resolved([item])[h('a')]['state'],'unknown')
    def test_dependency_schema_mismatch_does_not_select_a_codec(self):
        a=entity('a',dependencies=[dict(entry_id=h('b'),schema_hash=h('c'))]);b=entity('b','type',schema='b');self.assertEqual(resolved([a,b])[h('a')]['state'],'unknown')
    def test_unknown_lifecycle_is_retained_with_reason(self):
        item=entity('a');item['variants'][0]['availability']={'unknown_reason':'OEM lifecycle not acquired','x-raw':{'vendor':1}};rows=resolved([item]);self.assertEqual(rows[h('a')]['state'],'unknown');self.assertTrue(rows[h('a')]['reason_codes'])
    def test_atomic_publication_epoch_invalidation_and_cross_target(self):
        store=resolver.ViewStore(h('f'),1,0,'fixture','platform:local',0,'backend-fixture','boot-fixture');candidate=resolver.resolve([entity('a')],facts(),registry_id=h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
        store.publish(candidate,expected_generation=0);original=copy.deepcopy(store.read()['snapshot']);store.invalidate('backend-disconnect')
        self.assertTrue(store.read()['stale']);self.assertEqual(store.read()['snapshot'],original)
        with self.assertRaises(resolver.StaleView):store.publish(candidate,expected_generation=1)
        fresh=resolver.resolve([entity('a')],facts(),registry_id=h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=1,view_generation=2);fresh['target_key']='other-target'
        with self.assertRaises(ValueError):store.publish(fresh,expected_generation=1)
        self.assertEqual(store.read()['snapshot'],original)
    def test_availability_is_not_a_permission_grant(self):
        import policy_reference
        self.assertEqual(resolved([entity('a')])[h('a')]['state'],'available')
        self.assertEqual(policy_reference.evaluate([(h('a'),h('a'),'target',1)],{},None,entry_schema='tool',authority=True),'deny')

if __name__=='__main__':unittest.main()
