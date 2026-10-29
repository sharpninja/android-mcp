using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Admit inventory bytes and recheck acquisition through the shared pinned and sealed-reader authorities.</summary>
public sealed class SourcesInventoryReader(IPinnedArtifactReader inventories,ISourceBatchReader acquisition,ICanonicalJson canonical):ISourcesInventoryReader
{
    public async Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(request.InventoryManifest is null||request.InventoryManifest.MediaType!="application/json")throw new SourcesInventoryException("INVENTORY_READ_SCHEMA",[]);
        var bytes=await inventories.ReadAsync(request.InventoryManifest,cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SourcesInventoryReceipt? receipt;
        try{receipt=JsonSerializer.Deserialize<SourcesInventoryReceipt>(canonical.Serialize(bytes));}
        catch(Exception problem)when(problem is JsonException or CanonicalJsonException or NotSupportedException)
        {throw new SourcesInventoryException("INVENTORY_READ_SCHEMA",[]);}
        if(receipt?.Acquisition?.Receipt?.Projection is null||receipt.Acquisition.Manifest is null||receipt.Entries is null)
            throw new SourcesInventoryException("INVENTORY_READ_SCHEMA",[]);
        if(!Bytes(receipt.Acquisition.Receipt.Projection).AsSpan().SequenceEqual(Bytes(request.Projection)))
            throw new SourcesInventoryException("INVENTORY_READ_PROJECTION",[]);
        var pin=receipt.Acquisition.Manifest;
        var verified=await acquisition.ResumeOnlyAsync(new(new(receipt.Acquisition.Receipt.GenerationId,pin.RelativePath,
            request.Projection,receipt.Acquisition.Receipt.CachePolicy),pin),cancellationToken);
        if(!verified.Reused||verified.Manifest!=pin)throw new SourcesInventoryException("INVENTORY_READ_METADATA",[]);
        var expected=SourcesInventoryOperation.Describe(verified);
        if(!Bytes(receipt).AsSpan().SequenceEqual(Bytes(expected)))throw new SourcesInventoryException("INVENTORY_READ_METADATA",[]);
        return new(expected,request.InventoryManifest);
    }
    private byte[] Bytes<T>(T value)=>canonical.Serialize(JsonSerializer.SerializeToElement(value));
}
