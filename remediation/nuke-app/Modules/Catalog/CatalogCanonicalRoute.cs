namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogRouteRequest(string? ToolName,string? BindingId,string? SchemaHash,string Target,object Items);

/// <summary>Generated and generic aliases share one canonical plan and the existing policy algebra.</summary>
public static class CatalogCanonicalRoute
{
    // Known raw decoding/wire mappings are a separate explicitly pending original
    // caller. No descriptor/code identity is inferred by this bounded increment.
    public static IReadOnlyList<CanonicalBinding> Resolve(string route,CatalogRouteRequest request,
        IReadOnlyDictionary<string,(string BindingId,string SchemaHash)> aliases,IReadOnlyDictionary<string,string> schemas)
    {
        string binding,schema;
        if(route=="generated") (binding,schema)=aliases[request.ToolName!];
        else if(route=="generic")
        {
            binding=request.BindingId!;schema=schemas[binding];
            if(request.SchemaHash!=schema)throw new ArgumentException("Stale generic schema");
        }
        else throw new ArgumentException("Unknown route");
        if(!schemas.TryGetValue(binding,out var selected) || selected!=schema)throw new ArgumentException("Stale alias or wire mapping");
        return [new(binding,schema,request.Target,request.Items)];
    }
}
