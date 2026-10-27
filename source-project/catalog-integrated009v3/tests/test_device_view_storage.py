"""Executable SQLite device-view contract, fixture-only; no Android backend."""
import copy,json,pathlib,sqlite3,sys,tempfile,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import test_device_resolution as f,device_resolution as dr
try:import device_view_storage as storage
except ImportError:storage=None
class DeviceViewStorage(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(storage,'Device-view SQLite reference storage missing')
        self.db=sqlite3.connect(':memory:');self.addCleanup(self.db.close);self.db.executescript((P/'bridge-config.sql').read_text())
        self.db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')");self.db.commit()
        self.store=storage.DeviceViewStore(self.db,f.facts(),registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,inventory_count=2)
    def snapshot(self,facts=None,view_generation=1):
        facts=facts or f.facts();return dr.resolve([f.entity('a',dependencies=[dict(entry_id=f.h('b'),schema_hash=f.h('b'))]),f.entity('b','type',schema='b')],facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=facts['capability_epoch'],view_generation=view_generation)
    def test_complete_atomic_activation_and_type_projection(self):
        self.store.publish(self.snapshot(),facts=f.facts(),expected_generation=0)
        self.assertFalse(self.store.read()['stale']);self.assertEqual(self.db.execute('SELECT count(*) FROM resolved_device_catalog').fetchone()[0],2);self.assertEqual(self.db.execute('SELECT count(*) FROM binding_availability').fetchone()[0],1)
        self.assertEqual(self.db.execute('PRAGMA foreign_key_check').fetchall(),[])
    def test_invalidation_preserves_old_snapshot_and_records_reason(self):
        self.store.publish(self.snapshot(),facts=f.facts(),expected_generation=0);before=self.store.read()['snapshot'];self.store.invalidate('module-replaced')
        self.assertTrue(self.store.read()['stale']);self.assertEqual(self.store.read()['snapshot'],before);self.assertEqual(self.db.execute('SELECT count(*) FROM resolved_device_catalog').fetchone()[0],0)
        self.assertEqual(self.db.execute('SELECT reason FROM device_view_invalidations WHERE capability_epoch=1').fetchone(),('module-replaced',))
        with self.assertRaises(dr.StaleView):self.store.publish(self.snapshot(),facts=f.facts(),expected_generation=1)
    def test_cross_target_valid_hash_cannot_mutate_active_pointer(self):
        other=f.facts();other['target_key']='other-target'
        with self.assertRaisesRegex(ValueError,'Cross-target/profile'):self.store.publish(self.snapshot(other),facts=other,expected_generation=0)
        self.assertEqual(self.db.execute('SELECT count(*) FROM device_views').fetchone()[0],0);self.assertIsNone(self.store.read()['snapshot'])
    def test_sql_guards_reject_deletion_stale_cas_and_sealed_mutation(self):
        self.store.publish(self.snapshot(),facts=f.facts(),expected_generation=0)
        for sql in ("DELETE FROM device_view_active","UPDATE device_view_active SET generation=generation+1","UPDATE device_view_entries SET state='unknown'","UPDATE device_views SET inventory_count=0"):
            with self.subTest(sql=sql),self.assertRaises(sqlite3.IntegrityError):self.db.execute(sql)
        self.db.rollback();self.assertEqual(self.store.read()['generation'],1)
    def test_refresh_cas_does_not_activate_old_epoch_facts(self):
        snapshot=self.snapshot();self.store.invalidate('reconnect')
        with self.assertRaises(dr.StaleView):self.store.publish(snapshot,facts=f.facts(),expected_generation=0)
        facts=f.facts();facts['capability_epoch']=1;self.store.publish(self.snapshot(facts),facts=facts,expected_generation=0);self.assertEqual(self.store.read()['capability_epoch'],1)
    def test_wire_contract_evidence_is_stored_separately_from_existence(self):
        columns={r[1] for r in self.db.execute('PRAGMA table_info(binding_wire_contracts)')}
        self.assertTrue({'interface_version','interface_hash','flags','oneway','proof_source_json','proof_state'}<=columns)
        self.assertEqual(self.db.execute('SELECT count(*) FROM binding_wire_contracts').fetchone()[0],0)
    def test_partial_inventory_cannot_replace_complete_catalog_view(self):
        facts=f.facts();partial=dr.resolve([f.entity('a')],facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
        with self.assertRaisesRegex((ValueError,sqlite3.IntegrityError),'inventory|complete'):self.store.publish(partial,facts=facts,expected_generation=0)
        self.assertEqual(self.db.execute('SELECT count(*) FROM device_views').fetchone()[0],0)
if __name__=='__main__':unittest.main()
