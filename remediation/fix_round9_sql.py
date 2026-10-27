import pathlib
R=pathlib.Path(__file__).resolve().parent;P=R/'catalog'
p=P/'migrations/001-initial.sql';s=p.read_text()
for name,nullable in [('sha256',False),('binding_id',False),('base_record_hash',True)]:
    old=name+(' TEXT,' if nullable else ' TEXT NOT NULL'+(' PRIMARY KEY' if name=='binding_id' else '')+',')
    check=f"CHECK({'%s IS NULL OR '%name if nullable else ''}(length({name})=64 AND {name} NOT GLOB '*[^0-9a-f]*'))"
    new=old[:-1]+' '+check+','
    # Only column declarations, not references or other identity columns.
    assert old in s,old;s=s.replace(old,new,1)
p.write_text(s)
p=P/'migrations/002-evidence-and-publication.sql';s=p.read_text();s=s.replace('record_sha256 TEXT NOT NULL,PRIMARY KEY(registry_id,entry_id)','record_sha256 TEXT NOT NULL CHECK(length(record_sha256)=64 AND record_sha256 NOT GLOB \'*[^0-9a-f]*\'),PRIMARY KEY(registry_id,entry_id)')
guards='''CREATE TRIGGER active_registry_no_delete BEFORE DELETE ON active_registry BEGIN SELECT RAISE(ABORT,'Active pointer must be switched, not deleted'); END;
CREATE TRIGGER catalog_view_no_delete BEFORE DELETE ON catalog_view BEGIN SELECT RAISE(ABORT,'Catalog generation singleton cannot be deleted'); END;
CREATE VIEW overlay_etags AS
 SELECT o.entry_id,o.state,o.revision,
 '"cat1:'||v.registry_generation||':'||coalesce(e.record_sha256,'local')||':'||o.entry_id||':'||o.revision||':'||o.state||'"' AS etag
 FROM overlays o CROSS JOIN catalog_view v LEFT JOIN effective_entries e ON e.entry_id=o.entry_id;
'''
assert 'CREATE VIEW overlay_etags' not in s
s=s.replace('PRAGMA user_version=3;',guards+'PRAGMA user_version=3;');p.write_text(s)
(P/'schema.sql').write_text((P/'migrations/001-initial.sql').read_text()+(P/'migrations/002-evidence-and-publication.sql').read_text())
