using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure byte-preserving review projections; no file, process or network access.</summary>
public sealed class LosslessEvidenceService(IContentHash hashes):ILosslessEvidenceService
{
    private const string DataPrefix="data:image/svg+xml;base64,";
    private const string ReferencePrefix="review-asset://sha256/";
    private const string EncodingContract="RFC4648standardbase64withpadding/no-whitespace";
    private static readonly Regex DataUris=new("data:image/svg\\+xml;base64,([A-Za-z0-9+/=]+)",RegexOptions.CultureInvariant);
    private static readonly Regex References=new("review-asset://sha256/[0-9a-f]{64}",RegexOptions.CultureInvariant);

    public FactoredEvidence Factor(FrozenEvidenceInput input)
    {
        var files=ValidateFrozen(input);var original=Original(input,files);
        // Latin1 gives exactly one character per byte, so offsets and untouched
        // slices remain correct even when the original HTML contains UTF8 text.
        var raw=Encoding.Latin1.GetString(original.Bytes.Span);
        if(References.IsMatch(raw))throw Error("RESERVED_REFERENCE_COLLISION","Original HTML contains a reserved review-reference token.");
        var assets=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in input.Manifest)
            if(row.RelativePath.EndsWith(".svg",StringComparison.Ordinal))assets.TryAdd(row.Sha256,row.RelativePath);
        var matches=DataUris.Matches(raw);var pieces=new List<string>();var replacements=new JsonArray();var retained=new JsonArray();var last=0;
        foreach(Match match in matches)
        {
            byte[] decoded;
            try{decoded=Convert.FromBase64String(match.Groups[1].Value);}
            catch(FormatException){Retain("invalid base64 retained literally");continue;}
            var digest=hashes.Sha256(decoded);
            if(!assets.TryGetValue(digest,out var path)){Retain("not an assigned canonical SVG; retained literally");continue;}
            if(Convert.ToBase64String(decoded)!=match.Groups[1].Value){Retain("noncanonical base64 retained literally");continue;}
            var reference=ReferencePrefix+digest;
            pieces.Add(raw[last..match.Index]);pieces.Add(reference);last=match.Index+match.Length;
            replacements.Add(new JsonObject
            {
                ["original_start"]=match.Index,["original_end"]=match.Index+match.Length,
                ["original_uri_sha256"]=hashes.Sha256(Encoding.Latin1.GetBytes(match.Value)),["replacement_uri"]=reference,
                ["asset"]=path,["asset_sha256"]=digest,["encoding"]=EncodingContract
            });
            void Retain(string reason)=>retained.Add(new JsonObject{{"start",match.Index},{"end",match.Index+match.Length},{"reason",reason}});
        }
        pieces.Add(raw[last..]);var viewBytes=Encoding.Latin1.GetBytes(string.Concat(pieces));
        var map=new JsonObject
        {
            ["original_artifact"]=input.HtmlPath,["original_sha256"]=hashes.Sha256(original.Bytes.Span),["original_bytes"]=original.Bytes.Length,
            ["view_sha256"]=hashes.Sha256(viewBytes),["view_bytes"]=viewBytes.Length,["replacements"]=replacements,
            ["unmatched_retained_literals"]=retained,["all_other_html_bytes_unchanged"]=true,["frozen_inputs_modified"]=false,
            ["design_scope_reduced"]=false,["model_or_network_calls"]=0
        };
        var view=new Artifact("storyboards.factored.html",viewBytes,"text/html");var mapArtifact=JsonArtifact("reconstruction-map.json",map);
        var restored=Reconstruct(input,view,mapArtifact);
        if(!restored.Bytes.Span.SequenceEqual(original.Bytes.Span))throw Error("ROUND_TRIP_CHANGED","Factored evidence did not reproduce the original bytes.");
        var distinct=replacements.Select(n=>n!["asset_sha256"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count();
        var verification=new JsonObject
        {
            ["round_trip_byte_identical"]=true,["original_sha256"]=hashes.Sha256(restored.Bytes.Span),["embedded_svg_instances"]=matches.Count,
            ["replaced_known_instances"]=replacements.Count,["distinct_assets"]=distinct,["original_bytes"]=original.Bytes.Length,
            ["review_view_bytes"]=viewBytes.Length,["bytes_represented_by_exact_asset_references"]=original.Bytes.Length-viewBytes.Length,
            ["new_requirements_omitted"]=0
        };
        return new(view,mapArtifact,JsonArtifact("verification.json",verification));
    }

    public Artifact Reconstruct(FrozenEvidenceInput input,Artifact reviewHtml,Artifact reconstructionMap)
    {
        var files=ValidateFrozen(input);var original=Original(input,files);JsonNode map;
        try{map=JsonNode.Parse(reconstructionMap.Bytes.Span)??throw new JsonException("Null reconstruction map");}
        catch(JsonException e){throw Error("INVALID_MAPPING",e.Message);}
        try
        {
            if(map["original_artifact"]!.GetValue<string>()!=input.HtmlPath)throw Error("ORIGINAL_PATH_CHANGED","Reconstruction map names another original artifact.");
            if(hashes.Sha256(reviewHtml.Bytes.Span)!=map["view_sha256"]!.GetValue<string>())throw Error("VIEW_HASH_CHANGED","Factored view changed.");
            var mappings=new Dictionary<string,(string Asset,string AssetHash,string UriHash,string Encoding)>(StringComparer.Ordinal);
            foreach(var item in map["replacements"]!.AsArray())
            {
                var reference=item!["replacement_uri"]!.GetValue<string>();
                var mapping=(item["asset"]!.GetValue<string>(),item["asset_sha256"]!.GetValue<string>(),item["original_uri_sha256"]!.GetValue<string>(),item["encoding"]!.GetValue<string>());
                if(mappings.TryGetValue(reference,out var prior)&&prior!=mapping)throw Error("AMBIGUOUS_REFERENCE","One review reference identifies conflicting assets.");
                mappings[reference]=mapping;
            }
            var uris=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var pair in mappings)
            {
                if(!files.TryGetValue(pair.Value.Asset,out var asset))throw Error("ASSET_MISSING","Referenced frozen SVG is absent: "+pair.Value.Asset);
                if(hashes.Sha256(asset.Bytes.Span)!=pair.Value.AssetHash)throw Error("ASSET_HASH_CHANGED","Referenced SVG differs from reconstruction map: "+pair.Value.Asset);
                if(pair.Value.Encoding!=EncodingContract)throw Error("INVALID_MAPPING","Unsupported reconstruction encoding.");
                var uri=DataPrefix+Convert.ToBase64String(asset.Bytes.Span);
                if(hashes.Sha256(Encoding.ASCII.GetBytes(uri))!=pair.Value.UriHash)throw Error("URI_HASH_CHANGED","Data URI reconstruction changed.");
                uris[pair.Key]=uri;
            }
            var view=Encoding.Latin1.GetString(reviewHtml.Bytes.Span);
            var restored=References.Replace(view,m=>uris.TryGetValue(m.Value,out var uri)?uri:throw Error("UNKNOWN_REFERENCE","Factored view contains an unmapped review reference."));
            var bytes=Encoding.Latin1.GetBytes(restored);
            if(hashes.Sha256(bytes)!=map["original_sha256"]!.GetValue<string>())throw Error("RESTORED_HASH_CHANGED","Reconstructed bytes differ from the frozen original hash.");
            return new(input.HtmlPath,bytes,original.MediaType);
        }
        catch(EvidenceIntegrityException){throw;}
        catch(Exception e)when(e is InvalidOperationException or NullReferenceException or FormatException or ArgumentException)
        {throw Error("INVALID_MAPPING","Invalid reconstruction map: "+e.Message);}
    }

    private Dictionary<string,Artifact> ValidateFrozen(FrozenEvidenceInput input)
    {
        var files=new Dictionary<string,Artifact>(StringComparer.Ordinal);
        foreach(var artifact in input.Artifacts)
            if(!files.TryAdd(artifact.RelativePath,artifact))throw Error("FROZEN_MANIFEST_INVALID","Duplicate frozen artifact path.");
        var declared=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in input.Manifest)
        {
            if(!declared.Add(row.RelativePath)||!files.TryGetValue(row.RelativePath,out var artifact))throw Error("FROZEN_MANIFEST_INVALID","Missing or duplicate frozen manifest artifact: "+row.RelativePath);
            if(hashes.Sha256(artifact.Bytes.Span)!=row.Sha256)throw Error("FROZEN_HASH_CHANGED","Frozen artifact hash changed: "+row.RelativePath);
            if(artifact.Bytes.Length!=row.Length)throw Error("FROZEN_SIZE_CHANGED","Frozen artifact length changed: "+row.RelativePath);
        }
        if(files.Keys.Any(p=>!declared.Contains(p)))throw Error("FROZEN_MANIFEST_INVALID","Undeclared frozen input artifact.");
        return files;
    }
    private static Artifact Original(FrozenEvidenceInput input,IReadOnlyDictionary<string,Artifact> files)=>files.TryGetValue(input.HtmlPath,out var original)?original:throw Error("ORIGINAL_MISSING","Original frozen HTML is absent.");
    private static EvidenceIntegrityException Error(string code,string message)=>new(code,message);
    private static Artifact JsonArtifact(string path,JsonNode node)=>new(path,Encoding.UTF8.GetBytes(node.ToJsonString()+"\n"),"application/json");
}
