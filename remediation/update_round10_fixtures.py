import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tests/test_build.py';s=p.read_text().replace("api_level=37,channel='stable'","api_level=37,platform_api_version='37.0',channel='stable'");p.write_text(s)
p=C/'tests/test_round3.py';s=p.read_text().replace("['target_match_kind'],'java-lang-spelling-equivalence'","['target_match_kind'],'same-snapshot-sdk-signature-java-lang-spelling-equivalence'");p.write_text(s)
p=C/'tests/test_device_resolution.py';s=p.read_text().replace("identity_verified=True,platform=","identity_verified=True,capability_epoch=0,observed_at='2026-10-27T01:00:00Z',platform=")
s=s.replace('store.publish(candidate,expected_generation=0)','store.publish(candidate,expected_generation=0,facts=facts())').replace('store.publish(candidate,expected_generation=1)','store.publish(candidate,expected_generation=1,facts=facts())').replace('store.publish(snapshot,expected_generation=0)','store.publish(snapshot,expected_generation=0,facts=facts())')
old="fresh=resolver.resolve([entity('a')],facts(),registry_id=h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=1,view_generation=2);fresh['target_key']='other-target'\n        with self.assertRaises(ValueError):store.publish(fresh,expected_generation=1)"
new="observed=facts();observed.update(target_key='other-target',capability_epoch=1)\n        fresh=resolver.resolve([entity('a')],observed,registry_id=h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=1,view_generation=2)\n        with self.assertRaisesRegex(ValueError,'Cross-target/profile snapshot'):store.publish(fresh,expected_generation=1,facts=observed)"
assert old in s;s=s.replace(old,new)
old="self.assertEqual(policy_reference.evaluate([(h('a'),h('a'),'target',1)],{},None,entry_schema='tool',authority=True),'deny')"
new="allow=policy_reference.Rule('allow',h('a'),frozenset({'target'}),1);tool=policy_reference.Rule('allow','tool',frozenset({'target'}),1)\n        self.assertEqual(policy_reference.evaluate([(h('a'),h('a'),'target',1)],{h('a'):allow},tool,entry_schema='tool',authority=True),'admit-candidate')\n        self.assertEqual(policy_reference.evaluate([(h('a'),h('b'),'target',1)],{h('a'):allow},tool,entry_schema='tool',authority=True),'deny')"
assert old in s;s=s.replace(old,new);p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text().replace('contract 1.1','contract 1.2');p.write_text(s)
