import unittest
from interface_module_linkage import declared_versions,link,dump_directory
class Linkage(unittest.TestCase):
 def test_repository_root_and_nested_dump_hash_directories(self):
  self.assertEqual(dump_directory('aidl_api/foo/1/p/Foo.aidl'),'aidl_api/foo/1')
  self.assertEqual(dump_directory('dir/aidl_api/foo/current/p/Foo.aidl'),'dir/aidl_api/foo/current')
  self.assertIsNone(dump_directory('dir/Foo.aidl'))
 def module(self,repo='r',name='foo'):
  return dict(kind='aidl_interface',source=dict(repository=repo,revision='c',path='dir/Android.bp'),literal_properties=dict(name=name,versions=None,versions_with_info=[dict(version='1',imports=['bar-V2'])],frozen=True,unstable=False),matched_declared_paths=['dir/p/Foo.aidl'])
 def test_frozen_dump_name_version_and_imports_are_source_evidence(self):
  source=dict(repository='r',revision='c',path='dir/aidl_api/foo/1/p/Foo.aidl');result=link(source,[self.module()]);self.assertEqual(result['status'],'frozen-dump-declared-version-linked');self.assertEqual(result['declared_versions'][0]['imports'],['bar-V2']);self.assertFalse(result['compiled_or_device_version_proven'])
 def test_other_repository_not_borrowed(self):
  source=dict(repository='other',revision='c',path='dir/aidl_api/foo/1/p/Foo.aidl');self.assertEqual(link(source,[self.module()])['status'],'no-proven-module-link')
 def test_current_version_not_guessed_from_latest_frozen(self):
  source=dict(repository='r',revision='c',path='dir/p/Foo.aidl');result=link(source,[self.module()]);self.assertEqual(result['status'],'literal-current-source-linked');self.assertIsNone(result['frozen_dump_version']);self.assertFalse(result['compiled_or_device_version_proven'])
 def test_development_dump_current_is_not_version2(self):
  source=dict(repository='r',revision='c',path='dir/aidl_api/foo/current/p/Foo.aidl');result=link(source,[self.module()]);self.assertEqual(result['status'],'development-dump-linked-version-unknown');self.assertEqual(result['frozen_dump_version'],'current')
 def test_duplicate_module_names_remain_ambiguous(self):
  source=dict(repository='r',revision='c',path='dir/p/Foo.aidl');self.assertEqual(link(source,[self.module(),self.module()])['status'],'ambiguous-module-source-link')
