import unittest
from interface_applicability_evidence import produce

class Evidence(unittest.TestCase):
    def source(self):return dict(repository='r',revision='c',path='aidl_api/foo/1/p/Foo.aidl',sha256='a'*64)
    def linked(self):
        return dict(build_evidence=dict(repository='r',revision='c',path='Android.bp',sha256='b'*64),
            build_module_name='foo',module_kind='aidl_interface',frozen_dump_version='1',
            declared_versions=[dict(version='1',imports=['bar-V2'])],
            declared_flags=dict(apex_available=['com.android.foo','//apex_available:platform']),
            published_hash_lines=[dict(source=dict(repository='r',revision='c',path='aidl_api/foo/1/.hash',sha256='c'*64),line=1,literal='d'*40,format='published-aidl-hash-hex40')])
    def test_real_source_facts_preserved_without_fabricating_module_range(self):
        result=produce(self.source(),self.linked())
        self.assertEqual(result['declared_module']['declared_versions'][0]['imports'],['bar-V2'])
        self.assertEqual(result['interface_candidates'][0]['published_hash'],'d'*40)
        self.assertIsNone(result['interface_candidates'][0]['descriptor'])
        self.assertIsNone(result['module_candidates'][0]['version_range'])
        self.assertIn('unknown_reason',result['applicability'])
        self.assertFalse(result['runtime_observation'])
    def test_cross_revision_hash_not_borrowed(self):
        value=self.linked();value['published_hash_lines'][0]['source']['revision']='other'
        with self.assertRaises(ValueError):produce(self.source(),value)
    def test_no_module_and_no_hash_remain_named_unknowns(self):
        result=produce(self.source(),dict(status='no-proven-module-link'))
        self.assertEqual(len(result['unknown_reasons']),2)
        self.assertEqual(result['interface_candidates'],[])
    def test_hidl_package_version_not_android_api_or_integer_interface_version(self):
        fact=dict(source=dict(repository='r',revision='c',path='current.txt',sha256='e'*64),
            declared_identifier='android.hardware.foo@1.0::IFoo',literal='f'*64,
            format='published-hidl-hash-hex64-and-identifier',line=2)
        result=produce(self.source(),{},[fact]);item=result['interface_candidates'][0]
        self.assertEqual(item['descriptor'],fact['declared_identifier']);self.assertIsNone(item['minimum_version'])
        self.assertNotIn('platform',result['applicability'])
    def test_source_and_build_must_share_exact_revision(self):
        value=self.linked();value['build_evidence']['revision']='other'
        with self.assertRaises(ValueError):produce(self.source(),value)
