import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text()
ddl='''CREATE TABLE registry_inventory(registry_id TEXT NOT NULL,entry_id TEXT NOT NULL,document_json TEXT NOT NULL CHECK(json_valid(document_json)),record_sha256 TEXT NOT NULL CHECK(length(record_sha256)=64 AND record_sha256 NOT GLOB '*[^0-9a-f]*'),PRIMARY KEY(registry_id,entry_id),FOREIGN KEY(registry_id,entry_id) REFERENCES registry_entries(registry_id,entry_id));
CREATE TRIGGER registry_inventory_staged BEFORE INSERT ON registry_inventory WHEN (SELECT state FROM registries WHERE registry_id=new.registry_id) IS NOT 'staged' BEGIN SELECT RAISE(ABORT,'Inventory membership is sealed'); END;
CREATE TRIGGER registry_inventory_no_update BEFORE UPDATE ON registry_inventory BEGIN SELECT RAISE(ABORT,'Version the immutable inventory projection'); END;
CREATE TRIGGER registry_inventory_no_delete BEFORE DELETE ON registry_inventory BEGIN SELECT RAISE(ABORT,'Retain immutable inventory evidence'); END;
'''
assert 'CREATE TABLE registry_inventory' not in s;s=s.replace('PRAGMA user_version=3;',ddl+'PRAGMA user_version=3;');p.write_text(s)
(C/'schema.sql').write_text((C/'migrations/001-initial.sql').read_text()+s)
p=R/'build_device_contract.py';s=p.read_text();s=s.replace("definitions['Clause']=obj(","definitions['Clause']=obj(")
needle="known_rule=obj(dict(alternatives="
assert needle in s;s=s.replace(needle,"definitions['Clause']['properties']['unknown_reason']=string\n"+needle);p.write_text(s)
