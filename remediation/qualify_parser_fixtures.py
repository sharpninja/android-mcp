import pathlib
p=pathlib.Path(__file__).resolve().parent/'catalog/tests/test_parsers.py';s=p.read_text()
# These grammar fixtures require a proven-name node; unqualified-name rejection
# has its own regression and is no longer a valid setup for the grammar tests.
s=s.replace('"p"','"example.permission.P"').replace("['name'],'p'","['name'],'example.permission.P'")
s=s.replace('"x"','"example.permission.X"').replace('"a","b"','"example.permission.A","example.permission.B"')
p.write_text(s)
