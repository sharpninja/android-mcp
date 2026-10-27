import unittest,io,zipfile,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
try:import sdk_zip_metadata as m
except ImportError:m=None
class MetadataRangeTests(unittest.TestCase):
 def test_only_selected_metadata_payload_is_read(self):
  self.assertIsNotNone(m)
  b=io.BytesIO()
  with zipfile.ZipFile(b,'w',zipfile.ZIP_STORED) as z:z.writestr('android.jar',b'UNSELECTED_BINARY'*10000);z.writestr('data/api-versions.xml','<api version="3"/>');z.writestr('source.properties','AndroidVersion.ApiLevel=37\nAndroidVersion.ApiMinor=2\n')
  raw=b.getvalue();calls=[]
  def get(start,count):calls.append((start,count));return raw[start:start+count]
  files,entries=m.extract_metadata(len(raw),get)
  self.assertEqual(files['data/api-versions.xml'],b'<api version="3"/>');self.assertNotIn('android.jar',files)
  self.assertLess(sum(n for _,n in calls),10000)
if __name__=='__main__':unittest.main()
