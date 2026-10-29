using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogAnnotationImportProvider:ICatalogAnnotationImportCaseProvider
{
    public CatalogAnnotationImportReply Import(CatalogAnnotationImportCall call)
    {
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        var indexed=call.Variant is "current-exact" or "generic-erasure" or "unknown-formal" or "method-class-name";
        var xmlJvm=call.Variant is "generic-erasure" or "unknown-formal" or "method-class-name";
        var archiveName=xmlJvm ? "annotations.zip" : "ann.zip";
        var annotationName=xmlJvm ? "androidx.annotation.RequiresPermission" : "android.annotation.RequiresPermission";
        using var provider=new NativeCatalogBuildProvider
        {
            SourceDispatcher=runtime.Dispatcher,
            SourceValues=values=>
            {
                if(call.Variant=="shadowed-formal") values[0]=(values[0].Kind,values[0].Path,values[0].Text.Replace("public class A {","public class A<String> {",StringComparison.Ordinal));
                if(call.Variant=="source-signature-only") values[0]=("source-signature",values[0].Path,values[0].Text);
                if(indexed)
                {
                    var methods=call.Variant=="method-class-name" ? "<method name=\"&lt;init>()V\"/><method name=\"A()I\"/>" : call.Variant=="current-exact" ? "<method name=\"go(Ljava/lang/String;)V\"/>" : "<method name=\"call(La/Callback;)La/Result;\"/>";
                    values.Add(("sdk-api-index","index.xml","<api version=\"4\"><class name=\"a/A\">"+methods+"</class>"+(call.Variant=="current-exact" ? "" : "<class name=\"a/Callback\"/><class name=\"a/Result\"/><class name=\"java/lang/String\"/>")+"</api>"));
                }
                // InventorySetup replaces this explicitly declared binary source
                // with a real ZIP. No filename route or text decoding is used.
                values.Add(("sdk-annotations",archiveName,"replaced by raw fixture archive"));
            },
            InventorySetup=(inventory,root,originals)=>
            {
                byte[] archive;
                using(var buffer=new MemoryStream())
                {
                    using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true))
                    {
                        var member=zip.CreateEntry("a.xml");using var stream=member.Open();
                        using var xml=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,CloseOutput=false});
                        xml.WriteStartElement("root");xml.WriteStartElement("item");xml.WriteAttributeString("name",call.Item);
                        xml.WriteStartElement("annotation");xml.WriteAttributeString("name",annotationName);
                        xml.WriteStartElement("val");xml.WriteAttributeString("name","value");xml.WriteAttributeString("val","\"android.permission.CAMERA\"");
                        xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();
                    }
                    archive=buffer.ToArray();
                }
                File.WriteAllBytes(Path.Combine(root,archiveName),archive);originals[archiveName]=archive;
                foreach(var source in inventory["sources"]!.AsArray().OfType<JsonObject>())
                {
                    if(source["kind"]!.GetValue<string>()=="sdk-api-index") source["platform_api_version"]="37.2";
                    if(source["kind"]!.GetValue<string>()!="sdk-annotations") continue;
                    source["platform_api_version"]=call.AnnotationPlatform;source["sha256"]=ContentHash.Sha256(archive);
                    source["git_blob"]=ContentHash.GitBlobSha1Legacy(archive);source["size_bytes"]=archive.Length;
                    if(call.Variant=="foreign-revision") source["revision"]=new string('2',40);
                }
            }
        };
        var result=provider.Build(new("annotation-import"));Assert.Equal("ok",result.Outcome);
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(provider.EvidenceRoot,"out/catalog.sqlite"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();
        using var query=connection.CreateCommand();query.CommandText="SELECT b.signature,e.scope,e.document_json FROM annotation_evidence e LEFT JOIN bindings b ON b.binding_id=e.binding_id";
        using var rows=query.ExecuteReader();Assert.True(rows.Read());var signature=rows.IsDBNull(0) ? null : rows.GetString(0);var scope=rows.GetString(1);var evidence=JsonNode.Parse(rows.GetString(2))!.AsObject();Assert.False(rows.Read());rows.Close();
        int Count(string sql){using var command=connection.CreateCommand();command.CommandText=sql;return (int)(long)command.ExecuteScalar()!;}
        var annotation=evidence["annotation"]!.AsObject();var raw=annotation["raw"]!.GetValue<string>();
        var values=annotation["values"]!.AsArray();
        return new(signature,evidence["target_match_kind"]!.GetValue<string>(),scope,Count("SELECT count(*) FROM annotation_evidence"),Count("SELECT count(*) FROM permission_relations WHERE annotation='external'"),
            evidence["item_name"]!.GetValue<string>()==call.Item && evidence["archive_member"]!.GetValue<string>()=="a.xml" && raw.Contains(annotationName,StringComparison.Ordinal) && raw.Contains("android.permission.CAMERA",StringComparison.Ordinal),
            values.Count==1 && values[0]!["name"]!.GetValue<string>()=="value" && values[0]!["val"]!.GetValue<string>()=="\"android.permission.CAMERA\"",result.OriginalInputsRetained);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogAnnotationImportRealTests
{
    [Theory,MemberData(nameof(CatalogAnnotationImportCases.All),MemberType=typeof(CatalogAnnotationImportCases))]
    public void NativeArchiveAttachmentUsesSameTenScopedAssertions(CatalogAnnotationImportCase test)=>CatalogAnnotationImportCases.AssertCase(test,new NativeCatalogAnnotationImportProvider());
}
