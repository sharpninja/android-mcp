import unittest,tempfile,pathlib,json,hashlib
from chunk_artifact import split,reassemble,digest

class ReleaseParts(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(prefix='android-mcp-parts-',dir='/tmp');self.addCleanup(self.tmp.cleanup);self.root=pathlib.Path(self.tmp.name)
        self.source=self.root/'sealed.bin';self.data=bytes(range(256))*31+b'final';self.source.write_bytes(self.data)
    def manifest(self,size=377):
        split(self.source,self.root/'parts',digest(self.source),size)
        return self.root/'parts/manifest.json'
    def test_reassembly_retains_exact_original_bytes_with_final_short_part(self):
        m=self.manifest();out=self.root/'new.bin';result=reassemble(m,out,digest(m));self.assertEqual(self.data,out.read_bytes());self.assertEqual(len(self.data),result['bytes']);self.assertLess(json.loads(m.read_text())['parts'][-1]['bytes'],377)
    def test_tampered_part_never_becomes_published_output(self):
        m=self.manifest();part=self.root/'parts/artifact.part-0002';part.write_bytes(b'X'*part.stat().st_size);out=self.root/'new.bin'
        with self.assertRaisesRegex(ValueError,'Part hash mismatch'):reassemble(m,out,digest(m))
        self.assertFalse(out.exists())
    def test_wrong_sealed_hash_produces_no_manifest(self):
        with self.assertRaisesRegex(ValueError,'trusted sealed hash'):split(self.source,self.root/'parts','0'*64,377)
        self.assertFalse((self.root/'parts/manifest.json').exists());self.assertEqual(self.data,self.source.read_bytes())
    def test_path_injection_and_manifest_tampering_rejected(self):
        m=self.manifest();original=digest(m);data=json.loads(m.read_text());data['parts'][0]['name']='../outside';m.write_text(json.dumps(data));out=self.root/'new.bin'
        with self.assertRaisesRegex(ValueError,'Manifest hash mismatch'):reassemble(m,out,original)
        with self.assertRaisesRegex(ValueError,'part path'):reassemble(m,out,digest(m))
        self.assertFalse(out.exists())
    def test_existing_destination_never_overwritten(self):
        m=self.manifest();out=self.root/'existing';out.write_bytes(b'keep')
        with self.assertRaises(FileExistsError):reassemble(m,out,digest(m))
        self.assertEqual(b'keep',out.read_bytes())
    def test_symlink_part_is_rejected(self):
        m=self.manifest();part=self.root/'parts/artifact.part-0001';content=part.read_bytes();part.unlink();outside=self.root/'other';outside.write_bytes(content);part.symlink_to(outside)
        with self.assertRaisesRegex(ValueError,'regular part'):reassemble(m,self.root/'new.bin',digest(m))
    def test_two_gib_part_is_rejected_without_reading_input(self):
        with self.assertRaisesRegex(ValueError,'below2GiB'):split(self.source,self.root/'parts',digest(self.source),2*1024**3)
        self.assertFalse((self.root/'parts').exists())
    def test_empty_artifact_round_trips(self):
        self.source.write_bytes(b'');m=self.manifest();out=self.root/'empty';reassemble(m,out,digest(m));self.assertEqual(b'',out.read_bytes())

if __name__=='__main__':unittest.main()
