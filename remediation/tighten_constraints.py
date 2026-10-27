import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'migrations/001-initial.sql';s=p.read_text();s=s.replace('registry_version INTEGER NOT NULL UNIQUE','registry_version INTEGER NOT NULL UNIQUE CHECK(registry_version>=1)')
s+='''
CREATE TRIGGER registry_state_transition BEFORE UPDATE OF state ON registries
 WHEN new.state<>old.state AND NOT ((old.state='staged' AND new.state IN ('validated','rejected')) OR (old.state='validated' AND new.state='published') OR (old.state='published' AND new.state='retired'))
 BEGIN SELECT RAISE(ABORT,'Invalid registry state transition'); END;
'''
p.write_text(s)
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text();s=s.replace('CREATE VIEW effective_entries AS', '''CREATE TRIGGER parse_reports_no_update BEFORE UPDATE ON parse_reports BEGIN SELECT RAISE(ABORT,'Imported reports are immutable'); END;
CREATE TRIGGER parse_reports_no_delete BEFORE DELETE ON parse_reports BEGIN SELECT RAISE(ABORT,'Imported reports are immutable'); END;
CREATE VIEW effective_entries AS''');p.write_text(s)
p=C/'tooling/build_catalog.py';s=p.read_text().replace(' db.execute("UPDATE registries SET state=\'published\',validation_json=? WHERE registry_id=?",(js(validation),registry_id))',' db.execute("UPDATE registries SET state=\'validated\',validation_json=? WHERE registry_id=?",(js(validation),registry_id))\n db.execute("UPDATE registries SET state=\'published\' WHERE registry_id=?",(registry_id,))');p.write_text(s)
p=C/'tests/test_remediation.py';s=p.read_text().replace("(str(n),n,'1.2.0'","(str(n),n+1,'1.2.0'");p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
