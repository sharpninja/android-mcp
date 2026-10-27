import unittest,tempfile,pathlib,sys,gzip,json,hashlib
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import java_source,parsers
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import stage_sdk372_sources as stage
class JavaCacheTests(unittest.TestCase):
 def test_stage_uses_this_candidates_parser(self):
  self.assertEqual(pathlib.Path(stage.java_source.__file__).resolve(),(P/'tooling/java_source.py').resolve())
 def test_truncated_cache_quarantined_without_deleting_evidence(self):
  old=(stage.R,stage.D,stage.CACHE)
  try:
   with tempfile.TemporaryDirectory() as td:
    stage.R=stage.D=pathlib.Path(td);stage.CACHE=stage.D/'java-cache';stage.CACHE.mkdir()
    cache=stage.CACHE/'bad.jsonl.gz';cache.write_bytes(b'')
    moved=stage.quarantine_incomplete()
    self.assertEqual(len(moved),1);self.assertFalse(cache.exists())
    self.assertEqual((stage.R/moved[0]['quarantine']).read_bytes(),b'')
  finally:stage.R,stage.D,stage.CACHE=old
 def test_crc_record_count_and_hash_are_checked(self):
  with tempfile.TemporaryDirectory() as td:
   p=pathlib.Path(td)/'ok.gz'
   with gzip.open(p,'wt') as f:f.write('{"name":"x"}\n')
   stats=dict(records=1,cache_sha256=hashlib.sha256(p.read_bytes()).hexdigest())
   self.assertTrue(stage.verify_cache(p,stats));self.assertFalse(stage.verify_cache(p,dict(stats,records=2)))
   p.write_bytes(p.read_bytes()[:-4]);self.assertFalse(stage.verify_cache(p,stats))
