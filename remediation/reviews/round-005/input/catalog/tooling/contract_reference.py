"""Small offline state-machine oracle for API fixtures, NOT an administration service."""
import hashlib,json

class Error(Exception):
    def __init__(self,status):self.status=status

def identity(signature):return hashlib.sha256(signature.encode()).hexdigest()
def digest(document):return hashlib.sha256(json.dumps(document,sort_keys=True,separators=(',',':'),allow_nan=False,ensure_ascii=False).encode()).hexdigest()

class Catalog:
    def __init__(self):self.entries={};self.registry_generation=1;self.registry_id='fixture-initial';self.overlay_generation=0;self.history={}
    def tag(self,key):
        r=self.entries[key]
        return f'"cat1:{self.registry_generation}:{r["base_hash"] or "local"}:{key}:{r["revision"]}:{r["state"]}"'
    def create(self,signature,document,reason=None):
        key=identity(signature)
        if key in self.entries:raise Error(409)
        self.entries[key]=dict(signature=signature,document=document,state='active',revision=1,base_hash=None,imported=False)
        self.record(key,'create',reason);return key
    def record(self,key,operation,reason=None):
        self.overlay_generation+=1
        self.history.setdefault(key,[]).append(dict(operation=operation,revision=self.entries[key]['revision'],state=self.entries[key]['state']))
        if reason is not None:self.history[key][-1]['reason']=reason
    def precondition(self,key,tag):
        if tag is None:raise Error(428)
        if not isinstance(tag,str) or not tag.startswith('"cat1:') or not tag.endswith('"') or ',' in tag:raise Error(400)
        if key not in self.entries:raise Error(404)
        if tag!=self.tag(key):raise Error(412)
    def put(self,key,tag,document,resolution=None,reason=None):
        self.precondition(key,tag);r=self.entries[key]
        if r['state']=='conflict':
            expected=dict(action='rebase' if r['base_hash'] else 'retain-local',expected_imported_record_sha256=r['base_hash'])
            if resolution!=expected:raise Error(409)
        r.update(document=document,state='active',revision=r['revision']+1)
        self.record(key,'update',reason);return self.tag(key)
    def delete(self,key,tag,reason=None):
        self.precondition(key,tag);r=self.entries[key]
        if r['state']=='conflict':raise Error(409)
        if r['state'] in ('deleted','suppressed'):return self.tag(key)
        r.update(state='suppressed' if r['imported'] else 'deleted',revision=r['revision']+1)
        self.record(key,'delete',reason);return self.tag(key)
    def publish_fixture(self,records):
        """records = complete next selected base. Fixture bypasses staging validation."""
        # Validate all incoming identities/documents before changing selected state.
        for key,(signature,document) in records.items():
            if identity(signature)!=key:raise Error(422)
            digest(document)
        for key,r in list(self.entries.items()):
            if r['imported'] and key not in records:
                if r['revision']==0:
                    del self.entries[key]
                    continue
                r.update(base_hash=None,imported=False,state='conflict',revision=r['revision']+1);self.record(key,'conflict')
        for key,(signature,doc) in records.items():
            if identity(signature)!=key:raise Error(422)
            h=digest(doc)
            if key not in self.entries:self.entries[key]=dict(signature=signature,document=doc,state='active',revision=0,base_hash=h,imported=True)
            else:
                r=self.entries[key]
                if not r['imported'] or (r['base_hash']!=h and r['revision']>0):
                    r.update(state='conflict',revision=r['revision']+1);self.record(key,'conflict')
                elif r['revision']==0:r['document']=doc
                r.update(base_hash=h,imported=True)
        self.registry_generation+=1
        self.registry_id=digest(records)
    def cursor(self):return (self.registry_id,self.registry_generation,self.overlay_generation)
    def continue_page(self,cursor):
        if cursor!=self.cursor():raise Error(410)
