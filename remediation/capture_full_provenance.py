import pathlib,json,hashlib,datetime,sys,shutil,collections
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');C=R/'catalog';release=sys.argv[1];B=S/'build-snapshots'/('release'+release)/'catalog';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def relevant(root):
 return {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix in ('.py','.json','.sql','.md','.txt') and ('tests' not in p.relative_to(root).parts)}
rows=[]
for name in sorted(relevant(B)|relevant(C)):
 p=B/name;q=C/name;rows.append(dict(file=name,production_present=p.exists(),production_sha256=sha(p) if p.exists() else None,candidate_present=q.exists(),candidate_sha256=sha(q) if q.exists() else None,matches_candidate=p.exists() and q.exists() and sha(p)==sha(q)))
inventory=S/('source-inventory-release'+release+'.json');status_path=S/('build-release'+release+'-status.json');status=json.loads(status_path.read_text())
report=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=release,production_code_root=str(B),inventory_sha256=sha(inventory),build_inputs_sha256=sha(S/('build-release'+release+'-inputs.json')),files=rows,production_equals_latest_candidate=all(row['matches_candidate'] for row in rows),ddl_matches_candidate=all(row['matches_candidate'] for row in rows if row['file'].endswith('.sql')),note='Includes union of producer/candidate tooling, contracts, DDL, migrations and documentation; absent new modules explicitly false. Old005DDL predates IMM01/F11;006 also predates R7fixes. No prior release is relabeled as using these fixes.',pipeline_status=status)
(E/('production'+release+'-full-code-provenance.json')).write_text(json.dumps(report,indent=2))
for prefix in ('production-reconciliation-release','export-reconciliation-release','coverage-manifest-release','build-release'):
 name=prefix+release+('-status.json' if prefix=='build-release' else '.json')
 if (S/name).exists():shutil.copyfile(S/name,E/name)
validation=S/('validation-report-release'+release+'.json')
if validation.exists():shutil.copyfile(validation,E/validation.name)
native=S/'catalog/output'/('release-'+release)/'validation.json'
if native.exists():
 raw=native.read_bytes();data=json.loads(raw);source_reports=data.pop('source_reports');groups={};mismatches=[]
 for item in source_reports:
  group=groups.setdefault(item['kind'],dict(source_reports=0,statuses={},numeric_fields={},other_values={}))
  group['source_reports']+=1;state=item['status'];group['statuses'][state]=group['statuses'].get(state,0)+1
  for name,value in item.items():
   if name in ('source_id','kind','status'):continue
   if isinstance(value,(int,float)) and not isinstance(value,bool):
    n=group['numeric_fields'].setdefault(name,dict(total=0,min=value,max=value));n['total']+=value;n['min']=min(n['min'],value);n['max']=max(n['max'],value)
   else:
    values=group['other_values'].setdefault(name,[])
    if value not in values:values.append(value)
  if item.get('expected_declarations') is not None and item['declarations']!=item['expected_declarations']:mismatches.append(item)
 summary=dict(**data,report_count=len(source_reports),source_reports_by_kind=groups,expected_declaration_mismatches=mismatches,full_report_sha256=hashlib.sha256(raw).hexdigest(),full_report_bytes=len(raw),scope_note='Complete top-level native invariants plus per-kind/status and numeric aggregate/range summary. Individual source-ID rows not inlined; no omitted requirement or claim of semantic rebuild with later code.')
 (E/('built-in-validation-summary-release'+release+'.json')).write_text(json.dumps(summary,indent=2))
print(json.dumps(dict(release=release,provenance_files=len(rows),pipeline_status=status['status'],matching=sum(row['matches_candidate'] for row in rows))))
