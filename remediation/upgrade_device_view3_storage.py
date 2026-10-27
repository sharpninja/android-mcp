import pathlib
R=pathlib.Path(__file__).resolve().parent
for p in (R/'device_view_ddl.sql',R/'catalog/bridge-config.sql'):
    s=p.read_text();old="schema_hash TEXT CHECK(schema_hash IS NULL OR length(schema_hash)=64 AND schema_hash NOT GLOB '*[^0-9a-f]*'),dependencies_json"
    assert old in s;s=s.replace(old,"schema_hash TEXT CHECK(schema_hash IS NULL OR length(schema_hash)=64 AND schema_hash NOT GLOB '*[^0-9a-f]*'),contract_hash TEXT CHECK(contract_hash IS NULL OR length(contract_hash)=64 AND contract_hash NOT GLOB '*[^0-9a-f]*'),dependencies_json")
    s=s.replace("schema_hash IS NOT NULL AND graph_digest IS NOT NULL","schema_hash IS NOT NULL AND contract_hash IS NOT NULL AND graph_digest IS NOT NULL")
    s=s.replace("t.schema_hash=json_extract(d.value,'$.schema_hash')","t.contract_hash=json_extract(d.value,'$.contract_hash')")
    s=s.replace("OR json_type(new.document_json,'$.graph_digest') IS NULL", "OR json_type(new.document_json,'$.contract_hash') IS NULL OR json_extract(new.document_json,'$.contract_hash') IS NOT new.contract_hash OR json_type(new.document_json,'$.graph_digest') IS NULL")
    s=s.replace("'$.schema_version') IS NOT '2.0.0'","'$.schema_version') IS NOT '3.0.0'")
    s=s.replace('e.reason_codes_json,e.schema_hash,e.dependencies_json','e.reason_codes_json,e.schema_hash,e.contract_hash,e.dependencies_json')
    s=s.replace('generation,schema_hash,graph_digest,reason_codes_json FROM resolved_device_catalog','generation,schema_hash,contract_hash,graph_digest,reason_codes_json FROM resolved_device_catalog')
    p.write_text(s)
p=R/'catalog/tooling/device_view_storage.py';s=p.read_text();s=s.replace("  self.expected_inventory_count=inventory_count","  if 'contract_hash' not in {r[1] for r in db.execute('PRAGMA table_info(device_view_entries)')}:raise ValueError('Legacy device-view storage requires checked migration; model3 cannot activate silently')\n  self.expected_inventory_count=inventory_count")
s=s.replace("INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?)","INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?,?)").replace("e['schema_hash'],dr.canonical(e['dependencies'])","e['schema_hash'],e['contract_hash'],dr.canonical(e['dependencies'])");p.write_text(s)
p=R/'catalog/tests/test_device_view_storage.py';s=p.read_text().replace("INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?)","INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?,?)").replace("row['schema_hash'],dr.canonical(row['dependencies'])","row['schema_hash'],row['contract_hash'],dr.canonical(row['dependencies'])");p.write_text(s)
