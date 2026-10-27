import unittest,sys,pathlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from aidl_lex import inventory_counts
class SourceLexicalComments(unittest.TestCase):
 def test_comment_only_interface_has_no_members(self):
  counts=inventory_counts('interface IEmpty { // manual implementation\n /* no members; {} */\n }')
  self.assertEqual((counts['declarations'],counts['statements'],counts['expected_records']),(1,0,1))
 def test_comment_delimiters_do_not_create_or_split_members(self):
  counts=inventory_counts('interface IReal { /* fake(); */ void real(); // fake2();\n }')
  self.assertEqual((counts['declarations'],counts['statements'],counts['expected_records']),(1,1,2))
