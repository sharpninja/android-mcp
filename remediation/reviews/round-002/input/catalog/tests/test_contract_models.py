import hashlib,itertools,json,pathlib,sqlite3,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
from contract_reference import Catalog,Error,identity
from policy_reference import Rule,evaluate
from dispatch_manifest import generate

class Models(unittest.TestCase):
    def error(self,status,fn):
        with self.assertRaises(Error) as e:fn()
        self.assertEqual(e.exception.status,status)
    def test_cas_tombstones_history_and_cursor(self):
        c=Catalog();k=c.create('java:a.A#x()',{'v':1});old=c.tag(k);page=c.cursor()
        self.error(428,lambda:c.put(k,None,{}));self.error(400,lambda:c.put(k,'*',{}))
        deleted=c.delete(k,old);self.error(412,lambda:c.put(k,old,{}))
        self.assertEqual(c.delete(k,deleted),deleted)
        c.put(k,deleted,{'v':2});self.assertEqual(c.entries[k]['state'],'active')
        self.error(410,lambda:c.continue_page(page));self.assertEqual(len(c.history[k]),3)
    def test_import_both_collision_orders_and_rebase(self):
        sig='java:a.A#x()';key=identity(sig);base={key:(sig,{'p':1})}
        c=Catalog();c.create(sig,{});c.publish_fixture(base)
        self.assertEqual(c.entries[key]['state'],'conflict');self.error(409,lambda:c.delete(key,c.tag(key)))
        self.error(409,lambda:c.put(key,c.tag(key),{}));c.put(key,c.tag(key),{},dict(action='rebase',expected_imported_record_sha256=c.entries[key]['base_hash']))
        d=Catalog();d.publish_fixture(base);self.error(409,lambda:d.create(sig,{}))
        d.delete(key,d.tag(key));d.publish_fixture({key:(sig,{'p':2})});self.assertEqual(d.entries[key]['state'],'conflict')
        d.publish_fixture({});d.put(key,d.tag(key),{},dict(action='retain-local',expected_imported_record_sha256=None));self.assertFalse(d.entries[key]['imported'])
    def test_binding_rules_dominate_all_aliases(self):
        plan=[('binding','s','t',2)]
        for effect,entry_effect,authority,emergency in itertools.product(('deny','ask','allow'),('deny','ask','allow'),(False,True),(False,True)):
            rule=Rule(effect,'s',frozenset({'t'}),3);entry=Rule(entry_effect,'tool',frozenset({'t'}),3)
            decisions=[evaluate(plan,{'binding':rule},entry,authority=authority,emergency=emergency) for route in ('generated','generic','known-raw')]
            self.assertEqual(len(set(decisions)),1)
            if effect=='deny' or entry_effect=='deny' or emergency:self.assertEqual(decisions[0],'deny')
        allow=Rule('allow','s',frozenset({'t'}),1)
        self.assertEqual(evaluate(plan,{'binding':allow},allow,authority=True),'deny')
        self.assertEqual(evaluate(plan,{},allow,authority=True),'deny')
        self.assertEqual(evaluate([('raw:service','s','t',1)],{'binding':allow},allow,authority=True),'deny')
    def test_generated_alias_identity_and_default_deny(self):
        sig='java:a.A#x()';e=dict(entry_id=identity(sig),canonical_signature=sig)
        r=generate([e])['aliases'][0];self.assertEqual(r['binding_id'],r['generic_binding_id']);self.assertEqual(r['default_grant'],'deny')
        with self.assertRaises(ValueError):generate([e,e])
    def test_bridge_ddl_integrity_and_closed_enums(self):
        db=sqlite3.connect(':memory:');db.executescript((P/'bridge-config.sql').read_text())
        db.execute("INSERT INTO clients VALUES('fixture','fixture','active',0,1,0,0,NULL)")
        with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO credentials VALUES('fixture','missing',zeroblob(32),'fixture','active',0,NULL,NULL,0)")
        with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO client_logging_policy VALUES('fixture','full-raw',1,1,1,1)")
        self.assertEqual(db.execute('PRAGMA integrity_check').fetchone()[0],'ok');self.assertEqual(db.execute('PRAGMA foreign_key_check').fetchall(),[])

if __name__=='__main__':unittest.main()
