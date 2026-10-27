"""Offline SQLite CRUD/CAS oracle; caller capabilities are trusted fixture inputs, not request fields."""
import pathlib,json,re,copy,hashlib,sqlite3
from source_metadata_reference import validate_descriptor,validate_group,digest
class Error(Exception):
 def __init__(self,status,message):self.status=status;super().__init__(message)
class MetadataCatalog:
 def __init__(self,db,catalog_index=None):
  self.db=db;db.row_factory=sqlite3.Row;self.catalog_index=catalog_index
  db.executescript((pathlib.Path(__file__).resolve().parents[1]/'migrations/source_metadata_ddl.sql').read_text())
 def row(self,kind,key):
  row=self.db.execute('SELECT r.* FROM metadata_heads h JOIN metadata_revisions r USING(kind,resource_id,revision) WHERE h.kind=? AND h.resource_id=?',(kind,key)).fetchone()
  return dict(row) if row else None
 def generation(self):return self.db.execute('SELECT generation FROM metadata_state WHERE singleton=1').fetchone()[0]
 def tag(self,kind,key):
  r=self.row(kind,key)
  if r is None:raise Error(404,'Metadata resource not found')
  return '"meta1:'+str(self.generation())+':'+kind+':'+hashlib.sha256(key.encode()).hexdigest()+':'+(r['base_hash'] or 'local')+':'+str(r['revision'])+':'+r['state']+':'+r['content_hash']+'"'
 def get(self,kind,key):
  row=self.row(kind,key)
  if row is None:raise Error(404,'Metadata resource not found')
  return dict(row,document=json.loads(row['document_json']),etag=self.tag(kind,key))
 def representation(self,kind,key):
  value=self.get(kind,key);value.pop('document_json');return value
 def validate_document(self,kind,key,document):
  try:
   if kind=='functional-group':
    if document.get('group_id')!=key:raise ValueError('Group identity mismatch')
    validate_group(document)
    groups={}
    for row in self.db.execute("SELECT r.* FROM metadata_heads h JOIN metadata_revisions r USING(kind,resource_id,revision) WHERE h.kind='functional-group' AND r.state='active'"):
     groups[row['resource_id']]=dict(revision=row['revision'],content_hash=row['content_hash'],document=json.loads(row['document_json']))
    validate_group(document,groups)
   elif kind=='callback-descriptor':
    if document.get('callback_id')!=key:raise ValueError('Callback identity mismatch')
    validate_descriptor(document)
   else:raise ValueError('Unknown metadata kind')
   def pin(identity,contract_hash=None,type_required=False):
    row=self.catalog_index.get(identity) if self.catalog_index is not None else None
    if row is None:raise ValueError('Unknown catalog reference or trusted catalog index not supplied')
    if contract_hash is not None and row.get('contract_hash')!=contract_hash:raise ValueError('Catalog reference contract hash mismatch')
    if type_required and row.get('kind') not in ('class','interface','aidl-interface','parcelable','enum','struct','union','typedef','annotation'):raise ValueError('Referenced binding is not a declared type')
   if kind=='functional-group':
    for member in document['members']:pin(member['binding_id'],member['contract_hash'])
    for identity in document['required_type_ids']:pin(identity,type_required=True)
   else:
    pin(document['type_id'],document['type_contract_hash'],True)
    for method in document['methods']:
     pin(method['method_id'],method['contract_hash'])
     for identity in method['required_type_ids']:pin(identity,type_required=True)
    for identity in document['registration_binding_ids']:pin(identity)
    if document['cleanup']['binding_id']:pin(document['cleanup']['binding_id'],document['cleanup']['contract_hash'])
   digest(document)
  except (ValueError,KeyError,TypeError) as e:raise Error(422,str(e)) from e
 def require_owner(self,owner):
  if owner is not True:raise Error(403,'Trusted owner capability required; request body cannot assert it')
 def precondition(self,kind,key,etag):
  if etag is None:raise Error(428,'If-Match required')
  if not isinstance(etag,str) or not re.fullmatch(r'"meta1:[0-9]+:(callback-descriptor|functional-group):[a-f0-9]{64}:(local|[a-f0-9]{64}):[1-9][0-9]*:(active|suppressed|deleted|conflict):[a-f0-9]{64}"',etag):raise Error(400,'One exact strong metadata ETag required')
  if self.row(kind,key) is None:raise Error(404,'Metadata resource not found')
  if etag!=self.tag(kind,key):raise Error(412,'Metadata revision/generation changed')
 def _append(self,kind,key,document,origin,state,base_hash,reason):
  if not isinstance(reason,str) or not reason.strip():raise Error(422,'Nonempty reason required')
  old=self.row(kind,key);revision=old['revision']+1 if old else 1
  body=json.dumps(document,sort_keys=True,separators=(',',':'),ensure_ascii=False,allow_nan=False)
  self.db.execute('INSERT INTO metadata_revisions VALUES(?,?,?,?,?,?,?,?,?)',(kind,key,revision,origin,state,base_hash,digest(document),body,reason))
  if old:self.db.execute('UPDATE metadata_heads SET revision=? WHERE kind=? AND resource_id=? AND revision=?',(revision,kind,key,old['revision']))
  else:self.db.execute('INSERT INTO metadata_heads VALUES(?,?,?)',(kind,key,revision))
 def create(self,kind,key,document,reason,*,owner=False,if_none_match=None):
  self.require_owner(owner)
  if if_none_match is None:raise Error(428,'If-None-Match:* required')
  if if_none_match!='*':raise Error(400,'Creation requires If-None-Match:*')
  with self.db:
   if self.row(kind,key):raise Error(412,'If-None-Match:* failed: target identity exists; use current-CAS update')
   self.validate_document(kind,key,document);self._append(kind,key,document,'local','active',None,reason)
  return self.get(kind,key)
 def put(self,kind,key,document,etag,reason,*,owner=False,resolution=None):
  self.require_owner(owner)
  with self.db:
   self.precondition(kind,key,etag);old=self.row(kind,key)
   self.validate_document(kind,key,document)
   if old['state']=='conflict' and resolution!=dict(action='rebase' if old['base_hash'] else 'retain-local',expected_base_hash=old['base_hash']):raise Error(409,'Explicit conflict resolution against selected base required')
   self._append(kind,key,document,'overlay' if old['base_hash'] else 'local','active',old['base_hash'],reason)
  return self.get(kind,key)
 def delete(self,kind,key,etag,reason,*,owner=False):
  self.require_owner(owner)
  with self.db:
   self.precondition(kind,key,etag);old=self.row(kind,key)
   if old['state']=='conflict':raise Error(409,'Resolve conflict before deletion')
   if old['state'] not in ('suppressed','deleted'):self._append(kind,key,json.loads(old['document_json']),'overlay' if old['base_hash'] else 'local','suppressed' if old['base_hash'] else 'deleted',old['base_hash'],reason)
  return self.get(kind,key)
 def import_fixture(self,kind,key,document,reason='trusted immutable fixture import',*,trusted_import=False):
  if trusted_import is not True:raise Error(403,'Only trusted importer can add immutable base metadata')
  with self.db:
   self.validate_document(kind,key,document);h=digest(document);old=self.row(kind,key)
   self.db.execute('INSERT OR IGNORE INTO metadata_bases VALUES(?,?,?,?)',(kind,key,h,json.dumps(document,sort_keys=True,separators=(',',':'),ensure_ascii=False,allow_nan=False)))
   if old and old['base_hash']==h:return self.get(kind,key)
   clean=old is None or old['origin']=='imported' and old['state']=='active' and old['content_hash']==old['base_hash']
   self._append(kind,key,document if clean else json.loads(old['document_json']),'imported' if clean else 'overlay','active' if clean else 'conflict',h,reason)
  return self.get(kind,key)
 def cursor(self,reader):return (reader,self.generation())
 def continue_page(self,cursor,reader):
  if cursor!=(reader,self.generation()):raise Error(410,'Reader/generation scoped cursor expired')
 def history(self,kind,key):
  return [dict(r) for r in self.db.execute('SELECT * FROM metadata_revisions WHERE kind=? AND resource_id=? ORDER BY revision',(kind,key))]

