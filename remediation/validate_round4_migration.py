import pathlib,sys,json,datetime
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import migrate_v3,validate_catalog
source=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output/release-002/catalog.sqlite');target=R/'evidence/release002-migrated-round4-fixes.sqlite'
print('Copy migration started',datetime.datetime.now(datetime.timezone.utc).isoformat(),flush=True)
migrate_v3.migrate(source,target)
print('Read-only full entry/schema/hash/AST/integrity/FK validation started',flush=True)
report=validate_catalog.run_database(target,R/'evidence/legacy-populated-round4-schema-validation.json')
print(json.dumps(report),flush=True)
