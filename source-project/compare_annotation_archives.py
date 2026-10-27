"""Archive-local annotation mapping comparison; unresolved evidence is exported."""
import pathlib,json,sqlite3,collections,datetime,sys
def summarize(db):
    result={}
    for sid,path,url,revision,digest,detail,report in db.execute("SELECT s.source_id,s.path,s.url,s.revision,s.sha256,d.document_json,p.document_json FROM sources s JOIN source_details d USING(source_id) JOIN parse_reports p USING(source_id) WHERE s.kind='sdk-annotations'"):
        source=json.loads(detail);counts=collections.Counter();kinds=collections.Counter();scopes=collections.Counter();reasons=collections.Counter()
        for bid,scope,raw in db.execute('SELECT binding_id,scope,document_json FROM annotation_evidence WHERE source_id=?',(sid,)):
            doc=json.loads(raw);matched='matched' if bid else 'unmatched';counts['total']+=1;counts[matched]+=1;scopes[scope+':'+matched]+=1;kinds[doc.get('target_match_kind','unknown')]+=1
            name=doc['annotation'].get('name','');family=any(token in name for token in ('RequiresPermission','EnforcePermission','PermissionManuallyEnforced','RequiresNoPermission'))
            if family:counts['permission-family:total']+=1;counts['permission-family:'+matched]+=1
            if not bid:reasons[doc.get('target_resolution_reason') or 'unresolved reason unavailable']+=1
        key=(source.get('repository'),revision,path,digest)
        result[key]=dict(source_id=sid,path=path,url=url,revision=revision,sha256=digest,repository=source.get('repository'),platform_api_version=source.get('platform_api_version'),parse_report=json.loads(report),counts=dict(counts),match_kinds=dict(kinds),scopes=dict(scopes),unresolved_reasons=dict(reasons))
    return result
def readonly(path):return sqlite3.connect(path.resolve().as_uri()+'?mode=ro',uri=True)
def run(root,old,new):
    root=pathlib.Path(root).resolve();out=root/('annotation-archive-comparison-'+old+'-to-'+new+'.json');unresolved=root/('unresolved-annotation-evidence-release'+new+'.jsonl')
    if out.exists() or unresolved.exists():raise ValueError('Preserve comparison and unresolved evidence')
    a=readonly(root/'catalog/output'/('release-'+old)/'catalog.sqlite');b=readonly(root/'catalog/output'/('release-'+new)/'catalog.sqlite')
    try:
        before=summarize(a);after=summarize(b);rows=[]
        for key in sorted(set(before)|set(after)):
            x=before.get(key);y=after.get(key);rows.append(dict(acquisition_key=list(key),before=x,after=y,matched_delta=(y or {}).get('counts',{}).get('matched',0)-(x or {}).get('counts',{}).get('matched',0)))
        retained=0
        with unresolved.open('x') as stream:
            for sid,locator,scope,raw in b.execute('SELECT source_id,locator,scope,document_json FROM annotation_evidence WHERE binding_id IS NULL ORDER BY source_id,locator'):
                stream.write(json.dumps(dict(source_id=sid,locator=locator,scope=scope,evidence=json.loads(raw)),sort_keys=True,separators=(',',':'))+'\n');retained+=1
        aidl_counts=collections.Counter();aidl_status=collections.Counter();aidl_roots=0;multi=0
        for raw, in b.execute("SELECT p.document_json FROM parse_reports p JOIN sources s USING(source_id) WHERE s.kind='aidl'"):
            report=json.loads(raw);n=report.get('declaration_count');aidl_counts[str(n) if n is not None else 'not-recorded']+=1;aidl_status[report['status']]+=1
            if n is not None:aidl_roots+=n;multi+=n>1
        result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),baseline_release=old,candidate_release=new,database_mode='read-only',archives=rows,unresolved_evidence_records=retained,unresolved_evidence_file=unresolved.name,aidl=dict(declaration_count_distribution=dict(aidl_counts),status_distribution=dict(aidl_status),top_level_declaration_roots=aidl_roots,multi_declaration_files=multi),complete=False,authority='No OS/client grants or global completeness inferred')
    finally:a.close();b.close()
    out.write_text(json.dumps(result,indent=2));print(json.dumps(dict(path=str(out),unresolved=retained,archives=[dict(path=x['after']['path'],counts=x['after']['counts']) for x in rows if x['after']],aidl=result['aidl']),indent=2));return result
if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent,sys.argv[1],sys.argv[2])
