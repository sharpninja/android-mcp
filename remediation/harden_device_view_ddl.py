import pathlib,re
R=pathlib.Path(__file__).resolve().parent
for p in (R/'device_view_ddl.sql',R/'catalog/bridge-config.sql'):
    s=p.read_text()
    # All snapshot/control identities have explicit non-null hash shapes.
    for column in ('scope_id','facts_id','view_id','contract_id'):
        s=s.replace(column+' TEXT PRIMARY KEY',column+' TEXT NOT NULL PRIMARY KEY')
    for column in ('scope_id','facts_id','view_id','entry_id','schema_hash','graph_digest','contract_id'):
        s=s.replace('length('+column+')=64)',"length("+column+")=64 AND "+column+" NOT GLOB '*[^0-9a-f]*')")
    s=s.replace('stale INTEGER NOT NULL CHECK(stale IN (0,1)),FOREIGN KEY(view_id,scope_id)', 'stale INTEGER NOT NULL CHECK(stale IN (0,1)),expected_inventory_count INTEGER NOT NULL CHECK(expected_inventory_count>=0),FOREIGN KEY(view_id,scope_id)')
    s=s.replace("v.overlay_generation=new.overlay_generation)))","v.overlay_generation=new.overlay_generation AND v.inventory_count=new.expected_inventory_count) OR new.expected_inventory_count<>old.expected_inventory_count))")
    s=s.replace("new.view_id IS NOT old.view_id)) OR", "new.view_id IS NOT old.view_id OR (new.expected_inventory_count<>old.expected_inventory_count AND new.registry_id=old.registry_id AND new.catalog_generation=old.catalog_generation AND new.overlay_generation=old.overlay_generation))) OR")
    extra='''CREATE TRIGGER device_facts_current BEFORE INSERT ON device_capability_facts WHEN NOT EXISTS(SELECT 1 FROM device_view_active a JOIN device_view_scopes s USING(scope_id) WHERE a.scope_id=new.scope_id AND a.capability_epoch=new.capability_epoch AND json_extract(new.document_json,'$.capability_epoch')=new.capability_epoch AND json_extract(new.document_json,'$.observed_at')=new.observed_at AND json_extract(new.document_json,'$.profile_id')=s.profile_id AND json_extract(new.document_json,'$.target_key')=s.target_key AND json_extract(new.document_json,'$.android_user_id')=s.android_user_id AND json_extract(new.document_json,'$.backend_instance_id')=s.backend_instance_id AND json_extract(new.document_json,'$.device_instance_id')=s.device_instance_id) BEGIN SELECT RAISE(ABORT,'Captured facts scope or epoch mismatch'); END;
CREATE TRIGGER device_view_stage BEFORE INSERT ON device_views WHEN new.state<>'staged' OR NOT EXISTS(SELECT 1 FROM device_view_active a WHERE a.scope_id=new.scope_id AND new.capability_epoch=a.capability_epoch AND new.view_generation=a.generation+1 AND new.registry_id=a.registry_id AND new.catalog_generation=a.catalog_generation AND new.overlay_generation=a.overlay_generation AND new.inventory_count=a.expected_inventory_count) BEGIN SELECT RAISE(ABORT,'Stage must pin the complete current inventory and epoch'); END;
'''
    assert 'CREATE TRIGGER device_view_stage' not in s
    if p.name=='bridge-config.sql':s=s.replace('COMMIT;',extra+'COMMIT;')
    else:s+=extra
    p.write_text(s)
