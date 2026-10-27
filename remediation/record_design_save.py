import json,pathlib,subprocess,datetime
root=pathlib.Path(__file__).resolve().parent
receipt=json.loads((root/'design/library-write-receipt.json').read_text())
for row in receipt['results']:
    path=root/'design'/row['file_name']
    subprocess.run(['/usr/bin/python3','/tmp/task3-design-library-20261027/library_file_transfer.py','apply-xattrs',str(path),row['library_file_id']],input=json.dumps(row.get('xattrs',[])),text=True,check=True)
p=root/'design/design-update-local-receipt.json';d=json.loads(p.read_text());d.update(library_write_attempted=True,library_write_outcome='all_three_replacements_confirmed',library_write_receipt='design/library-write-receipt.json');p.write_text(json.dumps(d,indent=2)+'\n')
p=root/'LIVE-STATUS.json';d=json.loads(p.read_text());d.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),phase='Design Library replacements confirmed; round009 remediation in progress',design_library_versions={r['library_file_id']:r['current_version_number'] for r in receipt['results']},new_library_uploads='Only three explicitly authorized existing design items replaced; evidence bundle remains unuploaded',library_upload='design/library-write-receipt.json',candidate_tests=157,new_requirement_scope='Startup device view/reference added after009freeze; fresh review pending');p.write_text(json.dumps(d,indent=2)+'\n')
