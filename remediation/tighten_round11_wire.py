import pathlib
R=pathlib.Path(__file__).resolve().parent;p=R/'catalog/bridge-config.sql';s=p.read_text()
old="json_type(response_codec_json)='object' AND coalesce"
new="json_type(response_codec_json)='object' AND coalesce(json_type(argument_codec_json,'$.codec_id')='text' AND length(json_extract(argument_codec_json,'$.codec_id'))>0 AND json_extract(argument_codec_json,'$.schema_hash')=schema_hash,0) AND coalesce(json_type(response_codec_json,'$.codec_id')='text' AND length(json_extract(response_codec_json,'$.codec_id'))>0 AND json_extract(response_codec_json,'$.schema_hash')=schema_hash,0) AND coalesce"
assert old in s;s=s.replace(old,new)
s+="\n"
p.write_text(s);start=s.index('-- The offline loader verifies canonical SHA256');end=s.rindex('COMMIT;');(R/'device_view_ddl.sql').write_text(s[start:end])
p=R/'catalog/API-CONTRACT.md';s=p.read_text();s+='\nA verified wire row must provide object codecs with nonempty codec_id and matching final schema_hash, plus nonempty evidence. The tuple (binding,schema,descriptor,transaction_code,nullable-interface-version) is unique; transaction codes/flags are uint32. These structural checks do not verify codec implementation or confer invocation authority: only a trusted adapter with complete proven ABI/layout may admit the row. Unknown/unsupported proof states remain visible.\n';p.write_text(s)
print('Empty verified codec descriptions reject; source sidecar DDL synchronized')
