"""Read-only prospective link evidence; never mutates the frozen catalog."""
import pathlib,json,sqlite3,sys,re,hashlib,collections,datetime
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog-source-followup/tooling'))
from parsers import typ,split_top,sha
from annotation_link import fallback_supported,same_snapshot_source_ids,snapshot_declared_type_matches
out=R/(sys.argv[1] if len(sys.argv)>1 else 'same-snapshot-production-probe.json')
if out.exists():raise ValueError('Preserve previous probe')
db=sqlite3.connect((R/'catalog/output/release-006/catalog.sqlite').as_uri()+'?mode=ro',uri=True)
try:
    sources={sid:json.loads(doc) for sid,doc in db.execute('SELECT source_id,document_json FROM source_details')};counts=collections.Counter();examples=[]
    versions={str(s.get('platform_api_version')) for s in sources.values() if s['kind']=='sdk-api-index'}
    for sid,locator,raw in db.execute('SELECT source_id,locator,document_json FROM annotation_evidence WHERE binding_id IS NULL'):
        source=sources[sid];platform=source.get('platform_api_version')
        if platform is None or str(platform) in versions:continue
        doc=json.loads(raw);target=re.sub(r'\s+\d+$','',doc['item_name']);owner,sep,tail=target.partition(' ');canonical=None
        if not sep:canonical=owner
        elif '(' in tail:
            a=tail.find('(');b=tail.rfind(')');m=re.search(r'([\w$]+)$',tail[:a]);name=m.group(1) if m else None
            if name:canonical=owner+'#'+('<init>' if name==owner.split('.')[-1] else name)+'('+','.join(typ(x) for x in split_top(tail[a+1:b]) if x.strip())+')'
        else:canonical=owner+'#'+tail.split()[-1]
        candidates=[canonical] if canonical else []
        if sep and canonical and '(' in canonical:
            head,args=canonical.split('(',1);short=re.sub(r'(?<![\w.$])java\.lang\.','',args)
            if short!=args:candidates.append(head+'('+short)
        elif not sep:candidates=[owner,'package:'+owner]
        matches=[]
        for candidate in dict.fromkeys(candidates):
            bid=sha('java:'+candidate);eligible=same_snapshot_source_ids(db,bid,source,sources)
            if eligible and snapshot_declared_type_matches(db,bid,doc['item_name'],eligible,sha) and (candidate==canonical or not sep or fallback_supported(db,bid,canonical,sources,sha,eligible)):matches.append((candidate,bid))
        status='uniquely-proven-same-snapshot' if len(matches)==1 else 'still-unmatched-or-ambiguous';counts[status]+=1
        if len(matches)==1 and len(examples)<20:examples.append(dict(source_id=sid,locator=locator,item_name=doc['item_name'],candidate_entry_id=matches[0][1],platform=platform,snapshot_revision=source['revision']))
finally:db.close()
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_release='006',database_mode='read-only',prospective_only=True,counts=dict(counts),examples=examples,earlier_snapshots_modified=False)
out.write_text(json.dumps(result,indent=2));print(json.dumps(dict(counts=dict(counts),prospective_only=True),indent=2))
