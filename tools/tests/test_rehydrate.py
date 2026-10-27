"""Offline safety, resume and clean-path tests; never downloads the full corpus."""
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import rehydrate as r
import check_deliverables as policy


class RehydrationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='lean fixture with spaces ')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)

    def source(self, data=b'fixture\n'):
        origin = self.root / 'origin.txt'
        origin.write_bytes(data)
        return dict(path='fixture.txt', local_path='blobs/fixture', url=origin.as_uri(),
                    sha256=r.sha(data), size_bytes=len(data),
                    git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest())

    def test_fetch_resume_and_corrupt_input_is_preserved(self):
        source = self.source()
        work = self.root / 'clean workspace'
        self.assertEqual(r.acquire(source, work, True), 'downloaded')
        with patch.object(r, 'open_url', side_effect=AssertionError('unexpected I/O')):
            self.assertEqual(r.acquire(source, work, True), 'reused')
        target = work / source['local_path']
        target.write_bytes(b'corruption')
        with self.assertRaisesRegex(ValueError, 'differs from its pin'):
            r.acquire(source, work, True)
        self.assertEqual(target.read_bytes(), b'corruption')

    def test_pins_and_explicit_file_permission(self):
        source = self.source()
        with self.assertRaisesRegex(ValueError, 'Only HTTPS'):
            r.acquire(source, self.root / 'work')
        source['git_blob'] = '0'*40
        with self.assertRaisesRegex(ValueError, 'content address'):
            r.acquire(source, self.root / 'work', True)
        self.assertFalse((self.root/'work'/source['local_path']).exists())

    def test_archive_member_pin_and_archive_pin(self):
        package = self.root/'fixture.zip'
        with zipfile.ZipFile(package, 'w') as z:
            z.writestr('safe/api.txt', b'API fixture')
            z.writestr('../unselected', b'never extracted')
        source = self.source(b'API fixture')
        source['transport'] = dict(archive_url=package.as_uri(), archive_member='safe/api.txt', archive_sha256=r.file_sha(package))
        work = self.root/'work'
        self.assertEqual(r.acquire(source, work, True), 'downloaded')
        self.assertFalse((self.root/'unselected').exists())
        source['transport']['archive_sha256']='0'*64
        with self.assertRaisesRegex(ValueError, 'archive SHA-256'):
            r.acquire(source, self.root/'other', True)

    def test_paths_symlinks_and_workspace_binding(self):
        for name in ('../escape', '/absolute', 'a\\b', '.'):
            with self.assertRaises(ValueError): r.safe_path(self.root, name)
        (self.root/'link').symlink_to(self.root.parent, target_is_directory=True)
        with self.assertRaises(ValueError): r.safe_path(self.root, 'link/escape')
        recipe = dict(inventory={'sources':[self.source()]}, release='fixture')
        work=self.root/'bound'
        r.save_inventory(recipe, work, recipe['inventory'])
        changed=copy.deepcopy(recipe); changed['release']='different'
        with self.assertRaisesRegex(ValueError, 'different recipe'): r.load_inventory(changed, work)

    def test_default_help_and_fixture_plan_have_no_side_effects(self):
        source=self.source()
        recipe=self.root/'recipe.json'
        recipe.write_text(json.dumps(dict(recipe_version=1, release='fixture', inventory={'sources':[source]})))
        work=self.root/'not created'
        with patch.object(r, 'open_url', side_effect=AssertionError('network forbidden')), contextlib.redirect_stdout(io.StringIO()) as output:
            r.main(['plan','--recipe',str(recipe),'--workspace',str(work)])
        self.assertFalse(work.exists())
        self.assertFalse(json.loads(output.getvalue())['complete'])
        result=subprocess.run([sys.executable,str(TOOLS/'rehydrate.py'),'--help'],capture_output=True,text=True)
        self.assertEqual(result.returncode,0); self.assertIn('--strict-cache-bytes', result.stdout)

    def test_allowlist_rejects_data_and_symlinks(self):
        spec=json.loads((TOOLS.parent/'deliverables.json').read_text())
        self.assertEqual(policy.violations(self.root,['README.md','tools/rehydrate.py'],spec),[])
        for path in ('source-project/cache.sqlite','remediation/catalog/tests/data.sqlite','remediation/design-c4/build/old.svg','old-review.zip'):
            self.assertTrue(policy.violations(self.root,[path],spec),path)
        (self.root/'README.md').symlink_to(self.root/'missing')
        self.assertTrue(policy.violations(self.root,['README.md'],spec))

    def test_seed_imports_pinned_snapshot_without_changing_original(self):
        source=self.source()
        source['transport']={'immutable_url':False,'required_local_snapshot':'docs/snapshot.html'}
        bundle=self.root/'bundle';(bundle/'docs').mkdir(parents=True)
        original=bundle/'docs/snapshot.html';original.write_bytes(b'fixture\n')
        recipe=self.root/'seed.json'
        recipe.write_text(json.dumps(dict(recipe_version=1,release='fixture',inventory={'sources':[source]})))
        work=self.root/'seeded'
        for _ in range(2):
            with patch.object(r,'open_url',side_effect=AssertionError('network')),contextlib.redirect_stdout(io.StringIO()):
                r.main(['seed','--recipe',str(recipe),'--workspace',str(work),'--from-dir',str(bundle)])
        self.assertEqual((work/source['local_path']).read_bytes(),b'fixture\n')
        self.assertEqual(original.read_bytes(),b'fixture\n')

    def test_range_reader_refuses_unbounded_server_response(self):
        response=unittest.mock.MagicMock()
        response.__enter__.return_value=response
        response.status=200;response.headers={};response.read.return_value=b'archive'
        with patch.object(r,'open_url',return_value=response):
            with self.assertRaisesRegex(ValueError,'exact HTTP byte ranges'): r.RangeReader('https://fixture.invalid/archive.zip')

    def test_cache_regeneration_records_differences_and_resumes(self):
        producer=TOOLS.parent/'rehydration/producers/release-011'
        sys.path.insert(0,str(producer/'tooling'))
        import cache_fingerprint
        source=self.source(b'// intentionally empty Java fixture\n')
        source.update(kind='java-source',path='Empty.java',metadata_cache=dict(path='cache/empty.jsonl.gz',sha256='0'*64,parser_sha256=cache_fingerprint.current('java-source'),records=0))
        files={str(p.relative_to(producer)):r.file_sha(p) for p in producer.rglob('*') if p.is_file() and '__pycache__' not in p.parts}
        recipe=dict(recipe_version=1,release='fixture',inventory={'sources':[source]},producer={'path':str(producer.relative_to(TOOLS.parent)),'files':files},environment={})
        work=self.root/'caches';r.acquire(source,work,True)
        with self.assertRaisesRegex(ValueError,'encoding bytes differ'):
            r.extract(recipe,work,strict_cache_bytes=True)
        self.assertFalse((work/'cache/empty.jsonl.gz').exists())
        result=r.extract(recipe,work)
        self.assertEqual(result['regenerated_cache_encoding_differences'],1)
        self.assertTrue((work/'cache/empty.jsonl.gz.rehydration.json').is_file())
        self.assertEqual(r.extract(recipe,work)['regenerated_cache_encoding_differences'],0)

    def test_real_producer_end_to_end_in_clean_path(self):
        """Three tiny API/manifest/AIDL inputs exercise actual producer and validator."""
        producer=TOOLS.parent/'rehydration/producers/release-011'
        self.assertTrue(producer.is_dir(), 'Run against the assembled lean checkout')
        spec=importlib.util.spec_from_file_location('fixture_build_test',producer/'tests/test_build.py')
        fixture=importlib.util.module_from_spec(spec);spec.loader.exec_module(fixture)
        origin=self.root/'origin';origin.mkdir()
        inventory=json.loads(fixture.BuildTests().fixture(origin).read_text())
        for source in inventory['sources']:
            source['url']=(origin/source['local_path']).as_uri()
        files={str(p.relative_to(producer)):r.file_sha(p) for p in producer.rglob('*') if p.is_file() and '__pycache__' not in p.parts}
        recipe=dict(recipe_version=1,release='fixture',inventory=inventory,
                    producer={'path':str(producer.relative_to(TOOLS.parent)),'files':files},environment={})
        path=self.root/'recipe.json';path.write_text(json.dumps(recipe))
        work=self.root/'fresh target'
        for command in ('fetch','fetch','build','validate','export','build','validate','export'):
            result=subprocess.run([sys.executable,str(TOOLS/'rehydrate.py'),command,'--recipe',str(path),'--workspace',str(work),'--allow-file','--delay','0'],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stdout+'\n'+result.stderr)
        self.assertEqual(json.loads((work/'fetch-receipt.json').read_text())['reused'],3)
        with sqlite3.connect(work/'releases/fixture/catalog.sqlite') as db:
            self.assertEqual(db.execute('pragma integrity_check').fetchone()[0],'ok')
            self.assertEqual(db.execute('select count(*) from permission_definitions').fetchone()[0],1)
        report=work/'releases/fixture.validation.json';report.unlink()
        result=subprocess.run([sys.executable,str(TOOLS/'rehydrate.py'),'validate','--recipe',str(path),'--workspace',str(work)],capture_output=True,text=True)
        self.assertNotEqual(result.returncode,0); self.assertIn('missing or changed',result.stderr)


if __name__=='__main__': unittest.main()
