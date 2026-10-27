import unittest
from deeper_scope_audit import classify_tree
class ScopeEvidenceTests(unittest.TestCase):
    def test_hidl_paths_are_positive_candidates_not_irrelevance(self):
        result=classify_tree([dict(kind='blob',path='1.0/IFoo.hal',oid='a')])
        self.assertEqual(result['scope_bucket'],'hidl-filename-candidates');self.assertEqual(result['candidate_paths'][0]['git_blob'],'a')
    def test_native_source_absence_of_binder_name_stays_unknown(self):
        result=classify_tree([dict(kind='blob',path='src/codec.cpp',oid='b')])
        self.assertEqual(result['scope_bucket'],'source-content-uninspected');self.assertFalse(result['semantic_irrelevance_proven'])
    def test_binary_only_tree_is_not_empty(self):
        result=classify_tree([dict(kind='blob',path='lib/core.jar',oid='c')])
        self.assertEqual(result['scope_bucket'],'prebuilt-or-other-uninspected');self.assertFalse(result['semantic_irrelevance_proven'])
if __name__=='__main__':unittest.main()
