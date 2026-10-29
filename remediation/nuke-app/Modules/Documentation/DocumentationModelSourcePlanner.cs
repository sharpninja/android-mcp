using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>One pin-to-route authority. No filesystem, hash algorithm, network or process execution.</summary>
public sealed class DocumentationModelSourcePlanner(IStrictJsonInspector strict,DocumentationProjectSourceScope scope=DocumentationProjectSourceScope.Model):IDocumentationModelSourcePlanner
{
    private sealed record ContractRoute(DocumentationContractId Id,string File,string CanonicalPin,string CatalogPath,string MediaType);
    private static readonly ContractRoute[] Routes=
    [
        new(DocumentationContractId.OpenApi,"openapi.json","../catalog/contracts/openapi.json","contracts/openapi.json","application/json"),
        new(DocumentationContractId.ApiContract,"API-CONTRACT.md","../catalog/API-CONTRACT.md","API-CONTRACT.md","text/markdown"),
        new(DocumentationContractId.SourceMetadataContract,"SOURCE-METADATA-CONTRACT.md","../catalog/SOURCE-METADATA-CONTRACT.md","SOURCE-METADATA-CONTRACT.md","text/markdown")
    ];
    public DocumentationModelSourcePlan Plan(Artifact sourcePins)
    {
        try
        {
            if(sourcePins.RelativePath!="contracts/source-pins.json"||sourcePins.MediaType!="application/json")throw new FormatException("Unexpected source-pin artifact identity");
            var inspection=strict.Inspect(sourcePins.Bytes);
            if(inspection.CanonicalDiagnostics.Count>0||inspection.Value is null)throw new FormatException("Strict source-pin JSON inspection failed");
            var entries=inspection.Value.Value.GetProperty("files").EnumerateArray().ToArray();
            if(entries.Length!=Routes.Length)throw new FormatException("Missing or extra contract pin entry");
            var pins=new Dictionary<DocumentationContractId,string>();
            foreach(var entry in entries)
            {
                var name=entry.GetProperty("file").GetString();
                var route=Routes.SingleOrDefault(route=>route.File==name)??throw new FormatException("Unknown contract artifact");
                var canonical=entry.GetProperty("canonical_relative_path").GetString();
                var hash=entry.GetProperty("sha256").GetString();
                if(canonical!=route.CanonicalPin||hash is null||!Regex.IsMatch(hash,"^[a-f0-9]{64}$")||!pins.TryAdd(route.Id,hash))
                    throw new FormatException("Substituted path, invalid hash or duplicate contract pin");
            }
            var sources=new DocumentationProjectSourceCatalog().DescribeProjectSources(scope).Select(input=>input.Source).ToList();
            foreach(var route in Routes)sources.Add(new("snapshot:"+route.Id,DocumentationInputRole.ContractSnapshotRequired,route.Id,"contracts/"+route.File,route.MediaType,pins[route.Id]));
            foreach(var route in Routes)sources.Add(new("canonical:"+route.Id,DocumentationInputRole.CanonicalContractOptional,route.Id,route.CatalogPath,route.MediaType,pins[route.Id]));
            return new(sources.AsReadOnly());
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new DocumentationModelSourceException("Invalid admitted model sources: "+error.Message);}
    }
}
