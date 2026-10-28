using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogMetadataBodyProvider:ICatalogMetadataBodyCaseProvider
{
    public Task<CatalogMetadataBodyReply> ExecuteAsync(CatalogMetadataBodyCall call)=>Task.FromResult(Execute(call));
    private static CatalogMetadataBodyReply Execute(CatalogMetadataBodyCall call)
    {
        var root=NativeSchemaProvider.Root();var canonical=new CanonicalJson();var schema=Path.Combine(root,"contracts/SourceContractMetadata.schema.json");
        var service=new CatalogSourceMetadata(schema,canonical);var before=call.Document.DeepClone();var doc=call.Document.DeepClone().AsObject();
        bool Unchanged()=>JsonNode.DeepEquals(before,call.Document);
        if(call.Operation is "missing-produce-concerns" or "missing-attach-concerns")
        {
            var enumerated=false;IEnumerable<CatalogMetadataOccurrence> Rows(){enumerated=true;yield break;}
            var error=Record.Exception(()=>{if(call.Operation=="missing-produce-concerns")service.Produce(doc);else service.AttachOccurrences(doc,Rows());});
            var message=error?.Message;return new(error is null,NoEnumeration:!enumerated,InputUnchanged:Unchanged(),ErrorContains:message?.Contains("must be injected",StringComparison.Ordinal)==true?"must be injected":message);
        }
        if(call.Operation is "history" or "empty-history" || call.Operation.StartsWith("untrusted-",StringComparison.Ordinal))
        {
            using var database=CatalogDatabase.CreateInMemory(root,CatalogSchemaProfile.Metadata);
            var trusted=new Dictionary<string,MetadataCatalogIdentity>(StringComparer.Ordinal)
            {[new string('a',64)]=new("class",new string('b',64)),[new string('f',64)]=new("method",new string('e',64))};
            var store=new MetadataCatalog(database,canonical,new NativeContentHash(),Path.Combine(root,"contracts/FunctionalGroup.schema.json"),trusted);
            if(call.Operation.StartsWith("untrusted-",StringComparison.Ordinal))
            {
                int status;try{store.Create("callback-descriptor",doc["callback_id"]!.GetValue<string>(),JsonSerializer.SerializeToElement(doc),"fixture trusted pins",true,"*");status=200;}catch(CatalogException error){status=error.Status;}
                return new(status==200,Status:status,InputUnchanged:Unchanged());
            }
            const string kind="functional-group";var key=doc["group_id"]!.GetValue<string>();
            if(call.Operation=="empty-history")
            {var empty=store.History(kind,key);return new(true,HistoryRows:empty.Count,HistoryExact:empty.Count==0,InputUnchanged:Unchanged());}
            var first=store.Create(kind,key,JsonSerializer.SerializeToElement(doc),"fixture create",true,"*");
            var updated=doc.DeepClone().AsObject();updated["title"]="Local";var second=store.Put(kind,key,JsonSerializer.SerializeToElement(updated),first.ETag,"fixture update",true);
            store.Delete(kind,key,second.ETag,"fixture delete",true);var history=store.History(kind,key);
            var original=MetadataCrudCases.BaseDocument;var local=MetadataCrudCases.Variant("Local");
            MetadataRevision[] expected=
            [
                new(kind,key,1,"local","active",null,MetadataCrudCases.Hash(original),original,"fixture create"),
                new(kind,key,2,"local","active",null,MetadataCrudCases.Hash(local),local,"fixture update"),
                new(kind,key,3,"local","deleted",null,MetadataCrudCases.Hash(local),local,"fixture delete")
            ];
            return new(true,HistoryRows:history.Count,HistoryExact:history.SequenceEqual(expected),InputUnchanged:Unchanged());
        }
        var failure=Record.Exception(()=>
        {
            if(call.Operation is "duplicate-method" or "proven-no-evidence" or "proven-empty-cleanup" or "bad-adapter")service.ValidateDescriptor(doc);
            else if(call.Operation=="dependency-cycle")
            {
                var groups=CatalogMetadataBodyCases.CycleGroups().ToDictionary(pair=>pair.Key,pair=>new CatalogMetadataGroupPin(1,new string('a',64),pair.Value),StringComparer.Ordinal);
                service.ValidateGroup(doc,groups);
            }
            else service.ValidateGroup(doc);
        });
        return new(failure is null,InputUnchanged:Unchanged(),ErrorContains:failure?.Message);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataBodyRealTests
{
    [Theory,MemberData(nameof(CatalogMetadataBodyCases.All),MemberType=typeof(CatalogMetadataBodyCases))]
    public Task SameNineteenOriginalModuleGuardsAndHistoryCallers(CatalogMetadataBodyCase test)=>CatalogMetadataBodyCases.AssertCase(test,new NativeCatalogMetadataBodyProvider());
}
