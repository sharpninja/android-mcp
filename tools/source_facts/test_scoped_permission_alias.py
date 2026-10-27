import unittest,sys,pathlib
P=pathlib.Path(__file__).resolve().parents[2]/'remediation/catalog/tooling';sys.path.insert(0,str(P))
import java_source,parsers
from scoped_permission_alias import Scope,resolve
class Alias(unittest.TestCase):
 def parse(self,text):
  raw=text.encode();records=java_source.parse(raw)['records'];scope=Scope(raw,records);method=next(r for r in records if r['kind']=='method');symbols={'android.Manifest.permission.CAMERA':'android.permission.CAMERA'};return resolve(parsers.permission(method['annotations']),scope.aliases(method['owner'],symbols))
 def test_exact_static_import_resolves_unquoted_constant(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.CAMERA;\nclass A { @android.annotation.RequiresPermission(CAMERA) void f(){} }');self.assertEqual(value['name'],'android.permission.CAMERA')
 def test_quoted_short_literal_not_reinterpreted_as_constant(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.CAMERA;\nclass A { @android.annotation.RequiresPermission("CAMERA") void f(){} }');self.assertEqual(value['op'],'unknown')
 def test_declared_shadow_blocks_static_import(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.CAMERA;\nclass A { static final String CAMERA="other"; @android.annotation.RequiresPermission(CAMERA) void f(){} }');self.assertEqual(value['op'],'unknown')
 def test_inherited_scope_stays_unknown(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.CAMERA;\nclass A extends B { @android.annotation.RequiresPermission(CAMERA) void f(){} }');self.assertEqual(value['op'],'unknown')
 def test_wildcard_import_stays_unknown(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.*;\nclass A { @android.annotation.RequiresPermission(CAMERA) void f(){} }');self.assertEqual(value['op'],'unknown')
 def test_enclosing_field_shadow_blocks_static_import(self):
  value=self.parse('package p;\nimport static android.Manifest.permission.CAMERA;\nclass A { static String CAMERA="other"; class Inner { @android.annotation.RequiresPermission(CAMERA) void f(){} } }');self.assertEqual(value['op'],'unknown')
 def test_fake_comment_import_not_admitted(self):
  value=self.parse('package p;\n// import static android.Manifest.permission.CAMERA;\nclass A { @android.annotation.RequiresPermission(CAMERA) void f(){} }');self.assertEqual(value['op'],'unknown')
