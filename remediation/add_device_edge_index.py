import pathlib
R=pathlib.Path(__file__).resolve().parent
ddl="""
-- Available graph adjacency supports bounded indexed traversal; unknown edges remain lossless in JSON.
CREATE TABLE device_type_edges(view_id TEXT NOT NULL,entry_id TEXT NOT NULL,type_entry_id TEXT NOT NULL,type_contract_hash TEXT NOT NULL,PRIMARY KEY(view_id,entry_id,type_entry_id),FOREIGN KEY(view_id,entry_id) REFERENCES device_view_entries ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,FOREIGN KEY(view_id,type_entry_id) REFERENCES device_view_entries DEFERRABLE INITIALLY DEFERRED);
CREATE INDEX device_type_reverse ON device_type_edges(view_id,type_entry_id);
CREATE TRIGGER device_edge_source BEFORE INSERT ON device_type_edges WHEN NOT EXISTS(SELECT 1 FROM device_view_entries e JOIN device_views v USING(view_id),json_each(e.dependencies_json) d WHERE e.view_id=new.view_id AND e.entry_id=new.entry_id AND e.state='available' AND v.state='staged' AND json_extract(d.value,'$.entry_id')=new.type_entry_id AND json_extract(d.value,'$.contract_hash')=new.type_contract_hash) BEGIN SELECT RAISE(ABORT,'Graph edge must match staged available membership'); END;
CREATE TRIGGER device_edge_project AFTER INSERT ON device_view_entries WHEN new.state='available' BEGIN INSERT INTO device_type_edges SELECT new.view_id,new.entry_id,json_extract(d.value,'$.entry_id'),json_extract(d.value,'$.contract_hash') FROM json_each(new.dependencies_json) d; END;
CREATE TRIGGER device_edge_update BEFORE UPDATE ON device_type_edges BEGIN SELECT RAISE(ABORT,'Graph edges are immutable'); END;
CREATE TRIGGER device_edge_delete BEFORE DELETE ON device_type_edges WHEN EXISTS(SELECT 1 FROM device_view_active WHERE view_id=old.view_id) OR EXISTS(SELECT 1 FROM device_view_pins WHERE view_id=old.view_id) OR ((SELECT state FROM device_views WHERE view_id=old.view_id)='sealed' AND (SELECT authorized FROM device_view_gc WHERE singleton=1) IS NOT 1) BEGIN SELECT RAISE(ABORT,'Sealed active or pinned graph edges cannot be deleted'); END;
"""
for p in (R/'catalog/bridge-config.sql',R/'device_view_ddl.sql'):
 s=p.read_text();assert 'CREATE TABLE device_type_edges' not in s
 s=s.replace('COMMIT;',ddl+'\nCOMMIT;') if 'COMMIT;' in s else s+ddl;p.write_text(s)
print('Indexed available dependency adjacency added; unresolved edges remain in complete JSON metadata')
