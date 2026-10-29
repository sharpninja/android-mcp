using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeMetadataProvider:IMetadataCaseProvider
{
    private readonly CatalogDatabase database;
    private readonly MetadataCatalog catalog;
    private MetadataCursor? cursor;
    public NativeMetadataProvider()
    {
        var root=NativeSchemaProvider.Root();
        var schemaPath=Path.Combine(root,"contracts","FunctionalGroup.schema.json");
        Assert.Equal("27e3d8af6bae5eaaf79d5b0252b4af686714ad0f3ca620d87b8fbcfc29c8f55b",ContentHash.Sha256(File.ReadAllBytes(schemaPath)));
        database=CatalogDatabase.CreateInMemory(root,CatalogSchemaProfile.Metadata);
        catalog=new(database,new CanonicalJson(),new NativeContentHash(),schemaPath);
    }
    private int Count(string table)
    {
        using var command=database.Command("SELECT count(*) FROM "+table);return checked((int)(long)command.ExecuteScalar()!);
    }
    private MetadataReply Reply(int status,MetadataSnapshot? row=null)=>new(status,catalog.Generation,Count("metadata_revisions"),Count("metadata_heads"),
        row?.Revision ?? 0,row?.State,row?.Origin,row?.BaseHash,row?.ContentHash,row?.ETag,row?.Document,row?.Reason);
    public MetadataReply Execute(MetadataCall call)
    {
        const string kind="functional-group";
        try
        {
            if(call.Operation=="capture-cursor") { cursor=catalog.Cursor(call.Reader);return Reply(200); }
            if(call.Operation=="continue-cursor")
            {
                catalog.ContinuePage(cursor ?? throw new InvalidOperationException("No fixture cursor captured"),call.Reader);return Reply(200);
            }
            if(call.Operation=="delete-base-sql")
            {
                try { using var command=database.Command("DELETE FROM metadata_bases");command.ExecuteNonQuery();return Reply(200); }
                catch(SqliteException error) when(error.SqliteErrorCode==19) { return Reply(409); }
            }
            var document=call.Document is null ? default : JsonSerializer.SerializeToElement(call.Document);
            var row=call.Operation switch
            {
                "create"=>catalog.Create(kind,call.Key,document,call.Reason,call.Owner,call.IfNoneMatch),
                "put"=>catalog.Put(kind,call.Key,document,call.ETag,call.Reason,call.Owner,call.Resolution,call.ExpectedBase),
                "delete"=>catalog.Delete(kind,call.Key,call.ETag,call.Reason,call.Owner),
                "get"=>catalog.Get(kind,call.Key),
                "import"=>catalog.Import(kind,call.Key,document,call.Reason,call.TrustedImport),
                _=>throw new ArgumentException("Unknown fixture metadata operation",nameof(call))
            };
            return Reply(200,row);
        }
        catch(CatalogException error) { return Reply(error.Status); }
    }
    public void Dispose()=>database.Dispose();
}
[Trait("Gate","Real")]
public sealed class MetadataCrudRealTests
{
    [Theory,MemberData(nameof(MetadataCrudCases.All),MemberType=typeof(MetadataCrudCases))]
    public void NativeStorageUsesSameCallsAndAssertions(MetadataCrudCase test)
    { using var provider=new NativeMetadataProvider();MetadataCrudCases.AssertCase(test,provider); }
}
