using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogAnnotationIssueProvider:ICatalogAnnotationIssueCaseProvider
{
    public Task<CatalogAnnotationIssueReply> ImportAsync(CatalogAnnotationIssueCall call)=>Task.FromResult(Import(call));
    private static CatalogAnnotationIssueReply Import(CatalogAnnotationIssueCall call)
    {
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        using var provider=new NativeCatalogBuildProvider
        {
            SourceDispatcher=runtime.Dispatcher,
            SourceValues=values=>values.Add(("sdk-annotations","annotations.zip","owned fixture archive placeholder")),
            InventorySetup=(inventory,root,originals)=>
            {
                byte[] bytes;using(var memory=new MemoryStream())
                {
                    using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
                    {
                        if(call.IncludeOriginalTargets)
                        {
                            using var member=archive.CreateEntry("valid.xml").Open();
                            using var xml=XmlWriter.Create(member,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,CloseOutput=false});
                            xml.WriteStartElement("root");
                            foreach(var target in CatalogAnnotationIssueCases.OriginalTargets)
                            {
                                xml.WriteStartElement("item");xml.WriteAttributeString("name",target);xml.WriteStartElement("annotation");xml.WriteAttributeString("name","android.annotation.RequiresPermission");
                                xml.WriteStartElement("val");xml.WriteAttributeString("name","value");xml.WriteAttributeString("val","\"android.permission.CAMERA\"");xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();
                            }
                            xml.WriteEndElement();
                        }
                        using var invalid=archive.CreateEntry(call.Member).Open();invalid.Write(call.MemberBytes);
                    }
                    bytes=memory.ToArray();
                }
                File.WriteAllBytes(Path.Combine(root,"annotations.zip"),bytes);originals["annotations.zip"]=bytes;
                var source=inventory["sources"]!.AsArray().OfType<JsonObject>().Single(value=>value["kind"]!.GetValue<string>()=="sdk-annotations");
                source["sha256"]=ContentHash.Sha256(bytes);source["git_blob"]=ContentHash.GitBlobSha1Legacy(bytes);source["size_bytes"]=bytes.Length;
            }
        };
        var result=provider.Build(new("annotation-member"));Assert.Equal("ok",result.Outcome);
        using var database=CatalogDatabase.OpenExisting(Path.Combine(provider.EvidenceRoot,"out/catalog.sqlite"));
        int Count(string sql){using var command=database.Command(sql);return checked((int)(long)command.ExecuteScalar()!);}
        string sid;JsonObject report;
        using(var command=database.Command("SELECT s.source_id,p.document_json FROM sources s JOIN parse_reports p USING(source_id) WHERE s.kind='sdk-annotations'"))
        using(var rows=command.ExecuteReader()){Assert.True(rows.Read());sid=rows.GetString(0);report=JsonNode.Parse(rows.GetString(1))!.AsObject();Assert.False(rows.Read());}
        var issues=new List<(string Category,string Reason,string Raw)>();
        using(var command=database.Command(CatalogAnnotationIssueCases.MemberSql,null,("$source",sid)))
        using(var rows=command.ExecuteReader())while(rows.Read())issues.Add((rows.GetString(0),rows.GetString(1),rows.GetString(2)));
        var issue=Assert.Single(issues);var archiveFacts=report["source_metadata"]!["annotation_archive"]!;
        var nested=Assert.Single(archiveFacts["issues"]!.AsArray());Assert.NotNull(nested);
        var nestedRaw=nested!["raw"]!.GetValue<string>();var encoding=nested["raw_encoding"]!.GetValue<string>();
        var rawBytes=encoding=="base64"?Convert.FromBase64String(nestedRaw):Encoding.UTF8.GetBytes(nestedRaw);
        if(call.IncludeOriginalTargets)Assert.Equal(1,Count(CatalogAnnotationIssueCases.OriginalIssueSql));
        using var platform=database.Command("SELECT count(*) FROM issues WHERE source_id=$source AND category='annotation-platform'",null,("$source",sid));
        return new(Count(CatalogAnnotationIssueCases.EvidenceSql),Count(CatalogAnnotationIssueCases.UnmatchedSql),Count(CatalogAnnotationIssueCases.PermissionsSql),issues.Count,
            issue.Category,issue.Raw,issue.Reason.Contains(call.Member,StringComparison.Ordinal),report["status"]!.GetValue<string>(),report["xml_documents"]!.GetValue<int>(),report["items"]!.GetValue<int>(),report["annotations"]!.GetValue<int>(),
            report["mapped_annotations"]!.GetValue<int>(),report["unmatched_annotations"]!.GetValue<int>(),nested["archive_member"]!.GetValue<string>()==call.Member,rawBytes.SequenceEqual(call.MemberBytes),result.OriginalInputsRetained,checked((int)(long)platform.ExecuteScalar()!));
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAnnotationIssueRealTests
{
    [Theory,MemberData(nameof(CatalogAnnotationIssueCases.All),MemberType=typeof(CatalogAnnotationIssueCases))]
    public Task SameThreeOriginalMemberSqlAndLosslessEvidenceAssertions(CatalogAnnotationIssueCase test)=>CatalogAnnotationIssueCases.AssertCase(test,new NativeCatalogAnnotationIssueProvider());
}
