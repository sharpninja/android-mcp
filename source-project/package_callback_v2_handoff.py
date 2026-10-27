import pathlib,json,hashlib,datetime,zipfile
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/callback-audit-release010-v2'
report=json.loads((D/'report.json').read_text());Z=R/'integration/callback-functional-producer-v2-code-evidence.zip'
files=['callback_source_facts.py','test_callback_source_facts.py','functional_source_facts.py','test_functional_source_facts.py','audit_callback_catalog.py','audit_functional_taxonomy.py','integration/callback-source-facts-v3-tests.log','integration/functional-source-facts-v1-tests.log','integration/callback-audit-release010-v2/report.json','integration/functional-audit-release010-v1/report.json','integration/functional-audit-release010-v1/functional-evidence.jsonl','integration/SOURCE-BUILD-INTERFACE.md']
with zipfile.ZipFile(Z,'w',zipfile.ZIP_DEFLATED) as z:
 for name in files:z.write(R/name,name)
 z.writestr('README.md','''# Callback/functional producer v2

Supersedes v1 callback reply-mode claims. AIDL synchronous requires both explicit
and inherited oneway flags known false; absent inherited flags remain unknown.
HIDL method flags are handled separately.13callback+6functional fixtures pass.
The complete regenerated callback baseline remains at callback-audit-release010-v2
in the source task's integration directory; its report lists all byte hashes.
This compact code/evidence ZIP intentionally omits large callback JSONL streams;
none are deleted or dropped from the local full baseline. Functional service
annotation facts are included. All role/registration/removal/lifecycle pair
heuristics remain candidates, not executable stream setup. No upload occurred.
''')
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),supersedes='integration/callback-functional-producer-v1-actions.json only for callback reply semantics; old files preserved',code_archive=str(Z),code_archive_bytes=Z.stat().st_size,code_archive_sha256=hashlib.sha256(Z.read_bytes()).hexdigest(),full_callback_baseline=str(D),callback_counts=report['counts'],full_baseline_files=report['files'],tests=dict(callback=13,functional=6),active011_unchanged=True,uploaded=False)
(R/'integration/callback-functional-producer-v2-actions.json').write_text(json.dumps(receipt,indent=2));print(json.dumps({k:v for k,v in receipt.items() if k not in ('full_baseline_files','callback_counts')},indent=2))
