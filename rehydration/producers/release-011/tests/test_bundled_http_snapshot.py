import unittest,tempfile,pathlib,sys,hashlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from restore_snapshot import restore_bundled_snapshot
class BundledSnapshot(unittest.TestCase):
 def fixture(self,root):
  data=b'exact old HTTP response';(root/'reference.html').write_bytes(data)
  return dict(path='reference.html',sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),transport=dict(required_local_snapshot='reference.html'))
 def test_exact_snapshot_no_network(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);s=self.fixture(root);self.assertEqual(restore_bundled_snapshot(s,root),b'exact old HTTP response')
 def test_missing_snapshot_fails_without_mutable_url_fetch(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);s=self.fixture(root);(root/'reference.html').unlink()
   with self.assertRaises(FileNotFoundError):restore_bundled_snapshot(s,root)
 def test_changed_bytes_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);s=self.fixture(root);(root/'reference.html').write_bytes(b'new version')
   with self.assertRaises(ValueError):restore_bundled_snapshot(s,root)
 def test_escape_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);s=self.fixture(root);s['transport']['required_local_snapshot']='../elsewhere'
   with self.assertRaises(ValueError):restore_bundled_snapshot(s,root)
