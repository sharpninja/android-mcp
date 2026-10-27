"""Post-round010-freeze identity audit; not included in round010 evidence."""
import pathlib,sqlite3,unittest
P=pathlib.Path(__file__).resolve().parents[1]
class LocalIdentityBoundary(unittest.TestCase):
    def test_local_overlay_identity_has_the_same_hash_shape_as_imported_entries(self):
        for entry_id in ('local:fixture','short','A'*64,'z'*64):
            with self.subTest(entry_id=entry_id):
                db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text())
                with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES(?,NULL,'local:fixture',1,NULL,NULL,'active','{}','now','now')",(entry_id,))
if __name__=='__main__':unittest.main()
