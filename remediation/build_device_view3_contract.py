"""Keep immutable catalog input2 unchanged; version resolved/policy views as3."""
import copy,json,pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog/contracts';source=json.loads((C/'DeviceResolution.schema.json').read_text());schema=copy.deepcopy(source);schema['$id']='urn:android-binding-catalog:3.0.0:DeviceView';schema['$ref']='#/$defs/Snapshot';schema['description']='Resolved view3: base contract_hash and transitive graph_digest bind final schema_hash for grants/codecs. Catalog inventory2.1 explicitly represents point applicability; resolved3 binds complete membership and transitive type identity. Older resolved2 snapshots cannot activate silently.'
d=schema['$defs'];hash256=dict(type='string',pattern='^[a-f0-9]{64}$')
d['ResolvedDependency']=copy.deepcopy(d['Dependency']);d['ResolvedDependency']['properties']['contract_hash']=d['ResolvedDependency']['properties'].pop('schema_hash');d['ResolvedDependency']['required']=['entry_id','contract_hash']
d['ResolvedEntry']['properties']['contract_hash']={'anyOf':[hash256,{'type':'null'}]};d['ResolvedEntry']['required'].append('contract_hash');d['ResolvedEntry']['properties']['dependencies']['items']={'$ref':'#/$defs/ResolvedDependency'}
d['ResolvedEntry']['allOf'][0]['then']['properties']['contract_hash']=hash256
for name in ('InventoryEntity','ResolvedEntry'):
 d[name]['properties']['catalog_state']={'enum':['active','suppressed','conflict','deleted']}
 d[name]['properties']['catalog_revision']={'type':'integer','minimum':0,'maximum':9007199254740991}
 d[name]['properties']['catalog_document_sha256']={'anyOf':[hash256,{'type':'null'}]}
 if name=='ResolvedEntry':d[name]['required']+=['catalog_state','catalog_revision','catalog_document_sha256']
for name in ('Snapshot','Header'):
 for key in ('deprecated','x-publishable','x-replaced-by','description'):d[name].pop(key,None)
 d[name]['properties']['schema_version']={'const':'3.0.0'}
 d[name]['properties']['inventory_sha256']=copy.deepcopy(hash256);d[name]['required'].append('inventory_sha256')
(C/'DeviceView.schema.json').write_text(json.dumps(schema,indent=2)+'\n')
api=json.loads((C/'openapi.json').read_text());api['info']['version']='1.4.0'
def rewrite(value):
 if isinstance(value,dict):return {k:('#/components/schemas/Device'+v.rsplit('/',1)[1] if k=='$ref' and v.startswith('#/$defs/') else rewrite(v)) for k,v in value.items()}
 if isinstance(value,list):return [rewrite(v) for v in value]
 return value
for name,value in d.items():
 if name!='SnapshotResponse':api['components']['schemas']['Device'+name]=rewrite(copy.deepcopy(value))
(C/'openapi.json').write_text(json.dumps(api,indent=2)+'\n');print('Input2 unchanged; resolved DeviceView3/OpenAPI1.4 authored')
