import pathlib,json,datetime,os
R=pathlib.Path(__file__).resolve().parent;D=R/'design'
r=json.loads((D/'library-write-receipt-callback-groups.json').read_text())
doc=json.loads((D/'library-write-docx-callback-recovery.json').read_text())['metadata']
doc=dict(status='succeeded',library_file_id=doc['library_file_id'],file_id=doc['file_id'],current_version_number=int(doc['version_id']),file_name=doc['name'],size_bytes=doc['size_bytes'],local_xattrs_apply_exit=0,verified_by='Fresh Library read after local receipt-writing error; no duplicate write')
ziprow=dict(status='succeeded',library_file_id='libfile_b09763c5ad1c81918c28b43e12671137',file_id='file_00000000455c81f5b85499825fb92117',current_version_number=4,file_name='Android MCP Bridge Design Portable.zip',size_bytes=5230259,verified_by='Fresh Library read confirms successful original ZIP write; no duplicate write')
# Restore the local version identity from the confirmed read after the tool response envelope differed.
os.setxattr(D/ziprow['file_name'],'user.library-file-version',b'4');ziprow['local_version_xattr_verified']=os.getxattr(D/ziprow['file_name'],'user.library-file-version').decode()=='4'
r['results'] += [doc,ziprow];r['completed_at']=datetime.datetime.now(datetime.timezone.utc).isoformat();r['partial_local_receipt_errors_recovered']=True
(D/'library-write-receipt-callback-groups.json').write_text(json.dumps(r,indent=2)+'\n')
live=json.loads((R/'LIVE-STATUS.json').read_text());live.update(updated_at=r['completed_at'],design_library_versions={v['library_file_id']:v['current_version_number'] for v in r['results']},library_upload='design/library-write-receipt-callback-groups.json',new_library_uploads='Only three owned design replacements saved: Markdown7 DOCX5 ZIP4.45pages7diagrams31tables. No evidence/catalog/source upload.',current_phase='Updated design delivered; freeze new design-only hostile review now',candidate_tests=271,current_tests_optimized='271pass; evidence/tests-regression-085.receipt.json',optimized_tests_evidence='evidence/tests-regression-085.receipt.json')
(R/'LIVE-STATUS.json').write_text(json.dumps(live,indent=2)+'\n')
print(json.dumps([dict(library_file_id=v['library_file_id'],version=v['current_version_number'],file_id=v['file_id']) for v in r['results']]))
