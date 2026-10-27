import ast,pathlib
R=pathlib.Path(__file__).resolve().parent
for p in (R/'catalog/tests').glob('*.py'):
 data=p.read_bytes();lines=data.splitlines(keepends=True);offsets=[0]
 for line in lines:offsets.append(offsets[-1]+len(line))
 positions=[]
 for node in ast.walk(ast.parse(data)):
  if isinstance(node,ast.Call) and isinstance(node.func,ast.Attribute) and isinstance(node.func.value,ast.Name) and node.func.value.id in ('sdk_api_index','api') and node.func.attr=='parse' and len(node.args)==1 and not any(k.arg=='target' for k in node.keywords):
   pos=offsets[node.end_lineno-1]+node.end_col_offset-1;assert data[pos:pos+1]==b')';positions.append(pos)
 for pos in sorted(positions,reverse=True):data=data[:pos]+b",target='37.2'"+data[pos:]
 if positions:p.write_bytes(data)
