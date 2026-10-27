"""Declaration-cache producer contract2. Producers and readers use this helper.
Frozen code is required throughout a run; caches are never relabeled on upgrade.
"""
import pathlib,json,hashlib,sys,importlib.metadata,pyexpat,functools
P=pathlib.Path(__file__).resolve().parent
@functools.lru_cache(maxsize=2)
def current(kind):
 if kind=='java-source':
  names=['java_source.py','parsers.py','foundation_extract.py'];libraries={name:importlib.metadata.version(name) for name in ('tree-sitter','tree-sitter-java')}
 elif kind=='sdk-api-index':names=['sdk_api_index.py'];libraries={'expat':pyexpat.EXPAT_VERSION}
 else:raise ValueError('No cache producer contract for '+kind)
 names+=['cache_fingerprint.py']
 contract=dict(cache_contract=2,kind=kind,python=list(sys.version_info[:2]),libraries=libraries,code={name:hashlib.sha256((P/name).read_bytes()).hexdigest() for name in names})
 return hashlib.sha256(json.dumps(contract,sort_keys=True,separators=(',',':')).encode()).hexdigest()
if __name__=='__main__':print(current(sys.argv[1]))
