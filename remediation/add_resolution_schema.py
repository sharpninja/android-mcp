import pathlib,json
C=pathlib.Path(__file__).resolve().parent/'catalog/contracts'
def variant(action,kind):
 return dict(type='object',properties=dict(action=dict(const=action),expected_imported_record_sha256=kind),required=['action','expected_imported_record_sha256'],additionalProperties=False)
resolution=dict(oneOf=[variant('rebase',dict(type='string',pattern='^[0-9a-f]{64}$')),variant('retain-local',dict(type='null'))])
for p in C.glob('*.json'):
 d=json.loads(p.read_text())
 if '$defs' in d and 'Mutation' in d['$defs']:d['$defs']['Mutation']['properties']['x-resolution']=resolution
 if 'components' in d and 'Mutation' in d['components'].get('schemas',{}):d['components']['schemas']['Mutation']['properties']['x-resolution']=resolution
 p.write_text(json.dumps(d,indent=2)+'\n')
