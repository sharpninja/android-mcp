import datetime,hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');report=json.loads((R/'reviews/round-012/review-report.json').read_text());patch="""--- a/catalog/tooling/build_catalog.py
+++ b/catalog/tooling/build_catalog.py
@@ -173,2 +173,3 @@
     for r in d['members']:
      r['extras']['imports']=d['imports']
+     r['extras']['interface_oneway']=bool(d.get('oneway'))
""".replace('\n++++ b/','\n+++ b/')
p=R/'integration/round012-oneway-producer.patch';p.write_text(patch)
actions=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),completed_review='reviews/round-012/review-report.json',scores=report['scores'],overall=report['overall'],urgent_fix=dict(id='R12-02',patch=str(p),patch_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),exact_action="AIDL member loop: r['extras']['interface_oneway']=bool(d.get('oneway'))",fixture='catalog/tests/test_round11.py:test_builder_propagates_interface_oneway_to_method_contract',before='evidence/round012-oneway-builder-before.txt failure:None!=False',after='evidence/round012-oneway-builder-after.txt:18tests pass; full combined regression next',producer_processing_hash_changes=True),source_findings=[f for f in report['findings'] if f['id'] in ('R12-01','R12-03','R12-07','R12-09','R12-12','R12-15','R12-16')],coordination='No source owner files changed. Source already owns new structured-shape patch, which is byte-guard merged locally and passes255normal+optimized. New wire proof delta is consumer-only; oneway is producer-affecting and must be applied before a new full build freeze. Current review012 remains immutable. Integrate exact line/test preserving newer source work; do not overwrite active/frozenbuild files.',current_work='Reading source-only lifecycle/rawretention/HTTP snapshot helpers forR12-09; interface/module evidence producer and current bounded-scale path still require work; no98claim')
(R/'integration/round012-source-actions.json').write_text(json.dumps(actions,indent=2)+'\n');print(json.dumps(dict(actions=str(R/'integration/round012-source-actions.json'),patch=str(p))))
