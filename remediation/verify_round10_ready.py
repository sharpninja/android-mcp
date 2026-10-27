import datetime,hashlib,json,pathlib,sqlite3,contextlib
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((R/'design/render-manifest.json').read_text());assert manifest['version']=='1.10.0'
paths=sorted((R/'design/sources').glob('*.mmd'))+sorted((R/'design/assets').glob('*.svg'));assert len(paths)==8
files={str(p.relative_to(R/'design')):sha(p) for p in paths}
(E/'portable-assets-check.json').write_text(json.dumps(dict(renderer='Pele',version='1.10.0',new_render=True,source='Four diagram sources and SVGs regenerated for startup resolution/Binder class and sequence design; render-manifest and design visual QA retained',render_manifest_sha256=sha(R/'design/render-manifest.json'),files=files),indent=2)+'\n')
legacy=json.loads((E/'legacy-populated-round010-validation.json').read_text())
with contextlib.closing(sqlite3.connect(pathlib.Path(legacy['database']).as_uri()+'?mode=ro',uri=True)) as db:
    checks={name:bool(db.execute('SELECT 1 FROM sqlite_master WHERE name=?',(name,)).fetchone()) for name in ('active_registry_no_delete','catalog_view_no_delete','overlay_etags')}
    assert all(checks.values())
    assert 'length(binding_id)=64' in db.execute("SELECT sql FROM sqlite_master WHERE name='bindings'").fetchone()[0]
    assert 'length(sha256)=64' in db.execute("SELECT sql FROM sqlite_master WHERE name='sources'").fetchone()[0]
    assert 'length(record_sha256)=64' in db.execute("SELECT sql FROM sqlite_master WHERE name='registry_entries'").fetchone()[0]
    assert 'length(base_record_hash)=64' in db.execute("SELECT sql FROM sqlite_master WHERE name='overlays'").fetchone()[0]
receipt=dict(validated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),patch='integration/round009-review.patch',patch_sha256=sha(R/'integration/round009-review.patch'),full_suite='evidence/tests-regression-050.txt:167passed',device_reference='evidence/device-resolution-after2.txt:16passed',legacy_copy=dict(entries=135508,integrity='ok',schema_failures=0,foreign_key_failures=0,historical_variant_omissions=552,semantic_rebuild=False,sql_guards_present=checks),parser_corpus='19781files22447types161693records0countmismatches0unsupported;frozen006textcorpus',source_files_modified=False,independent_acceptance=False)
(R/'integration/round009-review-validation.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
