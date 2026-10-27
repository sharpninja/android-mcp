"""Offline wire evidence revision/CAS oracle, never a Binder transport/codec.
An evidence status is metadata, not an OS or per-client authority decision.
"""
import json
import device_resolution as dr
def current(db,contract_id):
 row=db.execute('SELECT * FROM current_wire_proofs WHERE contract_id=?',(contract_id,)).fetchone()
 if row is None:raise ValueError('Unknown immutable wire identity')
 names=['contract_id','binding_id','schema_hash','descriptor','interface_version','interface_hash','transaction_code','flags','oneway','revision','proof_state','argument_codec_json','response_codec_json','proof_source_json','record_sha256'];item=dict(zip(names,row));record=item.pop('record_sha256')
 for key in ('argument_codec','response_codec','proof_source'):item[key]=json.loads(item.pop(key+'_json'))
 expected=dr.digest(item)
 if record is not None and record!=expected:raise ValueError('Wire proof record hash mismatch')
 item['record_sha256']=expected;return item
def append(db,contract_id,*,expected_revision,proof_state,argument_codec,response_codec,proof_source):
 if db.in_transaction:raise ValueError('Wire proof reference owns its transaction')
 if type(expected_revision) is not int or not 0<=expected_revision<dr.LIMIT:raise ValueError('Invalid wire proof expected revision')
 try:
  db.execute('BEGIN IMMEDIATE');old=current(db,contract_id)
  if old['revision']!=expected_revision:raise ValueError('Wire proof revision changed')
  body={k:v for k,v in old.items() if k!='record_sha256'};body.update(revision=expected_revision+1,proof_state=proof_state,argument_codec=argument_codec,response_codec=response_codec,proof_source=proof_source)
  db.execute('INSERT INTO wire_proof_revisions VALUES(?,?,?,?,?,?,?)',(contract_id,body['revision'],proof_state,dr.canonical(argument_codec),dr.canonical(response_codec),dr.canonical(proof_source),dr.digest(body)))
  if expected_revision==0:db.execute('INSERT INTO wire_proof_active VALUES(?,?)',(contract_id,body['revision']))
  else:
   if db.execute('UPDATE wire_proof_active SET revision=? WHERE contract_id=? AND revision=?',(body['revision'],contract_id,expected_revision)).rowcount!=1:raise ValueError('Wire proof revision CAS failed')
  selected=current(db,contract_id);db.commit();return selected
 except Exception:db.rollback();raise
def selected_proof_unchanged(db,contract_id,record_sha256):
 """Fixture admission recheck; semantic client grants alone never replace it."""
 row=current(db,contract_id);return row['proof_state']=='verified' and row['record_sha256']==record_sha256
