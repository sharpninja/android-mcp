"""Author additive resolver contracts and API specification, not an HTTP service."""
import pathlib,json,copy
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog/contracts'
def obj(properties,required=None):return dict(type='object',properties=properties,required=list(properties) if required is None else required,additionalProperties=False)
def array(item,minimum=0):return dict(type='array',items=item,minItems=minimum)
def ref(name):return {'$ref':'#/$defs/'+name}
def nullable(value):return {'anyOf':[value,{'type':'null'}]}
string=dict(type='string',minLength=1);integer=dict(type='integer',minimum=0,maximum=2**31-1);counter=dict(type='integer',minimum=0,maximum=2**53-1);hash256=dict(type='string',pattern='^[a-f0-9]{64}$');decimal=dict(type='string',pattern='^(0|[1-9][0-9]*)$',maxLength=19)
version=dict(type='array',items=integer,minItems=2,maxItems=2)
definitions={}
definitions['PlatformRange']=obj(dict(introduced=version,removed=nullable(version)))
definitions['ModuleRange']=obj(dict(introduced=decimal,removed=nullable(decimal)))
definitions['InterfaceConstraint']=obj(dict(descriptor=string,minimum_version=nullable(integer),accepted_hashes=array(string)))
definitions['Clause']=obj(dict(platform=ref('PlatformRange'),extensions=dict(type='object',additionalProperties=integer),modules=dict(type='object',additionalProperties=ref('ModuleRange')),oem=array(string,1),build_fingerprints=array(string,1),interface=ref('InterfaceConstraint')),[]);definitions['Clause']['minProperties']=1
definitions['Clause']['properties']['unknown_reason']=string
known_rule=obj(dict(alternatives=array(ref('Clause'),1),evidence_ids=array(string,1)))
unknown_rule=obj(dict(unknown_reason=string,**{'x-raw':{}}),['unknown_reason'])
definitions['Rule']={'oneOf':[known_rule,unknown_rule]}
definitions['ModuleFact']=obj(dict(state=dict(enum=['present','absent','unknown']),version=nullable(decimal),authoritative=dict(type='boolean')))
definitions['InterfaceFact']=obj(dict(state=dict(enum=['reachable','absent','denied','unreachable','unsupported','unknown']),version=nullable(integer),hash=nullable(string),authoritative=dict(type='boolean')))
scope=dict(profile_id=string,target_key=string,android_user_id=integer,backend_instance_id=string,device_instance_id=string)
definitions['Facts']=obj(dict(**scope,identity_verified=dict(type='boolean'),capability_epoch=counter,observed_at=dict(type='string',minLength=1,maxLength=64,format='date-time'),platform=obj(dict(major=nullable(integer),minor=nullable(integer))),extensions=dict(type='object',additionalProperties=nullable(integer)),modules=dict(type='object',additionalProperties=ref('ModuleFact')),interfaces=dict(type='object',additionalProperties=ref('InterfaceFact')),oem=nullable(string),build_fingerprint=nullable(string),evidence_ids=array(string,1)))
definitions['Dependency']=obj(dict(entry_id=hash256,schema_hash=hash256))
definitions['Variant']=obj(dict(schema_hash=hash256,availability=ref('Rule'),dependencies=array(ref('Dependency'))))
definitions['InventoryEntity']=obj(dict(entry_id=hash256,entity_kind=dict(enum=['binding','type','other']),variants=array(ref('Variant'))))
definitions['ResolvedEntry']=obj(dict(entry_id=hash256,entity_kind=dict(enum=['binding','type','other']),state=dict(enum=['available','unavailable','unknown']),reason_codes=array(string,1),schema_hash=nullable(hash256),dependencies=array(ref('Dependency')),graph_digest=nullable(hash256)))
definitions['ResolvedEntry']['allOf']=[dict(**{'if':dict(properties=dict(state=dict(const='available'))),'then':dict(properties=dict(schema_hash=hash256,graph_digest=hash256))})]
definitions['Snapshot']=obj(dict(schema_version=dict(const='2.0.0'),view_id=hash256,facts_id=hash256,registry_id=hash256,catalog_generation=counter,overlay_generation=counter,capability_epoch=counter,view_generation=counter,inventory_count=counter,entries=array(ref('ResolvedEntry')),**scope))
definitions['SnapshotResponse']=obj(dict(snapshot=nullable(ref('Snapshot')),stale=dict(type='boolean'),capability_epoch=counter,generation=counter))
definitions['Header']=obj({k:v for k,v in definitions['Snapshot']['properties'].items() if k!='entries'})
definitions['StatusResponse']=obj(dict(header=nullable(ref('Header')),stale=dict(type='boolean'),capability_epoch=counter,generation=counter))
definitions['EntryPage']=obj(dict(view_id=hash256,view_generation=counter,items=dict(type='array',items=ref('ResolvedEntry'),maxItems=500),next_cursor=nullable(string)))
definitions['RefreshRequest']=obj(dict(profile_id=string,target_key=string))
schema={'$schema':'https://json-schema.org/draft/2020-12/schema','$id':'urn:android-binding-catalog:2.0.0:DeviceResolution','$ref':'#/$defs/Snapshot','$defs':definitions,'description':'Offline reference/collector contract. Facts are internally collected evidence, never remote assertions or permission grants. Missing metadata yields unknown. No probes or server are implemented.'}
(C/'DeviceResolution.schema.json').write_text(json.dumps(schema,indent=2)+'\n')
api_path=C/'openapi.json';api=json.loads(api_path.read_text());api['info']['version']='1.3.0'
def rewrite(value):
 if isinstance(value,dict):return {k:('#/components/schemas/Device'+v.rsplit('/',1)[1] if k=='$ref' and v.startswith('#/$defs/') else rewrite(v)) for k,v in value.items()}
 if isinstance(value,list):return [rewrite(v) for v in value]
 return value
for name,value in definitions.items():
 if name!='SnapshotResponse':api['components']['schemas']['Device'+name]=rewrite(copy.deepcopy(value))
api['components']['schemas'].pop('DeviceSnapshotResponse',None)
query=[dict(name=n,**{'in':'query'},required=True,schema=string) for n in ('profile_id','target_key')]
api['paths']['/v1/catalog/device-views/current']={'get':dict(operationId='getCurrentDeviceView',description='Read a pinned complete device view. Authenticated scope must authorize the exact profile/target. stale=true forbids admission; full inventory remains accessible. Availability is not a permission grant.',security=[dict(catalogAuth=['catalog:read'])],parameters=query,responses={'200':dict(description='Current snapshot plus stale/capability epoch; no remote facts accepted',content={'application/json':dict(schema={'$ref':'#/components/schemas/DeviceSnapshotResponse'})}),'403':dict(description='Profile/target access denied without existence disclosure')})}
api['paths']['/v1/catalog/device-views/{view_id}']={'get':dict(operationId='getDeviceViewSnapshot',description='Immutable snapshot read for an authorized profile/target. Historical snapshot is not current authority; cursors pin view_id/generation and fail410 on invalidation.',security=[dict(catalogAuth=['catalog:read'])],parameters=[dict(name='view_id',**{'in':'path'},required=True,schema=hash256)],responses={'200':dict(description='Immutable resolved snapshot',content={'application/json':dict(schema={'$ref':'#/components/schemas/DeviceSnapshot'})}),'404':dict(description='Unknown or inaccessible snapshot'),'410':dict(description='Expired snapshot/cursor')})}
api['paths']['/v1/catalog/device-views/refresh']={'post':dict(operationId='requestDeviceViewRefresh',description='Authenticated local owner only, within configured backend authority. Request bounded supported metadata refresh; never accept asserted facts, available flags, arbitrary probes or permission changes. Startup/event refresh uses the same internal coordinator. No HTTP service is implemented.',security=[dict(catalogAuth=['catalog:admin'])],parameters=[dict(name='If-Match',**{'in':'header'},required=True,schema=string)],requestBody=dict(required=True,content={'application/json':dict(schema={'$ref':'#/components/schemas/DeviceRefreshRequest'})}),responses={'202':dict(description='Refresh accepted; old view is stale until complete atomic activation'),'428':dict(description='Missing current-view precondition'),'412':dict(description='Stale view generation'),'403':dict(description='Owner/backend scope denied')})}
api['paths']['/v1/catalog/device-views/current']['get']['responses']['200']['content']['application/json']['schema']={'$ref':'#/components/schemas/DeviceStatusResponse'}
api['paths']['/v1/catalog/device-views/{view_id}']['get']['responses']['200']['content']['application/json']['schema']={'$ref':'#/components/schemas/DeviceHeader'}
api['paths']['/v1/catalog/device-views/{view_id}/entries']={'get':dict(operationId='listDeviceViewEntries',description='Bounded rows from one immutable snapshot. Full inventory retained. Cursor binds view/profile/target/authenticated reader/filter and expires410 on invalidation; availability never grants invocation.',security=[dict(catalogAuth=['catalog:read'])],parameters=[dict(name='view_id',**{'in':'path'},required=True,schema=hash256),dict(name='limit',**{'in':'query'},schema=dict(type='integer',minimum=1,maximum=500,default=100)),dict(name='availability',**{'in':'query'},schema=dict(enum=['all','available','unavailable','unknown'],default='all')),dict(name='cursor',**{'in':'query'},schema=string)],responses={'200':dict(description='Bounded page from the pinned view',content={'application/json':dict(schema={'$ref':'#/components/schemas/DeviceEntryPage'})}),'410':dict(description='Expired/stale snapshot or cursor'),'403':dict(description='Profile/target access denied')})}
view_tag=dict(type='string',pattern='^"dv1:[0-9a-f]{64}:(0|[1-9][0-9]*):(0|[1-9][0-9]*):(0|[1-9][0-9]*):(0|[1-9][0-9]*)"$')
current=api['paths']['/v1/catalog/device-views/current']['get']
current['description']='Read bounded current status/header and strong dv1 ETag, including generation-zero bootstrap with null header. Authorized exact profile/target only. stale forbids admission; availability is separate from permission.'
current['responses']['200']['headers']={'ETag':dict(required=True,description='Strong scope/generation/epoch/catalog/overlay token, including null-header bootstrap. Exact composition and canonical scope hashing in API-CONTRACT.md.',schema=view_tag)}
refresh=api['paths']['/v1/catalog/device-views/refresh']['post'];refresh['parameters'][0]['schema']=view_tag
refresh['description']+=' After scope authorization, missing/malformed/mismatching strong current-status ETag returns428/400/412. Matched refresh atomically increments epoch and marks stale, including bootstrap; no duplicate stale token can be admitted. Exact dv1 syntax in API-CONTRACT.md.'
refresh['responses']['400']=dict(description='Malformed or weak/multiple If-Match token')
refresh['responses']['202']=dict(description='Bounded internal refresh queued; epoch incremented and previous view stale',headers={'ETag':dict(required=True,description='New current-view token',schema=view_tag)},content={'application/json':dict(schema={'$ref':'#/components/schemas/DeviceStatusResponse'})})
for path,operations in api['paths'].items():
 if '/device-views/' in path:
  for method,operation in operations.items():
   if method in ('get','post'):
    operation['responses'].setdefault('400',dict(description='Malformed bounded request or cursor'));operation['responses'].setdefault('401',dict(description='Authentication required'))
refresh['responses']['412']['headers']={'ETag':dict(required=True,description='Current authorized-scope view token',schema=view_tag)}
api_path.write_text(json.dumps(api,indent=2)+'\n');print('DeviceResolution.schema.json and additive OpenAPI1.2.0 authored')
