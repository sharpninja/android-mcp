import pathlib,sys,re,datetime,json
R=pathlib.Path(__file__).resolve().parent;phase=sys.argv[1]
if not re.fullmatch('[a-z0-9-]+',phase):raise ValueError('Invalid evidence phase')
sys.path.insert(0,str(R/'catalog/tooling'));import migrate_v3,validate_catalog
source=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output/release-002/catalog.sqlite');target=R/'evidence'/('release002-migrated-'+phase+'.sqlite')
print('Copy migration started',datetime.datetime.now(datetime.timezone.utc).isoformat(),flush=True);migrate_v3.migrate(source,target)
print('Full read-only entry/schema/hash/AST/integrity/FK validation started',flush=True)
print(json.dumps(validate_catalog.run_database(target,R/'evidence'/('legacy-populated-'+phase+'-validation.json'))),flush=True)
