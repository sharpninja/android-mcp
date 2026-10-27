import unittest
from freeze_next_inventory import relocate,normalize_api,source_locator,replace_index_cache
class NextInventoryTests(unittest.TestCase):
    def test_cache_paths_relocated_without_relabeling_producer(self):
        source=dict(metadata_cache=dict(path='sdk37.2/cache/a.gz',parser_sha256='original'),local_path='snapshots/blobs/a',api_level='37.2')
        relocated=relocate(source,'cache-build-round005')
        self.assertEqual(relocated['metadata_cache']['path'],'cache-build-round005/sdk37.2/cache/a.gz')
        self.assertEqual(relocated['metadata_cache']['parser_sha256'],'original')
        self.assertEqual(source['metadata_cache']['path'],'sdk37.2/cache/a.gz')
    def test_platform_minor_and_package_revision_are_distinct(self):
        result=normalize_api(dict(api_level='37.2',sdk_package_revision=1,sdk_extension_level=24))
        self.assertEqual((result['api_level'],result['platform_api_version'],result['sdk_package_revision'],result['sdk_extension_level']),(37,'37.2',1,24))
    def test_relocation_rejects_escape(self):
        with self.assertRaises(ValueError):relocate(dict(local_path='../escape'),'cache')
    def test_source_locator_includes_revision(self):
        self.assertNotEqual(source_locator(dict(repository='r',revision='a',path='p')),source_locator(dict(repository='r',revision='b',path='p')))
    def test_cache_override_requires_same_source_and_platform(self):
        source=dict(sha256='source',platform_api_version='37.2',metadata_cache={'sha256':'old'})
        proof=dict(platform_api='37.2',payload_identical_to_frozen_generation=True,byte_reproducibility_verified=True,cache=dict(source_sha256='source',sha256='new'))
        result=replace_index_cache(source,proof)
        self.assertEqual(result['metadata_cache']['sha256'],'new');self.assertEqual(source['metadata_cache']['sha256'],'old')
        proof['platform_api']='37.3'
        with self.assertRaises(ValueError):replace_index_cache(source,proof)
if __name__=='__main__':unittest.main()
