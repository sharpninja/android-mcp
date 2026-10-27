import unittest,pathlib,sys,tempfile,hashlib,json,zipfile
from unittest.mock import patch
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import restore_snapshot as restore
class RestoreTests(unittest.TestCase):
 def test_source_zip_member_verified_without_network_or_install(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);archive=root/'sources.zip'
   with zipfile.ZipFile(archive,'w') as z:z.writestr('src/a/C.java','package a; class C{}')
   data=archive.read_bytes();s=dict(transport=dict(archive_local_path='sources.zip',archive_sha256=hashlib.sha256(data).hexdigest(),archive_sha1=hashlib.sha1(data).hexdigest(),archive_member='src/a/C.java'))
   with patch.object(restore,'get',side_effect=AssertionError('Network forbidden in fixture')):
    self.assertEqual(restore.restore_member(s,root),b'package a; class C{}')
   archive.write_bytes(b'tampered')
   with self.assertRaisesRegex(ValueError,'Existing source archive hash mismatch'):restore.restore_member(s,root)
 def test_missing_source_default_is_dry_run(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=root/'inventory.json';inv.write_text(json.dumps(dict(sources=[dict(status='downloaded',git_blob='1'*40,local_path='snapshots/missing')])) )
   with patch.object(restore,'get',side_effect=AssertionError('No network during dry run')):restore.main(inv)
   self.assertFalse((root/'snapshots/missing').exists())
