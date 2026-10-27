import unittest,tempfile,pathlib
import freeze_next_inventory_v2 as freeze
class VersionedFreezeTests(unittest.TestCase):
    def test_existing_inventory_is_preserved_before_input_reads(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);target=root/'source-inventory-release007.json';target.write_bytes(b'original')
            with self.assertRaisesRegex(ValueError,'Preserve existing'):freeze.run(root,'007','missing.json',cache_generation='new-cache')
            self.assertEqual(target.read_bytes(),b'original')
    def test_cache_generation_cannot_escape_root(self):
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaisesRegex(ValueError,'escapes'):freeze.run(pathlib.Path(td),'007','missing.json',cache_generation='../outside')
if __name__=='__main__':unittest.main()
