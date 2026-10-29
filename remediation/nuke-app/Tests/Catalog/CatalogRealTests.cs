using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogProvider:ICatalogCaseProvider
{
    private readonly AndroidMcp.BuildTool.Core.ICanonicalJson canonical=CanonicalJsonRealTests.Native();
    private readonly CatalogReference service;
    private CatalogCursor? captured;
    public NativeCatalogProvider()=>service=new(canonical,bytes=>ContentHash.Sha256(bytes.Span));
    private static JsonElement Document(System.Text.Json.Nodes.JsonObject value)
    {
        // The sealed CRUD fixture documents contain integer fields. Write their
        // typed input bytes explicitly; canonical hash identity remains owned
        // by the real ICanonicalJson service, never JsonNode serialization.
        using var bytes=new MemoryStream();
        using(var writer=new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            foreach(var property in value) writer.WriteNumber(property.Key,property.Value!.GetValue<int>());
            writer.WriteEndObject();
        }
        using var document=JsonDocument.Parse(bytes.ToArray());return document.RootElement.Clone();
    }
    public CatalogReply Execute(CatalogCall call)
    {
        try
        {
            CatalogEntrySnapshot? row=null;
            switch(call.Operation)
            {
                case "create":row=service.Create(call.Signature!,Document(call.Document!));break;
                case "put":row=service.Put(call.Key!,call.ETag,Document(call.Document!),call.Resolution,call.ExpectedBase);break;
                case "delete":row=service.Delete(call.Key!,call.ETag);break;
                case "get":row=service.Get(call.Key!);break;
                case "publish":service.PublishFixture(call.Imports!.ToDictionary(x=>x.Key,x=>new ImportedEntry(x.Value.Signature,Document(x.Value.Document)),StringComparer.Ordinal));break;
                case "capture-cursor":captured=service.Cursor();break;
                case "continue-cursor":service.ContinuePage(captured!);break;
                default:throw new InvalidOperationException("Unknown fixture action");
            }
            return row is null ? new(200) : new(200,row.Key,row.ETag,row.State,row.Revision,row.History.Count,
                service.RegistryGeneration,service.OverlayGeneration,Encoding.UTF8.GetString(canonical.Serialize(row.Document)));
        }
        catch(CatalogException error) { return new(error.Status); }
    }
}

[Trait("Gate","Real")]
public sealed class CatalogRealTests
{
    [Theory,MemberData(nameof(CatalogCases.All),MemberType=typeof(CatalogCases))]
    public void NativeEntriesUseSameCallsAndAssertions(CatalogCase test)=>CatalogCases.AssertCase(test,new NativeCatalogProvider());
}
