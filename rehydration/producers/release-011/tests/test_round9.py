"""Public AIDL arrays/generics remain token-delimited without added whitespace."""
import pathlib,sys,tempfile,unittest,json
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import parsers,build_catalog,test_round8

class Round9(unittest.TestCase):
    def test_bracket_and_generic_parameter_name_boundaries(self):
        row=parsers.member('void f(in int[]items, in List<String>names)','a.I','a',True)
        self.assertEqual([(p['type'],p['name']) for p in row['parameters']],[('int[]','items'),('List<String>','names')])
    def test_bracket_and_generic_field_name_boundaries_publish(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_round8.Review8().fixture(root,'package a; parcelable P { int []items = {}; List<String>names; }');build_catalog.build(inv,root/'out')
            docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())}
            self.assertEqual(docs['aidl:a.P#items']['return_type']['text'],'int[]');self.assertEqual(docs['aidl:a.P#names']['return_type']['text'],'List<String>')
    def test_identifier_type_still_needs_a_name_boundary(self):
        for raw in ('int','intitems','in intitems'):
            with self.subTest(raw=raw),self.assertRaises(ValueError):parsers.param(raw,True)

if __name__=='__main__':unittest.main()
