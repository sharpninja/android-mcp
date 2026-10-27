"""Proof acquisition must progress without rewriting immutable wire identity."""
import json,pathlib,sqlite3,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import test_device_resolution as f
try:import wire_proof_reference as wire
except ImportError:wire=None
class WireEvidenceRevision(unittest.TestCase):
 def setUp(self):
  self.db=sqlite3.connect(':memory:');self.addCleanup(self.db.close);self.db.executescript((P/'bridge-config.sql').read_text());key=f.h('a');self.schema=f.h('b');self.contract=f.h('c')
  self.db.execute('INSERT INTO binding_definitions VALUES(?,?,?)',(key,'fixture-binding','1.1.0'));self.db.execute('INSERT INTO binding_mappings VALUES(?,?,?,?,?,?,?,?,?,?)',(key,self.schema,1,'unknown','fixture','binder','a.I',0,'{}','{}'));self.db.execute('INSERT INTO binding_wire_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',(self.contract,key,self.schema,'a.I',None,None,0,0,0,'{}','{}','{}','unknown'));self.db.commit()
 def test_unknown_proof_can_be_verified_then_revoked_without_rewriting_identity(self):
  self.assertIsNotNone(wire,'Immutable unknown wire row has no versioned proof-acquisition path')
  codec={'codec_id':'fixture-codec-v1','schema_hash':self.schema};first=wire.append(self.db,self.contract,expected_revision=0,proof_state='verified',argument_codec=codec,response_codec=codec,proof_source={'evidence':['fixture-source']});self.assertEqual(first['revision'],1);self.assertEqual(first['proof_state'],'verified')
  self.assertTrue(wire.selected_proof_unchanged(self.db,self.contract,first['record_sha256']))
  self.assertEqual(self.db.execute('SELECT proof_state FROM binding_wire_contracts').fetchone()[0],'unknown')
  second=wire.append(self.db,self.contract,expected_revision=1,proof_state='unsupported',argument_codec={},response_codec={},proof_source={'reason':'fixture ABI proof revoked'});self.assertEqual(second['revision'],2);self.assertEqual(second['proof_state'],'unsupported');self.assertEqual(self.db.execute('SELECT count(*) FROM wire_proof_revisions').fetchone()[0],2)
  self.assertFalse(wire.selected_proof_unchanged(self.db,self.contract,first['record_sha256']));self.assertEqual(second['schema_hash'],first['schema_hash'])
  self.assertEqual(self.db.execute('PRAGMA foreign_key_check').fetchall(),[])
 def test_stale_revision_or_invalid_verified_codec_cannot_change_selection(self):
  self.assertIsNotNone(wire);codec={'codec_id':'fixture-codec','schema_hash':self.schema};wire.append(self.db,self.contract,expected_revision=0,proof_state='verified',argument_codec=codec,response_codec=codec,proof_source={'evidence':['fixture']})
  with self.assertRaisesRegex(ValueError,'revision'):wire.append(self.db,self.contract,expected_revision=0,proof_state='unknown',argument_codec={},response_codec={},proof_source={})
  with self.assertRaises((ValueError,sqlite3.IntegrityError)):wire.append(self.db,self.contract,expected_revision=1,proof_state='verified',argument_codec={},response_codec={},proof_source={'evidence':['fixture']})
  self.assertEqual(wire.current(self.db,self.contract)['revision'],1);self.assertEqual(self.db.execute('SELECT count(*) FROM wire_proof_revisions').fetchone()[0],1)
if __name__=='__main__':unittest.main()
