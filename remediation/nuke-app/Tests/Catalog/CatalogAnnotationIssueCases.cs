using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogAnnotationIssueCall(string Variant,string Member,byte[] MemberBytes,bool IncludeOriginalTargets);
public sealed record CatalogAnnotationIssueReply(int EvidenceRows,int UnmatchedRows,int ExternalPermissionRelations,int SqlIssueRows,
    string SqlCategory,string SqlRaw,bool SqlReasonNamesMember,string FlatStatus,int FlatXmlDocuments,int FlatItems,int FlatAnnotations,
    int FlatMappedAnnotations,int FlatUnmatchedAnnotations,bool NestedMemberLocator,bool NestedRawBytesExact,bool OriginalInputsRetained,int PlatformIssueRows=0);
public sealed record CatalogAnnotationIssueCase(string Id,CatalogAnnotationIssueCall Call,CatalogAnnotationIssueReply Expected);
public interface ICatalogAnnotationIssueCaseProvider { Task<CatalogAnnotationIssueReply> ImportAsync(CatalogAnnotationIssueCall call); }
public static class CatalogAnnotationIssueCases
{
    // Round2.test_external_annotations_scope_mapping_and_parse_error uses the
    // three exact target strings below, valid.xml and malformed.xml='<bad'.
    // Extra two raw controls pin existing build_catalog.py member persistence:
    // nonXML raw is its filename; malformed member SQL uses errors='replace'.
    // Lossless Source member evidence remains an independent assertion.
    public static readonly string[] OriginalTargets=["a.A void go(int)","a.A void go(int) 0","a.Missing void x()"];
    public const string EvidenceSql="SELECT count(*) FROM annotation_evidence";
    public const string UnmatchedSql="SELECT count(*) FROM annotation_evidence WHERE binding_id IS NULL";
    public const string PermissionsSql="SELECT count(*) FROM permission_relations WHERE annotation='external'";
    public const string OriginalIssueSql="SELECT count(*) FROM issues WHERE category='annotation-xml'";
    public const string MemberSql="SELECT category,reason,raw FROM issues WHERE source_id=$source AND category IN ('annotation-xml','unsupported') ORDER BY id";
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogAnnotationIssueCase("ANN-SQL-01-original-round2-three-targets",new("round2","malformed.xml",Encoding.UTF8.GetBytes("<bad"),true),
            new(3,1,1,1,"annotation-xml","<bad",true,"partial",1,3,3,2,1,true,true,true,PlatformIssueRows:1)),
        new CatalogAnnotationIssueCase("ANN-SQL-02-unsupported-member-filename",new("unsupported","README.txt",Encoding.UTF8.GetBytes("not XML evidence"),false),
            new(0,0,0,1,"unsupported","README.txt",false,"parsed",0,0,0,0,0,true,false,true)),
        new CatalogAnnotationIssueCase("ANN-SQL-03-replacement-SQL-lossless-nested",new("invalid-utf8","malformed.xml",[60,98,97,100,255],false),
            new(0,0,0,1,"annotation-xml","<bad\ufffd",true,"partial",0,0,0,0,0,true,true,true))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogAnnotationIssueCase test,ICatalogAnnotationIssueCaseProvider provider)
    {var bytes=test.Call.MemberBytes.ToArray();Assert.Equal(test.Expected,await provider.ImportAsync(test.Call));Assert.Equal(bytes,test.Call.MemberBytes);}
}
public sealed class ScriptedCatalogAnnotationIssue(CatalogAnnotationIssueCase test,int wrong=-1):ICatalogAnnotationIssueCaseProvider
{
    public Task<CatalogAnnotationIssueReply> ImportAsync(CatalogAnnotationIssueCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{EvidenceRows=value.EvidenceRows+1},1=>value with{UnmatchedRows=value.UnmatchedRows+1},
            2=>value with{ExternalPermissionRelations=value.ExternalPermissionRelations+1},3=>value with{SqlIssueRows=value.SqlIssueRows+1},
            4=>value with{SqlCategory="wrong"},5=>value with{SqlRaw="whole ZIP or wrong member"},6=>value with{SqlReasonNamesMember=!value.SqlReasonNamesMember},
            7=>value with{FlatStatus="wrong"},8=>value with{FlatXmlDocuments=value.FlatXmlDocuments+1},9=>value with{FlatItems=value.FlatItems+1},
            10=>value with{FlatAnnotations=value.FlatAnnotations+1},11=>value with{FlatMappedAnnotations=value.FlatMappedAnnotations+1},
            12=>value with{FlatUnmatchedAnnotations=value.FlatUnmatchedAnnotations+1},13=>value with{NestedMemberLocator=!value.NestedMemberLocator},
            14=>value with{NestedRawBytesExact=!value.NestedRawBytesExact},15=>value with{OriginalInputsRetained=!value.OriginalInputsRetained},16=>value with{PlatformIssueRows=value.PlatformIssueRows+1},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAnnotationIssueMockTests
{
    [Theory,MemberData(nameof(CatalogAnnotationIssueCases.All),MemberType=typeof(CatalogAnnotationIssueCases))]
    public Task OriginalSqlReportAndIndependentRawAssertionsPass(CatalogAnnotationIssueCase test)=>CatalogAnnotationIssueCases.AssertCase(test,new ScriptedCatalogAnnotationIssue(test));
    [Theory,MemberData(nameof(CatalogAnnotationIssueCases.All),MemberType=typeof(CatalogAnnotationIssueCases))]
    public async Task EverySqlReportMemberAndLosslessEvidenceMutationRejected(CatalogAnnotationIssueCase test)
    {for(var wrong=0;wrong<17;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogAnnotationIssueCases.AssertCase(test,new ScriptedCatalogAnnotationIssue(test,wrong))));}
}
