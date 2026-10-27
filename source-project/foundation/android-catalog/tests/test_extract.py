import importlib.util, unittest, tempfile, pathlib, json, sqlite3
P=pathlib.Path(__file__).resolve().parents[1]
s=importlib.util.spec_from_file_location('extract',P/'tooling/extract.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)

class ExtractTests(unittest.TestCase):
    def test_nested_arguments(self):
        self.assertEqual(m.split_top('java.util.Map<java.lang.String,java.lang.Integer>, int[]'),['java.util.Map<java.lang.String,java.lang.Integer>','int[]'])
    def test_permission_modes(self):
        for annotation,expected in [('@RequiresPermission(allOf={"a","b"})','all'),('@RequiresPermission(anyOf={"a","b"})','any'),('@RequiresPermission(value="a", conditional=true)','conditional'),('@PermissionManuallyEnforced','manual'),('@RequiresNoPermission','explicit-none'),('','unknown')]:
            self.assertEqual(m.permission_logic(m.annotations(annotation)[1])[0][1],expected)
    def test_nested_read(self):
        logic=m.permission_logic(m.annotations('@android.annotation.RequiresPermission.Read(@RequiresPermission(anyOf={"a","b"}))')[1])[0]
        self.assertEqual(logic[2]['access'],'read');self.assertIn('anyOf',logic[3])
    def test_overloads_and_ids(self):
        a,_=m.parse_signature('method public void run(java.lang.String);','test','test.Example')
        b,_=m.parse_signature('method public void run(int);','test','test.Example')
        self.assertNotEqual(a['signature'],b['signature'])
        self.assertEqual(a['signature'],'test.Example#run(java.lang.String)')
    def test_ingestion(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp)
            fixtures={'api.txt':('metalava','''// Signature format: 2.0
package test {
  public class Example {
    ctor public Example();
    method @RequiresPermission(allOf={"a","b"}) public void both(int);
    method @RequiresPermission(anyOf={"a","b"}) public void either();
    method @PermissionManuallyEnforced public void manual();
    method public void unknown();
    method @RequiresNoPermission public int free();
    field public static final java.lang.String VALUE = "value";
    unsupported future declaration;
  }
}
'''),'manifest.xml':('manifest','<manifest xmlns:android="http://schemas.android.com/apk/res/android"><permission android:name="a" android:protectionLevel="signature|privileged" /></manifest>'),'ITest.aidl':('aidl','package test; interface ITest { void run(); }')}
            sources=[]
            for filename,(kind,text) in fixtures.items():
                (root/filename).write_text(text)
                sources.append(dict(path=filename,url='fixture://'+filename,revision='0'*40,retrieved_at='2026-10-26T00:00:00Z',sha256=m.digest(text.encode()),api_level=37,channel='stable',kind=kind,license='synthetic test fixture'))
            (root/'inventory.json').write_text(json.dumps({'sources':sources}))
            result=m.run(root/'inventory.json',root/'result')
            self.assertEqual(result['integrity_check'],'ok');self.assertEqual(result['foreign_key_check'],[])
            self.assertEqual(result['counts']['bindings'],9)
            self.assertEqual(result['counts']['issues'],2)
            self.assertEqual(result['reconciliation']['signature_declarations'],8)
            db=sqlite3.connect(root/'result/catalog.sqlite')
            for mode in ('all','any','manual','unknown','explicit-none'):
                self.assertGreater(db.execute('SELECT count(*) FROM permission_relations WHERE logic=?',(mode,)).fetchone()[0],0)
            with self.assertRaises(sqlite3.IntegrityError):db.execute('DELETE FROM bindings')
            db.close()

if __name__=='__main__':unittest.main()
