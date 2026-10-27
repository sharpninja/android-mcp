"""Post-freeze investigation: published membership must not change invisibly."""
import pathlib,sqlite3,unittest,tempfile,sys
C=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(C/'tooling'))
class SealedRegistry(unittest.TestCase):
 def test_memberships_are_staged_only_and_staged_inserts_succeed(self):
  for state in ('staged','validated','published','retired','rejected'):
   with self.subTest(state=state):
    db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'schema.sql').read_text());db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',NULL,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now',?,'{}')",(state,));db.execute("INSERT INTO sources VALUES('s','p','https://example.invalid/p','rev','now','d04b98f48e8f8bcc15c6ae5ac050801cd6dcfd428fb5f9e65c4e16e7807340fa',NULL,'unknown','evidence','fixture')");db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')")
    for sql in ("INSERT INTO registry_entries VALUES('r','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','{}','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123')","INSERT INTO registry_sources VALUES('r','s')"):
     if state=='staged':db.execute(sql)
     else:
      with self.assertRaises(sqlite3.IntegrityError):db.execute(sql)
 def test_copy_migration_restores_retired_before_published(self):
  import migrate_v3
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);db=sqlite3.connect(root/'old.sqlite')
   for name in ('001-initial.sql','002-evidence-and-publication.sql'):db.executescript((C.parent/'evidence'/('original-'+name)).read_text())
   db.execute("INSERT INTO registries VALUES('old',1,'1.1.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','retired','{}')");db.execute("INSERT INTO registries VALUES('selected',5,'1.1.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','published','{}')");db.execute("INSERT INTO active_registry VALUES(1,'selected')");db.commit();db.close()
   migrate_v3.migrate(root/'old.sqlite',root/'new.sqlite');new=sqlite3.connect(root/'new.sqlite');self.addCleanup(new.close)
   self.assertEqual(dict(new.execute('SELECT registry_id,state FROM registries')),{'old':'retired','selected':'published'});self.assertEqual(new.execute('SELECT registry_id FROM active_registry').fetchone()[0],'selected');self.assertEqual(new.execute('SELECT registry_generation FROM catalog_view').fetchone()[0],5)
 def test_no_appending_members_after_publication(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'schema.sql').read_text())
  db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')")
  db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','published','{}')");db.execute("INSERT INTO active_registry VALUES(1,'r')");db.commit()
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO registry_entries VALUES('r','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','{}','d04b98f48e8f8bcc15c6ae5ac050801cd6dcfd428fb5f9e65c4e16e7807340fa')")
if __name__=='__main__':unittest.main()
