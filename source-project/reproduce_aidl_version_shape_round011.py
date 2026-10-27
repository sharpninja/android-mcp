"""Two versioned source files must not merge their structured type fields."""
import pathlib,sys,sqlite3,json,hashlib,datetime,contextlib
R=pathlib.Path(__file__).resolve().parent;I=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation/integration');sys.path[:0]=[str(I/'round011-v2/tooling'),str(I/'round011-v1/tooling'),str(R/'catalog-source011-hidl-annotations/tooling')]
import catalog_projection,dispatch_manifest
bid=hashlib.sha256(b'aidl:p.Data').hexdigest();sources={};docs={}
with contextlib.closing(sqlite3.connect(':memory:')) as db:
 db.execute('CREATE TABLE binding_declarations(binding_id TEXT,source_id TEXT,document_json TEXT)')
 for version,field_type in [('1','int'),('2','long')]:
  sid='version-'+version;sources[sid]=dict(source_id=sid,repository='fixture',revision='same-immutable-project-commit',kind='aidl',api_level=None,channel='unknown',path='aidl_api/module/'+version+'/p/Data.aidl');doc=dict(entry_id=bid,canonical_signature='aidl:p.Data',kind='aidl-parcelable',name='Data',owner='p.Data',package='p',arguments=[],codec_status='unknown',extensions=dict(language='aidl'),provenance=[dict(raw_signature='parcelable Data')]);docs[sid]=doc
  field=dict(entry_id='field',canonical_signature='aidl:p.Data#x',kind='field',name='x',owner='p.Data',package='p',arguments=[],return_type=dict(text=field_type,status='unknown',codec_status='unknown'),extensions=dict(language='aidl',aidl_declaration_kind='field'),provenance=[dict(raw_signature=field_type+' x;')]);db.executemany('INSERT INTO binding_declarations VALUES (?,?,?)',[(bid,sid,json.dumps(doc)),('field',sid,json.dumps(field))])
 projection=catalog_projection.Projection(db,sources);results=[]
 for sid,doc in docs.items():
  projected=projection.project(doc,sid);results.append(dict(source=sources[sid],shape=projected['extensions']['declared_type_shape'],schema_hash=dispatch_manifest.contract_digest(projected)))
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),projection_path=catalog_projection.__file__,projection_sha256=hashlib.sha256(pathlib.Path(catalog_projection.__file__).read_bytes()).hexdigest(),versioned_types=results,same_hash_despite_different_versioned_fields=results[0]['schema_hash']==results[1]['schema_hash'],action='Direct type fields must be scoped to their declaration source; type-reference cohorts must preserve explicit interface-version/dump provenance. Retain ambiguity instead of merging different layouts.')
(R/'integration/aidl-version-type-shape-round011-reproduction.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
