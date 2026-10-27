import pathlib,json,sys,hashlib,datetime,time
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import source_metadata_reference as m
SRC=R.parent.parent/'task/android-catalog-20261026/integration'
tasks=[
 (SRC/'interface-module-producer-v1/source-interface-evidence.jsonl','SourceInterfaceEvidence',None),
 (SRC/'hidl-source-constraints-v1/constraints.jsonl','HidlConstraint',None),
 (SRC/'aidl-source-constraints-v1/constraints.jsonl','AidlConstraint',None),
 (SRC/'native-abi-source-facts-v1/records.jsonl',None,'record'),
 (SRC/'native-abi-source-facts-v1/type-references.jsonl',None,'type-reference'),
 (SRC/'native-abi-source-facts-v1/callback-parameter-candidates.jsonl',None,'callback-candidate'),
 (SRC/'native-descriptor-source-facts-v1/facts.jsonl',None,'descriptor-candidate'),
 (SRC/'callback-audit-release011-v2/parameter-candidates.jsonl','ParameterCandidate',None),
 (SRC/'callback-audit-release011-v2/endpoint-methods.jsonl','EndpointMethod',None),
 (SRC/'callback-audit-release011-v2/lifecycle-pair-candidates.jsonl','LifecyclePair',None),
 (SRC/'functional-audit-release011-v1/functional-evidence.jsonl','FunctionalFacts',None)]
results=[];failures=[];started=datetime.datetime.now(datetime.timezone.utc).isoformat()
for p,root,kind in tasks:
 t=time.monotonic();h=hashlib.sha256();count=0;bad=0
 with p.open('rb') as f:
  for count,raw in enumerate(f,1):
   h.update(raw)
   try:
    doc=json.loads(raw)
    if root:m.validate(doc,root)
    else:m.native_evidence(kind,doc)
   except (ValueError,KeyError,TypeError) as error:
    bad+=1
    if len(failures)<20:failures.append(dict(file=str(p),line=count,error=str(error)[:1200]))
 result=dict(path=str(p),root=root or kind,rows=count,failures=bad,sha256=h.hexdigest(),elapsed_seconds=time.monotonic()-t);results.append(result);print(json.dumps(result),flush=True)
report=dict(started_at=started,completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),results=results,failures=failures,total_failures=sum(v['failures'] for v in results),scope='Read-only real producer exports; structural contract validity, not device/codec or semantic completeness proof')
(R/'evidence/source-metadata-real-exports-validation.json').write_text(json.dumps(report,indent=2)+'\n');raise SystemExit(1 if report['total_failures'] else 0)

