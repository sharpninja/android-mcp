import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import java_context
class JavaContextTests(unittest.TestCase):
 def test_imports_retained_in_runtime_source_details(self):
  sources=[dict(kind='java-source',path='C.java',metadata_cache=dict(path='cache.gz',sha256='abc',parser_sha256='def'))]
  stats=[dict(cache_path='cache.gz',cache_sha256='abc',parser_sha256='def',package='a',imports={'List':'java.util.List'},wildcard_imports=['other'],parser='tested',parse_errors=0)]
  java_context.attach(sources,stats)
  self.assertEqual(sources[0]['java_file_context']['imports'],{'List':'java.util.List'})
  self.assertEqual(sources[0]['java_file_context']['wildcard_imports'],['other'])
 def test_mismatched_context_rejected(self):
  sources=[dict(kind='java-source',path='C.java',metadata_cache=dict(path='cache.gz',sha256='abc',parser_sha256='def'))]
  with self.assertRaisesRegex(ValueError,'context cache mismatch'):java_context.attach(sources,[dict(cache_path='cache.gz',cache_sha256='wrong',parser_sha256='def')])
