"""Add the source metadata/descriptor/group API specification, not an HTTP host."""
import json,pathlib,copy
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog/contracts'
api=json.loads((C/'openapi.json').read_text());schema=json.loads((C/'SourceContractMetadata.schema.json').read_text())
def rewrite(value):
 if isinstance(value,dict):return {k:(v.replace('#/$defs/','#/components/schemas/') if k=='$ref' else rewrite(v)) for k,v in value.items()}
 if isinstance(value,list):return [rewrite(v) for v in value]
 return value
components=api['components']['schemas'];components.update(rewrite(schema['$defs']))
components['Entry']['properties'].setdefault('extensions',{'type':'object'}).setdefault('properties',{})['source_contract_metadata']={'$ref':'#/components/schemas/MetaSet'}
def ref(name):return {'$ref':'#/components/schemas/'+name}
def obj(props,required):return dict(type='object',properties=props,required=required,additionalProperties=False)
nonempty=dict(type='string',minLength=1);hash64=dict(type='string',pattern='^[a-f0-9]{64}$')
resolution=obj(dict(action={'enum':['rebase','retain-local']},expected_base_hash={'anyOf':[hash64,{'type':'null'}]}),['action','expected_base_hash'])
components['MetadataMutation']=obj(dict(document={'oneOf':[ref('MetaCallbackDescriptor'),ref('MetaFunctionalGroup')]},reason=nonempty,resolution=resolution),['document','reason'])
components['MetadataDelete']=obj(dict(reason=nonempty),['reason'])
components['MetadataRepresentation']=obj(dict(kind={'enum':['callback-descriptor','functional-group']},resource_id=nonempty,revision=dict(type='integer',minimum=1),origin={'enum':['imported','local','overlay']},state={'enum':['active','suppressed','deleted','conflict']},base_hash={'anyOf':[hash64,{'type':'null'}]},content_hash=hash64,document={'oneOf':[ref('MetaCallbackDescriptor'),ref('MetaFunctionalGroup')]},reason=nonempty,etag=nonempty),['kind','resource_id','revision','origin','state','base_hash','content_hash','document','reason','etag'])
components['MetadataPage']=obj(dict(items=dict(type='array',items=ref('MetadataRepresentation'),maxItems=100),next_cursor={'type':['string','null']}),['items','next_cursor'])
components['SourceMetadataRead']={'oneOf':[obj(dict(status={'const':'produced'},metadata=ref('MetaSet')),['status','metadata']),obj(dict(status={'const':'not-produced'},entry_id=hash64,reason=nonempty),['status','entry_id','reason'])]}
def parameter(name,where,shape,required=True):return dict(name=name,**{'in':where},required=required,schema=shape)
def response(name,description='Success'):
 return dict(description=description,content={'application/json':dict(schema=ref(name))})
def operation(identifier,method,document=None,parameters=()):
 op=dict(operationId=identifier,security=[{'catalogAuth':['catalog:read' if method=='get' else 'catalog:admin']}],parameters=list(parameters),responses={str(code):response('Error',text) for code,text in [(400,'Malformed input or precondition'),(401,'Authentication required'),(403,'Reader scope or protected owner approval denied'),(404,'Resource absent'),(409,'Existing identity, dependency or unresolved import conflict'),(410,'Expired reader/collection/generation cursor'),(412,'Stale strong metadata ETag'),(422,'Schema, reference, source or semantic validation failed'),(428,'Required precondition absent')]})
 if document:op['requestBody']=dict(required=True,content={'application/json':dict(schema=ref(document))})
 return op
for path,key,kind in [('callback-descriptors','callback_id','CallbackDescriptor'),('functional-groups','group_id','FunctionalGroup')]:
 collection='/v1/catalog/'+path;shape=hash64 if key=='callback_id' else dict(type='string',pattern='^fg:[a-z0-9][a-z0-9.-]{1,95}$')
 get=operation('list'+kind+'s','get',parameters=[parameter('limit','query',dict(type='integer',minimum=1,maximum=100,default=50),False),parameter('cursor','query',nonempty,False)])
 get['description']='Bounded keyset list. Authenticated reader, collection, registry and metadata generation pin cursor; changed or cross-reader cursor410. Active records only for readers; non-active and history require protected owner. Administrative active never means invocable.';get['responses']['200']=response('MetadataPage')
 api['paths'][collection]={'get':get}
 identity=parameter(key,'path',shape);etag=parameter('If-Match','header',nonempty,False)
 item={}
 for method in ('get','put','delete'):
  op=operation(('read' if method=='get' else 'replace' if method=='put' else 'delete')+kind,method,'MetadataMutation' if method=='put' else 'MetadataDelete' if method=='delete' else None,[identity]+([etag] if method!='get' else []))
  op['description']='Exact metadata1.0 resource with strong meta1 ETag; identity/collection must match. Non-active records are owner-only. '+('Replace uses current If-Match; conflict requires explicit resolution against base hash and may restore tombstones. Create uses If-None-Match:* at this item URI. No PATCH.' if method=='put' else 'DELETE appends imported suppression or local tombstone, preserving history. Repeating a stale conditional request returns412 without further effect; GET current state before a fresh intent.' if method=='delete' else 'Returns administrative representation, never runtime registration or group activation.')
  if method=='put':
   op['parameters'].append(parameter('If-None-Match','header',{'const':'*'},False))
   op['description']='Create at this ITEM URI using If-None-Match:* (201 or412 if it exists), OR fully replace using one current If-Match (200). Exactly one precondition: missing428, both400. No collection POST or PATCH. '+op['description']
   op['responses']['201']=response('MetadataRepresentation','Created at the target item URI')
  op['responses']['200']=response('MetadataRepresentation');op['responses']['200']['headers']={'ETag':dict(description='Exact strong meta1 generation/kind/identity/base/revision/state/content hash token',schema=nonempty)};item[method]=op
 api['paths'][collection+'/{'+key+'}']=item
 history=operation('read'+kind+'History','get',parameters=[identity,parameter('limit','query',dict(type='integer',minimum=1,maximum=100,default=50),False),parameter('cursor','query',nonempty,False)]);history['security']=[{'catalogAuth':['catalog:admin']}];history['description']='Protected owner only; bounded revision history with the same reader/generation cursor rules. No deletion of history.';history['responses']['200']=response('MetadataPage');api['paths'][collection+'/{'+key+'}/history']={'get':history}
get=operation('readEntrySourceMetadata','get',parameters=[parameter('entry_id','path',hash64)])
get['description']='Follows effective Entry read visibility. Returns produced source evidence or explicit not-produced reason; absent metadata never means no callbacks or unrestricted invocation. Suppressed/deleted entry follows Entry410 behavior.';get['responses']['200']=response('SourceMetadataRead');api['paths']['/v1/catalog/entries/{entry_id}/source-metadata']={'get':get}
api['info']['version']='1.6.0'
note=' Source metadata1.0/descriptor/group CRUD: ../SOURCE-METADATA-CONTRACT.md. Specification and offline reference only; no host or authenticated listener enabled.'
api['info']['description']=api['info']['description'].replace(note,'').rstrip()+note
(C/'openapi.json').write_text(json.dumps(api,indent=2)+'\n')
p=R/'catalog/API-CONTRACT.md';text=p.read_text();marker='## Source metadata, callbacks and functional groups (1.6.0)'
text=text.replace('## Source metadata, callbacks and functional groups (1.5.0)',marker).replace('OpenAPI1.5.0 specifies the routes','OpenAPI1.6.0 specifies the routes')
if marker not in text:text+='\n\n'+marker+'\n\nThe additive source evidence and declarative resource contract is [SOURCE-METADATA-CONTRACT.md](SOURCE-METADATA-CONTRACT.md). Its meta1 ETags, immutable imports, local overrides, source-occurrence variants and protected-owner CRUD are separate from cat1 entry and dv2 device-view tokens. Saving these records never activates a callback or group or grants Android/MCP authority. OpenAPI1.6.0 specifies the routes; Python/SQLite remains an offline oracle, with HTTP hosting and middleware unimplemented.\n'
p.write_text(text)
print(json.dumps(dict(openapi='1.6.0',added_collections=['callback-descriptors','functional-groups'],source_metadata='1.0.0',runtime_implemented=False)))
