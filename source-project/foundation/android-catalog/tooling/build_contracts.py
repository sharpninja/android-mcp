"""Rebuild normative JSON Schema and OpenAPI 3.1 documents; no server side effects."""
import json,pathlib,copy
ROOT=pathlib.Path(__file__).resolve().parents[1]/'contracts'
def ref(n):return {'$ref':'#/$defs/'+n}
def obj(props,required=()):return {'type':'object','properties':props,'required':list(required),'additionalProperties':False,'patternProperties':{'^x-':{}}}
S={'type':'string'};DT={'type':'string','format':'date-time'};ID={'type':'string','pattern':'^[0-9a-f]{64}$'}
def node(op,props={},required=()):return obj({'op':{'const':op},**props},('op',*required))
D={}
D['PermissionExpression']={'oneOf':[
 node('permission',{'name':S},('name',)),
 node('all',{'children':{'type':'array','minItems':1,'items':ref('PermissionExpression')}},('children',)),
 node('any',{'children':{'type':'array','minItems':1,'items':ref('PermissionExpression')}},('children',)),
 node('conditional',{'condition':ref('PermissionExpression'),'requirement':ref('PermissionExpression')},('condition','requirement')),
 node('access',{'access':{'enum':['read','write']},'requirement':ref('PermissionExpression')},('access','requirement')),
 node('manual',{'reason':S},('reason',)),node('explicit-none'),
 node('unknown',{'reason':S,'raw':S},('reason',))]}
D['TypeReference']=obj({'text':S,'resolved_binding_id':ID,'status':{'enum':['resolved','primitive','unknown','unsupported']},'reason':S,'codec_status':{'enum':['known','unknown','unsupported']},'codec_id':S},('text','status','codec_status'))
D['Source']=obj({'source_id':ID,'url':{'type':'string','format':'uri'},'path':S,'revision':S,'retrieved_at':DT,'sha256':ID,'api_level':{'type':'integer','minimum':1},'channel':{'enum':['stable','preview','development']},'kind':S,'license':S},('source_id','url','path','revision','retrieved_at','sha256','api_level','channel','kind','license'))
D['Provenance']=obj({'source_id':ID,'line':{'type':'integer','minimum':1},'raw_signature':S,'source_revision':S},('source_id','raw_signature','source_revision'))
D['Entry']=obj({'schema_version':{'const':'1.0.0'},'entry_id':ID,'canonical_signature':S,'kind':{'enum':['package','class','interface','enum','annotation','constructor','method','field','property','aidl-interface','aidl-member','unsupported']},'package':S,'owner':S,'name':S,'arguments':{'type':'array','items':ref('TypeReference')},'return_type':ref('TypeReference'),'permission':ref('PermissionExpression'),'provenance':{'type':'array','items':ref('Provenance')},'origin':{'enum':['imported','local','overlay']},'state':{'enum':['active','suppressed','deleted','conflict']},'registry_version':{'type':'integer','minimum':0},'overlay_revision':{'type':'integer','minimum':0},'codec_status':{'enum':['known','unknown','unsupported']},'unresolved':{'type':'array','items':S},'extensions':{'type':'object'},'required_semantics':{'type':'array','uniqueItems':True,'items':S}},('schema_version','entry_id','canonical_signature','kind','name','permission','provenance','origin','state','registry_version','overlay_revision','codec_status','unresolved','required_semantics'))
D['Mutation']=obj({'schema_version':{'const':'1.0.0'},'canonical_signature':S,'kind':D['Entry']['properties']['kind'],'package':S,'owner':S,'name':S,'arguments':D['Entry']['properties']['arguments'],'return_type':ref('TypeReference'),'permission':ref('PermissionExpression'),'codec_status':D['Entry']['properties']['codec_status'],'unresolved':D['Entry']['properties']['unresolved'],'extensions':{'type':'object'},'required_semantics':D['Entry']['properties']['required_semantics'],'reason':S},('schema_version','canonical_signature','kind','name','permission','codec_status','unresolved','required_semantics','reason'))
D['Error']=obj({'code':{'enum':['invalid_request','unauthorized','forbidden','not_found','gone','conflict','precondition_failed','precondition_required','unsupported_schema','unknown_semantics','validation_failed','idempotency_conflict','cursor_expired']},'message':S,'request_id':S,'details':{'type':'array','items':obj({'path':S,'reason':S},('path','reason'))}},('code','message','request_id'))
D['Page']=obj({'items':{'type':'array','maxItems':500,'items':ref('Entry')},'next_cursor':{'type':['string','null']},'registry_version':{'type':'integer'},'view_version':S},('items','next_cursor','registry_version','view_version'))
D['HistoryEvent']=obj({'entry_id':ID,'revision':{'type':'integer','minimum':1},'operation':{'enum':['create','update','suppress','delete','restore','rebase','conflict']},'actor':S,'occurred_at':DT,'document':ref('Entry')},('entry_id','revision','operation','actor','occurred_at','document'))
D['HistoryPage']=obj({'items':{'type':'array','maxItems':500,'items':ref('HistoryEvent')},'next_cursor':{'type':['string','null']}},('items','next_cursor'))
D['ImportRequest']=obj({'schema_version':{'const':'1.0.0'},'android_version':S,'api_level':{'type':'integer','minimum':1},'source_set_hash':ID,'sources':{'type':'array','minItems':1,'items':ref('Source')},'records_sha256':ID,'local_artifact_id':S},('schema_version','android_version','api_level','source_set_hash','sources','records_sha256','local_artifact_id'))
D['Registry']=obj({'registry_id':S,'registry_version':{'type':'integer','minimum':1},'schema_version':S,'android_version':S,'api_level':{'type':'integer'},'source_set_hash':ID,'state':{'enum':['staged','validated','rejected','published','retired']},'validation':{'type':'object'}},('registry_id','registry_version','schema_version','android_version','api_level','source_set_hash','state','validation'))
D['PublishRequest']=obj({'expected_active_registry_id':{'type':['string','null']},'conflict_policy':{'const':'mark-conflict'}},('expected_active_registry_id','conflict_policy'))
ROOT.mkdir(exist_ok=True)
for name in ('Entry','PermissionExpression','Source','Mutation','Error','ImportRequest'):
    doc={'$schema':'https://json-schema.org/draft/2020-12/schema','$id':'urn:android-binding-catalog:1.0.0:'+name,'$ref':'#/$defs/'+name,'$defs':D}
    (ROOT/(name+'.schema.json')).write_text(json.dumps(doc,indent=2)+'\n')
def api_ref(n):return {'$ref':'#/components/schemas/'+n}
def response(n,description='Success'):
    return {'description':description,'headers':{'ETag':{'description':'Strong opaque revision token for this representation','schema':S}},'content':{'application/json':{'schema':api_ref(n)}}}
errors={str(n):response('Error',d) for n,d in [(400,'Invalid request'),(401,'Authentication required'),(403,'Scope denied'),(404,'Not found'),(409,'Conflict'),(410,'Tombstoned or expired cursor'),(412,'ETag does not match'),(422,'Schema, source or semantic validation failed'),(428,'If-Match required')]}
def op(id,scope,success='Entry',code='200',params=[],body=None,description=''):
    o={'operationId':id,'description':description,'security':[{'catalogAuth':[scope]}],'parameters':params,'responses':{code:response(success),**copy.deepcopy(errors)}}
    if body:o['requestBody']={'required':True,'content':{'application/json':{'schema':api_ref(body)}}}
    return o
etag={'name':'If-Match','in':'header','required':True,'schema':S,'description':'Exact strong ETag. Wildcard is not accepted.'}
idem={'name':'Idempotency-Key','in':'header','required':True,'schema':{'type':'string','minLength':8,'maxLength':200},'description':'Same key and same canonical request hash returns original result. Different hash returns 409.'}
eid={'name':'entry_id','in':'path','required':True,'schema':ID}
rid={'name':'registry_id','in':'path','required':True,'schema':S}
pagination=[{'name':'limit','in':'query','schema':{'type':'integer','minimum':1,'maximum':500,'default':100}},{'name':'cursor','in':'query','schema':S,'description':'Opaque cursor pinned to registry and overlay view generation; never an offset'}]
paths={
 '/v1/catalog/entries':{
  'get':op('searchEntries','catalog:read','Page',params=pagination+[{'name':'package','in':'query','schema':S},{'name':'owner','in':'query','schema':S},{'name':'name','in':'query','schema':S},{'name':'include_suppressed','in':'query','schema':{'type':'boolean','default':False}}],description='Bounded indexed search of effective catalog view. Inclusion of suppressed records requires admin scope. Stable ordering by entry_id within pinned view.'),
  'post':op('createLocalEntry','catalog:admin',code='201',params=[idem],body='Mutation',description='Create local overlay entry only. Server derives canonical ID. Imported ID collision returns 409; use PUT with current ETag to create an override.')},
 '/v1/catalog/entries/{entry_id}':{
  'get':op('readEffectiveEntry','catalog:read',params=[eid],description='Return effective entry plus ETag. Suppressed/deleted entries return 410; history remains available to admins.'),
  'put':op('replaceOverlay','catalog:admin',params=[eid,etag],body='Mutation',description='Full replacement of local overlay document; imported source immutable. Canonical identity cannot change. Create history atomically. Conflicted or suppressed entries may be explicitly restored against their current admin history ETag after rebase validation.'),
  'delete':op('suppressEntry','catalog:admin',params=[eid,etag],description='Create suppression tombstone for imported entry or deleted tombstone for local entry, retaining provenance and history. Response is tombstone Entry and new ETag. No recursive cascade.')},
 '/v1/catalog/entries/{entry_id}/history':{'get':op('readEntryHistory','catalog:admin','HistoryPage',params=[eid,*pagination],description='Append-only history. Response ETag is current overlay revision, usable for explicit restore/rebase PUT.')},
 '/v1/catalog/imports':{'post':op('stageImport','catalog:admin','Registry','201',params=[idem],body='ImportRequest',description='Stage hash-pinned artifact already present locally. No URL fetching or file-system path traversal. Validate hashes, source signatures, references, schema and full coverage report before eligible publication. Retry is idempotent; no active-view changes.')},
 '/v1/catalog/registries/{registry_id}':{'get':op('readRegistry','catalog:admin','Registry',params=[rid])},
 '/v1/catalog/registries/{registry_id}/publish':{'post':op('publishRegistry','catalog:admin','Registry',params=[rid,etag,idem],body='PublishRequest',description='Atomically compare active pointer, validate staged registry and compute overlay conflicts; swap pointer and append audit/history in one SQLite transaction. Unknown enforcement semantics stay non-executable. No partial publish. Rollback on failure. If-Match is staged registry ETag; expected_active_registry_id protects active-pointer race.')}
}
api={'openapi':'3.1.0','info':{'title':'Android Binding Catalog Administration Contract','version':'1.0.0','description':'Specification only. No HTTP server, authentication provider or remote listener enabled. Catalog administration never grants Android invocation permission.'},'paths':paths,'components':{'securitySchemes':{'catalogAuth':{'type':'oauth2','description':'Abstract scope contract; deployment and issuer remain unspecified. Do not enable remote access from this document.','flows':{'clientCredentials':{'tokenUrl':'https://catalog.invalid/not-configured/token','scopes':{'catalog:read':'Read catalog metadata','catalog:admin':'Manage local overlays and imports; no invocation grant'}}}}},'schemas':json.loads(json.dumps(D).replace('#/$defs/','#/components/schemas/'))}}
(ROOT/'openapi.json').write_text(json.dumps(api,indent=2)+'\n')
