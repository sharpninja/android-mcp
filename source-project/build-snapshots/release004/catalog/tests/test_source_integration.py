import unittest,pathlib,tempfile,json,hashlib,sqlite3,sys,zipfile,gzip
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog as b
import test_build as base_module
class SourceIntegrationTests(unittest.TestCase):
    fixture=base_module.BuildTests.fixture

    def test_cached_java_and_current_index_import_with_distinct_evidence(self):
        import java_source, sdk_api_index
        with tempfile.TemporaryDirectory() as td:
            r = pathlib.Path(td)
            inv = self.fixture(r)
            spec = json.loads(inv.read_text())
            for kind, name, data, parser in [('java-source', 'C.java', b'package a; public class C { @android.annotation.RequiresPermission("android.permission.CAMERA") public void f(int x){} }', lambda b: java_source.parse(b)['records']), ('sdk-api-index', 'api.xml', b'<api version="4"><class name="a/C"><method name="f(I)V"/><field name="VALUE"/></class></api>', lambda b: sdk_api_index.parse(b)['records'])]:
                (r / name).write_bytes(data)
                cache = r / (name + '.gz')
                records = parser(data)
                with gzip.open(cache, 'wt') as f:
                    for row in records:
                        f.write(json.dumps(row) + '\n')
                source = dict(spec['sources'][0], kind=kind, path=name, local_path=name, sha256=hashlib.sha256(data).hexdigest(), git_blob=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\x00' + data).hexdigest(), size_bytes=len(data), url='https://dl.google.com/mock/' + name, metadata_cache=dict(path=cache.name, sha256=hashlib.sha256(cache.read_bytes()).hexdigest(), records=len(records)))
                spec['sources'].append(source)
            inv.write_text(json.dumps(spec))
            v = b.build(inv, r / 'out')
            self.assertEqual(v['counts']['sources'], 5)
            docs = [json.loads(l) for l in (r / 'out/entries.jsonl').read_text().splitlines()]
            self.assertTrue(any((d['canonical_signature'] == 'jvm:a/C#f(I)V' for d in docs)))
            java = next((d for d in docs if d['canonical_signature'] == 'java-source:a.C#f(int)'))
            self.assertEqual(java['permission']['name'], 'android.permission.CAMERA')
            jvm = next((d for d in docs if d['canonical_signature'] == 'jvm:a/C#f(I)V'))
            self.assertEqual(jvm['permission']['name'], 'android.permission.CAMERA')
            self.assertEqual(java['extensions']['proven_current_sdk_jvm_identity'], 'a/C#f(I)V')

    def test_cache_hash_failure_does_not_publish(self):
        with tempfile.TemporaryDirectory() as td:
            r = pathlib.Path(td)
            inv = self.fixture(r)
            spec = json.loads(inv.read_text())
            s = spec['sources'][0]
            s['kind'] = 'java-source'
            s['metadata_cache'] = dict(path='bad.gz', sha256='0' * 64, records=0)
            (r / 'bad.gz').write_bytes(b'broken')
            inv.write_text(json.dumps(spec))
            with self.assertRaisesRegex(ValueError, 'Metadata cache SHA256 mismatch'):
                b.build(inv, r / 'out')
            self.assertFalse((r / 'out/catalog.sqlite').exists())

    def test_malformed_aidl_is_preserved_as_unknown(self):
        with tempfile.TemporaryDirectory() as td:
            r = pathlib.Path(td)
            inv = self.fixture(r)
            spec = json.loads(inv.read_text())
            s = spec['sources'][2]
            data = b'package a; @Broken( interface IFoo {'
            (r / 'IFoo.aidl').write_bytes(data)
            s.update(sha256=hashlib.sha256(data).hexdigest(), git_blob=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\x00' + data).hexdigest(), size_bytes=len(data))
            inv.write_text(json.dumps(spec))
            b.build(inv, r / 'out')
            db = sqlite3.connect(r / 'out/catalog.sqlite')
            row = db.execute('SELECT raw_schema,parse_status FROM aidl_schemas').fetchone()
            self.assertEqual(row, (data.decode(), 'unsupported'))
            self.assertGreater(db.execute('SELECT count(*) FROM issues').fetchone()[0], 0)
            db.close()
