import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tests/test_device_view_storage.py';s=p.read_text()
s=s.replace('        self.store=storage.DeviceViewStore',"        self.inventory=[f.entity('a',dependencies=[dict(entry_id=f.h('b'),schema_hash=f.h('b'))]),f.entity('b','type',schema='b')]\n        self.store=storage.DeviceViewStore")
s=s.replace('overlay_generation=0,inventory_count=2)','overlay_generation=0,inventory=self.inventory,inventory_count=2)')
s=s.replace("'INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?)'", "'INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?,?)'").replace("1,0,0,2,2,snapshot['facts_id']", "1,0,0,2,2,snapshot['inventory_sha256'],snapshot['facts_id']")
p.write_text(s)
p=C/'tests/test_device_catalog_pipeline.py';s=p.read_text().replace('overlay_generation=0,inventory_count=len(raw))','overlay_generation=0,inventory=raw,inventory_count=len(raw))');p.write_text(s)
p=C/'tests/test_round10.py';s=p.read_text().replace("store=dr.ViewStore(fixtures.h('f'),1,0,'fixture','platform:local',0,'backend-fixture','boot-fixture')\n        with self.assertRaisesRegex(ValueError,'graph digest')", "store=dr.ViewStore(fixtures.h('f'),1,0,'fixture','platform:local',0,'backend-fixture','boot-fixture',inventory=[fixtures.entity('a')])\n        with self.assertRaisesRegex(ValueError,'graph digest')");p.write_text(s)
# Empty one-line P declaration was not emitted by the legacy signature fixture grammar.
p=C/'tests/test_round11.py';s=p.read_text().replace('public class P {}\\n','public class P {\\n }\\n');p.write_text(s)
print('Updated offline fixtures to provide trusted inventory pins')
