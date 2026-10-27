import unittest,tempfile,pathlib,hashlib,gzip,json
from restore_java_caches import write_cache
class SDKIndexCacheReproduction(unittest.TestCase):
    def test_known_sdk_type_cache_is_independent_of_directory_and_clock(self):
        rows=[dict(kind='type',canonical='a.A',extras=dict(target_platform_api='37.2',type_kind_status='unknown'))]
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);a=root/'first'/'api-index-current.jsonl.gz';b=root/'second'/'api-index-current.jsonl.gz'
            first=write_cache(a,rows);second=write_cache(b,rows)
            self.assertEqual(first,second);self.assertEqual(a.read_bytes()[4:8],bytes(4))
            with gzip.open(a,'rt') as stream:self.assertEqual([json.loads(line) for line in stream],rows)
if __name__=='__main__':unittest.main()
