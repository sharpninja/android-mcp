import unittest,sqlite3,pathlib,tempfile,json,hashlib,contextlib
from device_inventory_reconciliation import verify
class DeviceExport(unittest.TestCase):
 def setup_fixture(self,root):
  db=sqlite3.connect(':memory:');db.executescript('CREATE TABLE active_registry(registry_id TEXT);INSERT INTO active_registry VALUES (\'reg\');CREATE TABLE registry_inventory(registry_id TEXT,entry_id TEXT,document_json TEXT,record_sha256 TEXT);')
  raw='{"entry_id":"a","entity_kind":"type","variants":[]}';digest=hashlib.sha256(raw.encode()).hexdigest();db.execute('INSERT INTO registry_inventory VALUES (?,?,?,?)',('reg','a',raw,digest));payload=(raw+'\n').encode();(root/'device_inventory.jsonl').write_bytes(payload);manifest=dict(contract='urn:android-binding-catalog:2.0.0:DeviceResolution',registry_id='reg',entries=1,inventory_sha256=hashlib.sha256(payload).hexdigest());(root/'device_inventory_manifest.json').write_text(json.dumps(manifest));return db
 def test_exact_bytes_count_identity_and_manifest(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(self.setup_fixture(root)) as db:self.assertEqual(verify(db,root)['entries'],1)
 def test_changed_row_same_count_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(self.setup_fixture(root)) as db:
    (root/'device_inventory.jsonl').write_text('{"entry_id":"b"}\n')
    with self.assertRaises(ValueError):verify(db,root)
 def test_extra_export_row_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(self.setup_fixture(root)) as db:
    with (root/'device_inventory.jsonl').open('ab') as f:f.write(b'{}\n')
    with self.assertRaises(ValueError):verify(db,root)
 def test_bad_stored_hash_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(self.setup_fixture(root)) as db:
    db.execute('UPDATE registry_inventory SET record_sha256=?',('0'*64,))
    with self.assertRaises(ValueError):verify(db,root)
 def test_manifest_count_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(self.setup_fixture(root)) as db:
    p=root/'device_inventory_manifest.json';m=json.loads(p.read_text());m['entries']=2;p.write_text(json.dumps(m))
    with self.assertRaises(ValueError):verify(db,root)
