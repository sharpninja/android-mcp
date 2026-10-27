"""Persist current execution status without changing the frozen review input."""
import datetime, hashlib, json, pathlib, sqlite3
R=pathlib.Path(__file__).resolve().parent
S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=[]
for release in ('005','006'):
    dbpath=S/'catalog/output'/('release-'+release)/'catalog.sqlite'
    with sqlite3.connect(dbpath.as_uri()+'?mode=ro',uri=True) as db:
        columns=[r[1] for r in db.execute('PRAGMA table_info(annotation_evidence)')]
        source_columns=[r[1] for r in db.execute('PRAGMA table_info(sources)')]
        # Capture only the two annotation-source rows and their resolution counts.
        source_rows=db.execute("SELECT source_id,path FROM sources WHERE kind='sdk-annotations'").fetchall()
        counts=[]
        for sid,path in source_rows:
            matched,unmatched=db.execute('SELECT sum(binding_id IS NOT NULL),sum(binding_id IS NULL) FROM annotation_evidence WHERE source_id=?',(sid,)).fetchone()
            counts.append(dict(source_id=sid,path=path,matched=matched or 0,unmatched=unmatched or 0))
    rows.append(dict(release=release,database=str(dbpath),open_mode='read-only',annotation_evidence_columns=columns,sources_columns=source_columns,annotation_sources=counts))
out=dict(captured_at=now(),received_after_round008_freeze=True,frozen_input_modified=False,production_counts=rows,
 parent_verified_breakdown={'005':{'SDK37.0':99,'SDK37.2':478,'total':577},'006':{'SDK37.0':6509,'SDK37.2':478,'total':6987}},
 explanation='The increase of6410 is entirely SDK37.0 version routing: explicit versions require matching indexes, but only37.2index was imported. It is a regression, not newly ambiguous signatures or merely stricter safety. Counts include IntDef and other annotations, not just permissions.',
 prospective_probe={'uniquely_proven_same_snapshot':6141,'still_unmatched_or_ambiguous_SDK37_0':368,'not_published_production':True,'receipt':str(S/'integration/same-snapshot-annotations-integration-v2.json'),'receipt_sha256':sha(S/'integration/same-snapshot-annotations-integration-v2.json')},
 next_review_requirement='Include this correction and only actual completed later production results; do not reinterpret006as already corrected.')
(R/'evidence/annotation-routing-postfreeze008.json').write_text(json.dumps(out,indent=2)+'\n')
state=json.loads((R/'LIVE-STATUS.json').read_text())
state.update(updated_at=now(),current_phase='Round008 active;125-test candidate frozen with completed006 evidence; no duplicate review',
 review_directory=str(R/'reviews/round-008'),review_pid=2092405,review_phase='Awaiting independent report; provider visible stream active,stderr empty',command_session=None,
 review_process_verified='Host ps confirmed PID2092405 alive at23:06UTC',review_error_log_empty=True,
 primary_input_blocker='006isvalidated but predates post007extractor/DDL fixes; relevant outside paths remain unimported and unknown/native/generated scope unresolved',
 production_target='Completed006:794195entries39194sources1399definitions1639permission names; completefalse',
 source_production_progress=dict(release='006',entries=794195,sources=39194,published=True,validated=True,complete=False),
 migration_validation='evidence/legacy-populated-round008-validation.json',migration_host_pid=None,migration_cpu_check='Completed22:40:59',
 review_visible_assistant_messages=0,review_events=sum(1 for _ in (R/'reviews/round-008/response.visible.jsonl').open()),
 frozen_snapshot_tests=None,frozen_snapshot_tests_passed=None,copy_migration_finished=True,
 annotation_routing_correction='evidence/annotation-routing-postfreeze008.json; received after freeze; SDK37.0 regression6410,prospective6141recovered368remain; no production claim')
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n')
print(json.dumps(dict(status='round008active',pid=2092405,annotation_sources=rows)))
