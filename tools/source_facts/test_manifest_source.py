import unittest,xml.etree.ElementTree as ET
from manifest_source import parse
class Manifest(unittest.TestCase):
 def test_late_declaration_after_license_comment_is_recorded(self):
  raw=b'<!-- license -->\n<?xml version="1.0" encoding="utf-8"?>\n<manifest><permission name="p"/></manifest>'
  root,receipt=parse(raw);self.assertEqual(len(list(root.iter('permission'))),1);self.assertEqual(receipt['normalization']['kind'],'late-utf8-xml-declaration-masked-after-comments')
 def test_standard_manifest_unchanged(self):
  root,receipt=parse(b'<?xml version="1.0"?><manifest/>');self.assertIsNone(receipt['normalization']);self.assertEqual(root.tag,'manifest')
 def test_declaration_inside_comment_not_interpreted(self):
  root,receipt=parse(b'<!-- <?xml version="1.0"?> --><manifest/>');self.assertIsNone(receipt['normalization'])
 def test_noncomment_content_before_declaration_not_repaired(self):
  with self.assertRaises(ET.ParseError):parse(b'broken <?xml version="1.0"?><manifest/>')
 def test_empty_source_not_claimed_valid_manifest(self):
  root,receipt=parse(b'');self.assertIsNone(root);self.assertFalse(receipt['valid_android_manifest']);self.assertEqual(receipt['permission_elements'],0)
 def test_binary_xml_not_decoded_as_utf8_with_replacement(self):
  with self.assertRaisesRegex(ET.ParseError,'binary XML'):parse(b'\x03\x00\x08\x00binary')
