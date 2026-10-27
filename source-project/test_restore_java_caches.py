import unittest,tempfile,pathlib,json,gzip,hashlib
from restore_java_caches import write_cache

class CacheRestoreTests(unittest.TestCase):
    def test_identical_basename_has_identical_bytes_in_different_roots(self):
        with tempfile.TemporaryDirectory() as d:
            a=pathlib.Path(d)/'a'/'blob.jsonl.gz';b=pathlib.Path(d)/'b'/'blob.jsonl.gz'
            records=[dict(kind='method',name='m')]
            write_cache(a,records);write_cache(b,records)
            self.assertEqual(a.read_bytes(),b.read_bytes())
    def test_wrong_expected_digest_never_publishes(self):
        with tempfile.TemporaryDirectory() as d:
            p=pathlib.Path(d)/'blob.jsonl.gz'
            with self.assertRaises(ValueError):write_cache(p,[dict(kind='field')],'0'*64)
            self.assertFalse(p.exists());self.assertTrue(p.with_suffix('.gz.tmp').exists())
    def test_existing_target_not_overwritten(self):
        with tempfile.TemporaryDirectory() as d:
            p=pathlib.Path(d)/'blob.jsonl.gz';p.write_bytes(b'evidence')
            with self.assertRaises(FileExistsError):write_cache(p,[])
            self.assertEqual(p.read_bytes(),b'evidence')

if __name__=='__main__':unittest.main()
