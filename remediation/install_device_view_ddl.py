import pathlib
R=pathlib.Path(__file__).resolve().parent;p=R/'catalog/bridge-config.sql';s=p.read_text()
assert 'CREATE TABLE device_views(' not in s
s='\n'.join(line for line in s.splitlines() if not line.startswith(('CREATE TABLE binding_availability(','CREATE TRIGGER availability_target_insert ','CREATE TRIGGER availability_target_update ')))+'\n'
assert s.count('COMMIT;')==1
s=s.replace('COMMIT;',(R/'device_view_ddl.sql').read_text()+'\nCOMMIT;');p.write_text(s)
