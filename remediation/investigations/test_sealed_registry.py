"""Post-freeze investigation: published membership must not change invisibly."""
import pathlib,sqlite3,unittest
C=pathlib.Path(__file__).resolve().parents[1]/'catalog'
class SealedRegistry(unittest.TestCase):
 def test_no_appending_members_after_publication(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'schema.sql').read_text())
  db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')")
  db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'h','now','published','{}')");db.execute("INSERT INTO active_registry VALUES(1,'r')");db.commit()
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO registry_entries VALUES('r','b','{}','hash')")
if __name__=='__main__':unittest.main()
