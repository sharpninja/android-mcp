"""Offline future-node reader; generated AST validation remains strict.
Original input bytes remain in immutable import evidence. x-raw-node retains the
exact parsed JSON value, not a claim of preserving lexical whitespace/order.
"""
import pathlib,json,copy,re
import jsonschema
P=pathlib.Path(__file__).resolve().parents[1]
SCHEMA=json.loads((P/'contracts/PermissionExpression.schema.json').read_text())
OPS={node['properties']['op']['const'] for node in SCHEMA['$defs']['PermissionExpression']['oneOf']}
def read(node,schema_version='1.0.0'):
 if not re.fullmatch(r'1\.[0-9]+\.[0-9]+',schema_version):raise ValueError('Unsupported permission major/version')
 def visit(value):
  if not isinstance(value,dict) or value.get('op') not in OPS:
   return {'op':'unknown','reason':'Unrecognized permission semantic node; invocation unsupported','x-raw-node':copy.deepcopy(value)}
  result=copy.deepcopy(value)
  # Only schema-defined semantic edges are visited; x-* raw metadata is data.
  for key in ('children',):
   if key in result and isinstance(result[key],list):result[key]=[visit(child) for child in result[key]]
  for key in ('condition','requirement','read','write'):
   if key in result:result[key]=visit(result[key])
  return result
 result=visit(node);jsonschema.Draft202012Validator(SCHEMA).validate(result);return result
