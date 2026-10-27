"""Apply additive schemas to an isolated catalog; preserve unrelated fields."""
import pathlib,json,sys
D=pathlib.Path(__file__).resolve().parent;P=pathlib.Path(sys.argv[1]).resolve();a=json.loads((D/'schema-additions.json').read_text())
e=json.loads((P/'contracts/Entry.schema.json').read_text());e['$defs'].update(a['entry_defs']);e['$defs']['Entry']['properties'].setdefault('extensions',{'type':'object'}).setdefault('properties',{})['source_contract_metadata']=a['entry_extension'];(P/'contracts/Entry.schema.json').write_text(json.dumps(e,indent=2)+'\n')
o=json.loads((P/'contracts/openapi.json').read_text());o['components']['schemas'].update(a['openapi_components']);o['components']['schemas']['Entry']['properties'].setdefault('extensions',{'type':'object'}).setdefault('properties',{})['source_contract_metadata']={'$ref':'#/components/schemas/MetaSet'};o['paths'].update(a['openapi_paths']);o['info']['version']='1.5.0';(P/'contracts/openapi.json').write_text(json.dumps(o,indent=2)+'\n')
