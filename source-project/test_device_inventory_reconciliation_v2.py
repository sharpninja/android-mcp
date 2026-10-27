import unittest,tempfile,pathlib,json,contextlib
import test_device_inventory_reconciliation as fixtures
from device_inventory_reconciliation_v2 import verify
class Inventory21(unittest.TestCase):
 def test_contract21_exact_bytes_and_manifest(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(fixtures.DeviceExport().setup_fixture(root)) as db:
    path=root/'device_inventory_manifest.json';doc=json.loads(path.read_text());doc['contract']='urn:android-binding-catalog:2.1.0:DeviceResolution';path.write_text(json.dumps(doc));self.assertEqual(verify(db,root)['entries'],1)
 def test_contract20_not_silently_relabelled(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory)
   with contextlib.closing(fixtures.DeviceExport().setup_fixture(root)) as db:
    with self.assertRaisesRegex(ValueError,'manifest mismatch'):verify(db,root)
