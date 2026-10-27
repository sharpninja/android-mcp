"""Regenerate exact historical Java caches from restored frozen source bytes.
No network; uses only the supplied pinned task parser, never imported Android code.
"""
import pathlib,sys,json,gzip,hashlib,argparse,datetime

def write_cache(target,records,expected=None):
    target=pathlib.Path(target)
    if target.exists():raise FileExistsError('Refusing to replace existing cache '+str(target))
    target.parent.mkdir(parents=True,exist_ok=True);tmp=target.with_suffix(target.suffix+'.tmp')
    if tmp.exists():raise FileExistsError('Preserve incomplete diagnostic cache '+str(tmp))
    with gzip.GzipFile(filename=str(tmp),mode='wb',mtime=0,compresslevel=6) as stream:
        for record in records:stream.write((json.dumps(record,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode())
    digest=hashlib.sha256(tmp.read_bytes()).hexdigest()
    if expected is not None and digest!=expected:raise ValueError('Reproduced cache differs from frozen SHA256; diagnostic file retained: '+str(tmp))
    tmp.replace(target);return digest

def run(inventory,producer,limit=None):
    inv=pathlib.Path(inventory).resolve();root=inv.parent;producer=pathlib.Path(producer).resolve()
    sys.path.insert(0,str(producer));import java_source,parsers
    parser_hash=hashlib.sha256(pathlib.Path(java_source.__file__).read_bytes()+pathlib.Path(parsers.__file__).read_bytes()).hexdigest()
    sources=[s for s in json.loads(inv.read_text())['sources'] if s['kind']=='java-source' and s['status']=='downloaded']
    if limit:sources=sources[:limit]
    done=0;cached=0;seen=set();records=0
    for s in sources:
        cache=s['metadata_cache']
        if cache['parser_sha256']!=parser_hash:raise ValueError('Historical parser fingerprint mismatch')
        target=(root/cache['path']).resolve();blob=(root/s['local_path']).resolve()
        if root not in target.parents or root not in blob.parents:raise ValueError('Path escapes restoration root')
        if str(target) in seen:continue
        seen.add(str(target));data=blob.read_bytes()
        if hashlib.sha256(data).hexdigest()!=s['sha256']:raise ValueError('Frozen source SHA256 mismatch')
        if target.exists():
            if hashlib.sha256(target.read_bytes()).hexdigest()!=cache['sha256']:raise ValueError('Existing cache differs; preserve it')
            cached+=1;continue
        parsed=java_source.parse(data,s['path']);rows=parsed['records']
        if parsed['parse_errors'] or len(rows)!=cache['records']:raise ValueError('Historical declaration count/parse status mismatch')
        write_cache(target,rows,cache['sha256']);done+=1;records+=len(rows)
        if done%500==0:print('restored Java caches',done,flush=True)
    result=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),sources_selected=len(sources),unique_caches=len(seen),regenerated=done,existing_verified=cached,regenerated_records=records,historical_parser_sha256=parser_hash,exact_frozen_cache_hashes_verified=True)
    print(json.dumps(result,indent=2));return result

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('inventory');p.add_argument('producer');p.add_argument('--limit',type=int);a=p.parse_args();run(a.inventory,a.producer,a.limit)
