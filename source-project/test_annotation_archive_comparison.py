import unittest,sqlite3,json
from compare_annotation_archives import summarize
class ArchiveComparisonTests(unittest.TestCase):
    def test_matched_unmatched_counts_and_raw_evidence_are_separate(self):
        db=sqlite3.connect(':memory:');self.addCleanup(db.close)
        db.executescript('CREATE TABLE sources(source_id,path,url,revision,sha256,kind);CREATE TABLE source_details(source_id,document_json);CREATE TABLE parse_reports(source_id,document_json);CREATE TABLE annotation_evidence(source_id,locator,binding_id,scope,document_json);')
        db.execute("INSERT INTO sources VALUES('s','annotations.zip','https://example.invalid','r','hash','sdk-annotations')");db.execute('INSERT INTO source_details VALUES(?,?)',('s',json.dumps(dict(repository='repo',platform_api_version='37.2'))));db.execute('INSERT INTO parse_reports VALUES(?,?)',('s','{"status":"partial"}'))
        raw=json.dumps(dict(annotation=dict(name='android.annotation.RequiresPermission',permission=dict(op='unknown')),target_match_kind='unresolved',target_resolution_reason='ambiguous',item_name='a.A void f(T)'))
        db.execute('INSERT INTO annotation_evidence VALUES(?,?,?,?,?)',('s','x:1',None,'declaration',raw));db.execute('INSERT INTO annotation_evidence VALUES(?,?,?,?,?)',('s','x:2','binding','parameter:0',raw))
        archive=next(iter(summarize(db).values()));self.assertEqual(archive['counts']['total'],2);self.assertEqual(archive['counts']['matched'],1);self.assertEqual(archive['counts']['unmatched'],1);self.assertEqual(archive['counts']['permission-family:total'],2)
if __name__=='__main__':unittest.main()
