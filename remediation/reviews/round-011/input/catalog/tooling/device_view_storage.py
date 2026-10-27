"""Offline SQLite reference for captured facts and atomic resolved-view activation.
No Android probes, connections, policy changes, Binder execution or HTTP server.
"""
import datetime,json,sqlite3
import device_resolution as dr
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
class DeviceViewStore:
 def __init__(self,db,scope_facts,*,registry_id,catalog_generation,overlay_generation,inventory_count):
  if not isinstance(inventory_count,int) or isinstance(inventory_count,bool) or not 0<=inventory_count<=dr.LIMIT:raise ValueError('Invalid expected inventory count')
  self.expected_inventory_count=inventory_count
  self.db=db;self.scope=tuple(scope_facts[k] for k in dr.SCOPE);self.scope_id=dr.digest(list(self.scope))
  with db:
   db.execute('INSERT OR IGNORE INTO device_view_scopes VALUES(?,?,?,?,?,?)',(self.scope_id,*self.scope))
   if not db.execute('SELECT 1 FROM device_view_active WHERE scope_id=?',(self.scope_id,)).fetchone():
    db.execute('INSERT INTO device_view_invalidations VALUES(?,?,?,?)',(self.scope_id,0,'startup',now()))
    db.execute('INSERT INTO device_view_active VALUES(?,NULL,?,?,?,0,0,1,?)',(self.scope_id,registry_id,catalog_generation,overlay_generation,inventory_count))
 def control(self):
  row=self.db.execute('SELECT view_id,registry_id,catalog_generation,overlay_generation,capability_epoch,generation,stale,expected_inventory_count FROM device_view_active WHERE scope_id=?',(self.scope_id,)).fetchone()
  if row is None:raise ValueError('Device scope not initialized')
  return dict(zip(('view_id','registry_id','catalog_generation','overlay_generation','capability_epoch','generation','stale','expected_inventory_count'),row))
 def read(self):
  c=self.control();snapshot=None
  if c['view_id']:
   snapshot=json.loads(self.db.execute('SELECT document_json FROM device_views WHERE view_id=?',(c['view_id'],)).fetchone()[0]);snapshot['entries']=[json.loads(r[0]) for r in self.db.execute('SELECT document_json FROM device_view_entries WHERE view_id=? ORDER BY entry_id',(c['view_id'],))]
  return dict(snapshot=snapshot,stale=bool(c['stale']),capability_epoch=c['capability_epoch'],generation=c['generation'])
 def invalidate(self,reason,*,registry_id=None,catalog_generation=None,overlay_generation=None,inventory_count=None):
  if not isinstance(reason,str) or not reason:raise ValueError('Invalidation reason required')
  if self.db.in_transaction:raise ValueError('Reference storage owns its transaction')
  try:
   self.db.execute('BEGIN IMMEDIATE');c=self.control();epoch=c['capability_epoch']+1
   if epoch>dr.LIMIT:raise OverflowError('Capability epoch exhausted')
   self.db.execute('INSERT INTO device_view_invalidations VALUES(?,?,?,?)',(self.scope_id,epoch,reason,now()))
   self.db.execute('UPDATE device_view_active SET capability_epoch=?,stale=1,registry_id=?,catalog_generation=?,overlay_generation=?,expected_inventory_count=? WHERE scope_id=?',(epoch,registry_id if registry_id is not None else c['registry_id'],catalog_generation if catalog_generation is not None else c['catalog_generation'],overlay_generation if overlay_generation is not None else c['overlay_generation'],inventory_count if inventory_count is not None else c['expected_inventory_count'],self.scope_id));self.db.commit()
  except Exception:self.db.rollback();raise
 def publish(self,snapshot,*,facts,expected_generation):
  if self.db.in_transaction:raise ValueError('Reference storage owns its transaction')
  try:
   self.db.execute('BEGIN IMMEDIATE');c=self.control()
   oracle=dr.ViewStore(c['registry_id'],c['catalog_generation'],c['overlay_generation'],*self.scope);oracle.epoch=c['capability_epoch'];oracle.generation=c['generation']
   oracle.publish(snapshot,facts=facts,expected_generation=expected_generation)
   if snapshot['inventory_count']!=c['expected_inventory_count']:raise ValueError('Incomplete inventory for pinned catalog')
   facts_id=dr.digest(facts);self.db.execute('INSERT OR IGNORE INTO device_capability_facts VALUES(?,?,?,?,?)',(facts_id,self.scope_id,facts['capability_epoch'],facts['observed_at'],dr.canonical(facts)))
   header={k:v for k,v in snapshot.items() if k!='entries'}
   self.db.execute('INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],self.scope_id,snapshot['registry_id'],snapshot['catalog_generation'],snapshot['overlay_generation'],snapshot['capability_epoch'],snapshot['view_generation'],snapshot['inventory_count'],facts_id,dr.canonical(header),'staged'))
   for e in snapshot['entries']:
    self.db.execute('INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],e['entry_id'],e['entity_kind'],e['state'],dr.canonical(e['reason_codes']),e['schema_hash'],dr.canonical(e['dependencies']),e['graph_digest'],dr.canonical(e)))
   self.db.execute("UPDATE device_views SET state='sealed' WHERE view_id=?",(snapshot['view_id'],))
   changed=self.db.execute('UPDATE device_view_active SET view_id=?,generation=?,stale=0 WHERE scope_id=? AND generation=? AND capability_epoch=?',(snapshot['view_id'],snapshot['view_generation'],self.scope_id,expected_generation,facts['capability_epoch'])).rowcount
   if changed!=1:raise dr.StaleView('Concurrent device view CAS')
   self.db.commit()
  except Exception:self.db.rollback();raise
