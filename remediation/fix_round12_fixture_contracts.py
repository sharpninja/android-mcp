import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
for name in ('test_contract_models.py','test_round2.py'):
 p=C/'tests'/name;s=p.read_text()
 if name=='test_contract_models.py':s=s.replace("r=generate([e])['aliases'][0]", "e['kind']='method';r=generate([e])['aliases'][0]")
 else:s=s.replace("before=dispatch_manifest.generate([doc])", "doc['kind']='method';before=dispatch_manifest.generate([doc])")
 p.write_text(s)
p=C/'tests/test_device_catalog_pipeline.py';s=p.read_text().replace('import catalog_projection,validate_catalog','import catalog_projection,validate_catalog,effective_device_inventory')
needle="            facts=f.facts();facts['platform']={'major':37,'minor':0};view=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);status=dict(snapshot=view,stale=False,capability_epoch=0,generation=1)"
replacement="            with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:captured=effective_device_inventory.capture(db)\n            facts=f.facts();facts['platform']={'major':37,'minor':0};view=dr.resolve(captured['inventory'],facts,registry_id=captured['registry_id'],catalog_generation=captured['catalog_generation'],overlay_generation=captured['overlay_generation'],capability_epoch=0,view_generation=1);status=dict(snapshot=view,stale=False,capability_epoch=0,generation=1)"
assert needle in s;s=s.replace(needle,replacement);p.write_text(s)
print('Legacy minimal fixture documents now identify methods; active aliases consume actual pinned effective capture')
