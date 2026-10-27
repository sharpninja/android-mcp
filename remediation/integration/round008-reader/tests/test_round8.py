"""Known structural corruption must not masquerade as future semantics."""
import pathlib,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import jsonschema,permission_reader

class Round8(unittest.TestCase):
    def test_reader_rejects_nonobject_or_missing_typed_operator(self):
        for raw in (None,False,42,[],{}, {'op':None},{'op':17}):
            with self.subTest(raw=raw),self.assertRaises(jsonschema.ValidationError):
                permission_reader.read(raw,'1.9.0')

    def test_reader_rejects_corrupt_known_semantic_child(self):
        for raw in ({'op':'all','children':[None]},
                    {'op':'any','children':[{'op':False}]},
                    {'op':'conditional','condition':False,'requirement':{'op':'permission','name':'android.permission.CAMERA'}}):
            with self.subTest(raw=raw),self.assertRaises(jsonschema.ValidationError):
                permission_reader.read(raw,'1.9.0')

    def test_reader_quarantines_only_typed_future_operator_and_preserves_metadata(self):
        child={'op':'future-operator','children':[None],'vendor':17}
        raw={'op':'all','children':[child],'x-evidence':{'children':[None],'op':False}}
        result=permission_reader.read(raw,'1.9.0')
        self.assertEqual(result['children'][0]['op'],'unknown')
        self.assertEqual(result['children'][0]['x-raw-node'],child)
        self.assertEqual(result['x-evidence'],raw['x-evidence'])
        self.assertEqual(raw['children'][0],child)

if __name__=='__main__':unittest.main()
