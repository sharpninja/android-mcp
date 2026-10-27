import unittest,io
from production_reconciliation import source_key, shape_key, legacy_variant_candidates, manifest_elements, verify_json_streams, verify_table_rows

class ReconciliationTests(unittest.TestCase):
    def test_source_identity_does_not_merge_distinct_repositories(self):
        a=dict(repository='a',revision='c',path='I.aidl',sha256='x')
        b=dict(a,repository='b')
        self.assertNotEqual(source_key(a),source_key(b))
    def test_return_shape_variant_is_retained(self):
        rows=[('id',dict(kind='method',arguments=[],return_type=t)) for t in ('int','String')]
        self.assertEqual(legacy_variant_candidates(rows),{'id'})
    def test_permission_only_difference_is_not_declaration_shape(self):
        rows=[('id',dict(kind='method',arguments=[],permission=dict(op=op))) for op in ('unknown','manual')]
        self.assertEqual(legacy_variant_candidates(rows),set())
    def test_duplicate_manifest_elements_are_counted_separately(self):
        raw=b'<manifest xmlns:android="http://schemas.android.com/apk/res/android"><permission android:name="p"/><permission android:name="p" android:protectionLevel="signature"/></manifest>'
        names=manifest_elements(raw)
        self.assertEqual(names['p'],2)
    def test_stream_json_preserves_all_entries_and_order(self):
        self.assertEqual(verify_json_streams(io.BytesIO(b'{"x":1}\n{"x":2}\n'),io.BytesIO(b'{"schema_version":"1.2.0","complete":false,"entries":[{"x":1},{"x":2}]}')),2)
    def test_stream_json_rejects_missing_final_entry(self):
        with self.assertRaises(ValueError):verify_json_streams(io.BytesIO(b'{"x":1}\n{"x":2}\n'),io.BytesIO(b'{"schema_version":"1.2.0","complete":false,"entries":[{"x":1}]}'))
    def test_table_export_rejects_same_count_changed_value(self):
        with self.assertRaises(ValueError):verify_table_rows([{'id':1,'value':'original'}],io.StringIO('{"id":1,"value":"changed"}\n'))
    def test_table_export_rejects_extra_row(self):
        with self.assertRaises(ValueError):verify_table_rows([{'id':1}],io.StringIO('{"id":1}\n{"id":2}\n'))

if __name__=='__main__':unittest.main()
