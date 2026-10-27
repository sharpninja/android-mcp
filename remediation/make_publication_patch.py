"""Produce a focused integration patch; never edit the source task's active files."""
import pathlib,difflib,hashlib,json
R=pathlib.Path(__file__).resolve().parent;P=R/'integration';P.mkdir(exist_ok=True)
source=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/tooling/build_catalog.py')
old=source.read_text();new=old
anchor=" if validation['integrity_check']!='ok' or validation['foreign_key_check']:raise ValueError('Integrity/FK validation failed')"
gate=""" # Schema validation MUST precede publication, not merely report on it afterward.
 import jsonschema
 entry_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text()))
 ast_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/PermissionExpression.schema.json').read_text()))
 for bid,document in db.execute('SELECT entry_id,document_json FROM effective_entries'):
  errors=list(entry_validator.iter_errors(json.loads(document)))
  if errors:raise ValueError('Entry schema rejected '+bid+': '+errors[0].message)
 for (expression,) in db.execute('SELECT expression_json FROM permission_relations'):
  errors=list(ast_validator.iter_errors(json.loads(expression)))
  if errors:raise ValueError('Permission schema rejected: '+errors[0].message)
 validation['json_schema_failures']=[]
"""
assert new.count(anchor)==1;new=new.replace(anchor,gate+anchor)
anchor="db.close();os.replace(out/'catalog.staging.sqlite',out/'catalog.sqlite')\n db=sqlite3.connect(out/'catalog.sqlite');db.row_factory=sqlite3.Row"
assert new.count(anchor)==1;new=new.replace(anchor,"db.close()\n db=sqlite3.connect(out/'catalog.staging.sqlite');db.row_factory=sqlite3.Row")
anchor="db.close();(out/'validation.json').write_text(json.dumps(validation,indent=2));print("
assert new.count(anchor)==1;new=new.replace(anchor,"db.close();(out/'validation.json').write_text(json.dumps(validation,indent=2))\n # Publish only after schema validation, integrity gates and all exports complete.\n os.replace(out/'catalog.staging.sqlite',out/'catalog.sqlite')\n print(")
(P/'publication-gate.patch').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/catalog/tooling/build_catalog.py',tofile='b/catalog/tooling/build_catalog.py')))
(P/'build_catalog.publication-gate-only.py').write_text(new)
(P/'publication-gate-manifest.json').write_text(json.dumps(dict(source_path=str(source),source_sha256=hashlib.sha256(old.encode()).hexdigest(),patched_sha256=hashlib.sha256(new.encode()).hexdigest(),scope='Schema/AST validation before commit; catalog.sqlite final publication marker after exports. No parser, ETag, storage vocabulary, acquisition or active source writes.'),indent=2))
print(P/'publication-gate.patch')
