import unittest
from map_native_search_sources import project_for_path
class NativeSourcePins(unittest.TestCase):
 def test_longest_manifest_project_wins(self):
  rows=[dict(path='a',name='root'),dict(path='a/b',name='nested')]
  self.assertEqual(project_for_path(rows,'a/b/file.cpp')['name'],'nested')
 def test_prefix_without_directory_boundary_does_not_match(self):
  self.assertIsNone(project_for_path([dict(path='a/b',name='one')],'a/bc/file.cpp'))
 def test_unmapped_path_remains_unknown(self):self.assertIsNone(project_for_path([], 'x/file.cpp'))
