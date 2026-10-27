import datetime,hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent
report=R/'reviews/round-008/review-report.json';doc=json.loads(report.read_text())
target=R/'integration/round008-source-findings.json'
target.write_text(json.dumps(dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),report=str(report),report_sha256=hashlib.sha256(report.read_bytes()).hexdigest(),status='Regression-first fixes in progress; patch not yet ready',source_files_modified=False,
 findings=[f for f in doc['findings'] if f['id'] in ('R8-01','R8-02','R8-03','R8-05','R8-06')],
 coordination='Source owner retains active007checkpoint. Nested AIDL fix, Java qualification/visibility, overlay write-time CAS and cache regeneration required for later corrected build. No request to overwrite active code/data.',
 reader_followup='integration/round008-reader-actions.json:offline-only P8-01,128tests'),indent=2)+'\n')
print(str(target))
