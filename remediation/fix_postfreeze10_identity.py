import hashlib,pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'migrations/001-initial.sql';s=p.read_text();old='CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY,';assert old in s
s=s.replace(old,"CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY CHECK(length(entry_id)=64 AND entry_id NOT GLOB '*[^0-9a-f]*'),");p.write_text(s)
(C/'schema.sql').write_text(s+(C/'migrations/002-evidence-and-publication.sql').read_text())
for name,literal in [('test_round3.py','local'),('test_round4.py','l'),('test_round7.py','local')]:
    p=C/'tests'/name;s=p.read_text().replace("'"+literal+"'","'"+hashlib.sha256(('java:a.A' if literal in ('local','l') else literal).encode()).hexdigest()+"'");p.write_text(s)
p=C/'tests/test_round9_review.py';s=p.read_text().replace("entry=B if imported else 'local:fixture'","entry=B if imported else hashlib.sha256(b'local:fixture').hexdigest()");p.write_text(s)
p=C/'tests/test_round2.py';s=p.read_text().replace("VALUES('wrong','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0'","VALUES('"+hashlib.sha256(b'java:wrong').hexdigest()+"','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0'");p.write_text(s)
