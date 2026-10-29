using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Pure provided Catalog metadata over exact admitted logical rows, never source evidence.</summary>
public sealed class CatalogSourceNormalizer(ICanonicalJson canonical)
{
    public IReadOnlyList<CatalogSourceNormalization>? Decode(JsonElement? sidecar)
    {
        if(sidecar is null || sidecar.Value.ValueKind==JsonValueKind.Null)return null;
        try{return JsonSerializer.Deserialize<CatalogSourceNormalization[]>(canonical.Serialize(sidecar.Value)) ?? throw new JsonException("Sidecar is null");}
        catch(Exception error)when(error is JsonException or CanonicalJsonException or NotSupportedException)
        {throw new CatalogBuildException("catalog-source-normalization-schema","Malformed or unknown normalization fields",error);}
    }
    public IReadOnlyDictionary<int,JsonObject> Normalize(RehydrationSourceProjection projection,
        IReadOnlyList<CatalogSourceNormalization>? sidecar,IReadOnlySet<int> selectedLogicalRows)
    {
        var rows=projection.Rows.ToDictionary(row=>row.RowIndex);var provided=new Dictionary<int,CatalogSourceNormalization>();
        foreach(var value in sidecar ?? [])
        {
            if(value is null || !rows.TryGetValue(value.RowIndex,out var row) || !provided.TryAdd(value.RowIndex,value))
                throw new CatalogBuildException("catalog-source-normalization-row","Normalization identifies a foreign or repeated logical row");
            if(value.ExpectedOriginalRowCanonicalSha256!=canonical.Hash(row.OriginalRow))
                throw new CatalogBuildException("catalog-source-normalization-identity","Normalization differs from the exact admitted OriginalRow");
            if(value.ApiLevel is <=0 || string.IsNullOrWhiteSpace(value.Channel))
                throw new CatalogBuildException("catalog-source-normalization-metadata","Explicit nullable API and nonempty channel required");
        }
        foreach(var index in selectedLogicalRows)
        {
            if(!rows.ContainsKey(index))throw new CatalogBuildException("catalog-source-normalization-row","Selected normalization row is foreign");
            if(!provided.ContainsKey(index))throw new CatalogBuildException("catalog-source-normalization-missing","Selected logical row lacks explicit Catalog metadata");
        }
        var result=new Dictionary<int,JsonObject>();
        foreach(var index in selectedLogicalRows.Order())
        {
            var metadata=provided[index];var original=rows[index].OriginalRow;
            var normalized=JsonNode.Parse(original.GetRawText())!.AsObject();
            normalized["api_level"]=metadata.ApiLevel is { } api ? JsonValue.Create(api) : null;
            normalized["channel"]=metadata.Channel;
            if(metadata.License is not null)normalized["license"]=metadata.License;
            if(metadata.Scope is not null)normalized["scope"]=metadata.Scope;
            if(metadata.RetrievedAt is not null)normalized["retrieved_at"]=metadata.RetrievedAt;
            normalized["x-native-catalog-normalization"]=new JsonObject{["disposition"]="provided-catalog-normalization",
                ["source_evidence"]=false,["original_row_canonical_sha256"]=metadata.ExpectedOriginalRowCanonicalSha256,
                ["provided"]=JsonSerializer.SerializeToNode(metadata)};
            result.Add(index,normalized);
        }
        return result.ToFrozenDictionary();
    }
}
