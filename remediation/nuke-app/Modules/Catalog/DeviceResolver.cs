using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record DeviceResolutionPins(string RegistryId,long CatalogGeneration,long OverlayGeneration,long CapabilityEpoch,long ViewGeneration);

/// <summary>Pure offline fixture resolver. Existence never establishes OS or client authority.</summary>
public sealed class DeviceResolver
{
    public const long CounterLimit=9007199254740991;
    internal static readonly IReadOnlyList<string> ScopeFields=Array.AsReadOnly(new[]{"profile_id","target_key","android_user_id","backend_instance_id","device_instance_id"});
    private readonly ICanonicalJson canonical;
    private readonly JsonObject schemaSource;
    private readonly Dictionary<string,JsonSchema> validators=new(StringComparer.Ordinal);
    private readonly object validatorSync=new();
    public DeviceResolver(ICanonicalJson canonical,string schemaPath)
    {
        this.canonical=canonical;
        schemaSource=JsonNode.Parse(File.ReadAllText(schemaPath))!.AsObject();
    }
    private sealed record Decision(string State,string[] Reasons);
    private sealed record Dependency(string EntryId,string ContractHash);
    private sealed record Variant(string ContractHash,Dependency[] Dependencies,Decision Result);
    private sealed class Row(string id,string kind,string catalogState,long revision,string? documentHash,Decision baseline,Variant? selected)
    {
        public string Id=id;
        public string Kind=kind;
        public string CatalogState=catalogState;
        public long Revision=revision;
        public string? DocumentHash=documentHash;
        public Decision Baseline=baseline;
        public Decision Current=baseline;
        public string? ContractHash=selected?.ContractHash;
        public Dependency[] Dependencies=selected?.Dependencies ?? [];
        public string? GraphDigest;
        public string? SchemaHash;
    }
    private static string Text(JsonElement value,string key)=>value.GetProperty(key).GetString()!;
    private static string? OptionalText(JsonElement value,string key)=>value.TryGetProperty(key,out var result) && result.ValueKind!=JsonValueKind.Null ? result.GetString() : null;
    private static long? OptionalInteger(JsonElement value,string key)=>value.TryGetProperty(key,out var result) && result.ValueKind!=JsonValueKind.Null ? result.GetInt64() : null;
    private static Decision State(string state,string reason)=>new(state,[reason]);
    private static Decision Merge(IEnumerable<Decision> values,bool alternative=false)
    {
        var rows=values.ToArray();
        if(rows.Length==0) return State("unknown","APPLICABILITY_MISSING");
        var state=alternative
            ? rows.Any(x=>x.State=="available") ? "available" : rows.Any(x=>x.State=="unknown") ? "unknown" : "unavailable"
            : rows.Any(x=>x.State=="unavailable") ? "unavailable" : rows.Any(x=>x.State=="unknown") ? "unknown" : "available";
        var reasons=rows.Where(x=>x.State==state).SelectMany(x=>x.Reasons).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(state,reasons.Length==0 ? ["APPLICABLE_EVIDENCE"] : reasons);
    }
    private void Validate(JsonElement value,string root)
    {
        JsonSchema schema;
        lock(validatorSync)
        {
            if(!validators.TryGetValue(root,out schema!))
            {
                // Schema projection only; these serialized bytes never define an identity.
                var projected=(JsonObject)schemaSource.DeepClone();projected["$ref"]="#/$defs/"+root;
                schema=JsonSchema.FromText(projected.ToJsonString());validators.Add(root,schema);
            }
        }
        if(!schema.Evaluate(value,new EvaluationOptions{RequireFormatValidation=true,OutputFormat=OutputFormat.Flag}).IsValid)
            throw new InvalidDataException("Invalid "+root+" contract");
    }
    private static Decision Platform(JsonElement observed,JsonElement rule)
    {
        var lower=rule.GetProperty("introduced");var upper=rule.GetProperty("removed");
        var lowerMajor=lower[0].GetInt64();var lowerMinor=lower[1].GetInt64();
        var hasUpper=upper.ValueKind!=JsonValueKind.Null;
        if(hasUpper && (upper[0].GetInt64()<lowerMajor || upper[0].GetInt64()==lowerMajor && upper[1].GetInt64()<=lowerMinor))
            throw new ArgumentException("Invalid platform applicability interval");
        var major=OptionalInteger(observed,"major");var minor=OptionalInteger(observed,"minor");
        if(major is null) return State("unknown","PLATFORM_VERSION_UNKNOWN");
        bool? AtLeast(long requiredMajor,long requiredMinor)
        {
            if(major!=requiredMajor) return major>requiredMajor;
            if(minor is null) return requiredMinor==0 ? true : null;
            return minor>=requiredMinor;
        }
        var lo=AtLeast(lowerMajor,lowerMinor);var hi=hasUpper ? AtLeast(upper[0].GetInt64(),upper[1].GetInt64()) : false;
        if(lo==false || hi==true) return State("unavailable","PLATFORM_VERSION_EXCLUDED");
        return lo is null || hi is null ? State("unknown","PLATFORM_MINOR_UNKNOWN") : State("available","PLATFORM_VERSION_MATCH");
    }
    private static Decision Module(JsonElement? observed,JsonElement rule)
    {
        var lower=long.Parse(Text(rule,"introduced"),NumberStyles.None,CultureInfo.InvariantCulture);
        var rawUpper=OptionalText(rule,"removed");long? upper=rawUpper is null ? null : long.Parse(rawUpper,NumberStyles.None,CultureInfo.InvariantCulture);
        if(upper is not null && upper<=lower) throw new ArgumentException("Invalid module version interval");
        if(observed is null || Text(observed.Value,"state")=="unknown") return State("unknown","MODULE_VERSION_UNKNOWN");
        var fact=observed.Value;var authoritative=fact.GetProperty("authoritative").GetBoolean();
        if(Text(fact,"state")=="absent") return authoritative ? State("unavailable","MODULE_ABSENT") : State("unknown","MODULE_ABSENCE_UNPROVEN");
        if(!authoritative) return State("unknown","MODULE_PRESENCE_UNPROVEN");
        var rawVersion=OptionalText(fact,"version");if(rawVersion is null) return State("unknown","MODULE_VERSION_UNKNOWN");
        var version=long.Parse(rawVersion,NumberStyles.None,CultureInfo.InvariantCulture);
        return version>=lower && (upper is null || version<upper) ? State("available","MODULE_VERSION_MATCH") : State("unavailable","MODULE_VERSION_EXCLUDED");
    }
    private static Decision Clause(JsonElement rule,JsonElement facts)
    {
        var results=new List<Decision>();
        if(rule.TryGetProperty("unknown_reason",out _)) results.Add(State("unknown","APPLICABILITY_INCOMPLETE"));
        if(rule.TryGetProperty("platform_point",out var point))
        {
            var platform=facts.GetProperty("platform");
            results.Add(OptionalInteger(platform,"major")==point[0].GetInt64() && OptionalInteger(platform,"minor")==point[1].GetInt64()
                ? State("available","PLATFORM_POINT_MATCH") : State("unknown","PLATFORM_APPLICABILITY_UNPROVEN"));
        }
        if(rule.TryGetProperty("platform",out var platformRule)) results.Add(Platform(facts.GetProperty("platform"),platformRule));
        if(rule.TryGetProperty("extensions",out var extensions))
        foreach(var required in extensions.EnumerateObject())
        {
            var value=OptionalInteger(facts.GetProperty("extensions"),required.Name);
            results.Add(value is null ? State("unknown","SDK_EXTENSION_UNKNOWN") : value>=required.Value.GetInt64()
                ? State("available","SDK_EXTENSION_MATCH") : State("unavailable","SDK_EXTENSION_TOO_OLD"));
        }
        if(rule.TryGetProperty("modules",out var modules))
        foreach(var required in modules.EnumerateObject())
            results.Add(Module(facts.GetProperty("modules").TryGetProperty(required.Name,out var module) ? module : null,required.Value));
        foreach(var (name,field) in new[]{("oem","oem"),("build_fingerprints","build_fingerprint")})
        {
            if(!rule.TryGetProperty(name,out var allowed)) continue;
            var observed=OptionalText(facts,field);
            results.Add(observed is null ? State("unknown","BUILD_APPLICABILITY_UNKNOWN") : allowed.EnumerateArray().Any(x=>x.GetString()==observed)
                ? State("available","BUILD_APPLICABILITY_MATCH") : State("unavailable","BUILD_APPLICABILITY_EXCLUDED"));
        }
        if(rule.TryGetProperty("interface",out var contract))
        {
            var found=facts.GetProperty("interfaces").TryGetProperty(Text(contract,"descriptor"),out var observed);
            if(!found || Text(observed,"state")!="reachable")
                results.Add(found && Text(observed,"state")=="absent" && observed.GetProperty("authoritative").GetBoolean()
                    ? State("unavailable","INTERFACE_AUTHORITATIVELY_ABSENT") : State("unknown","INTERFACE_OBSERVATION_UNAVAILABLE"));
            else
            {
                if(!observed.GetProperty("authoritative").GetBoolean()) return Merge(results.Append(State("unknown","INTERFACE_REACHABILITY_UNPROVEN")));
                var minimum=OptionalInteger(contract,"minimum_version");
                if(minimum is not null)
                {
                    var version=OptionalInteger(observed,"version");
                    results.Add(version is null ? State("unknown","INTERFACE_VERSION_UNKNOWN") : version>=minimum
                        ? State("available","INTERFACE_VERSION_MATCH") : State("unavailable","INTERFACE_VERSION_TOO_OLD"));
                }
                var hashes=contract.GetProperty("accepted_hashes");
                if(hashes.GetArrayLength()>0) results.Add(hashes.EnumerateArray().Any(x=>x.GetString()==OptionalText(observed,"hash"))
                    ? State("available","INTERFACE_HASH_MATCH") : State("unknown","INTERFACE_HASH_UNRECOGNIZED"));
                if(minimum is null && hashes.GetArrayLength()==0) results.Add(State("unknown","INTERFACE_CONTRACT_UNPROVEN"));
            }
        }
        return Merge(results);
    }
    private static Decision Applicability(JsonElement rule,JsonElement facts)=>rule.TryGetProperty("unknown_reason",out _)
        ? State("unknown","APPLICABILITY_UNKNOWN") : Merge(rule.GetProperty("alternatives").EnumerateArray().Select(x=>Clause(x,facts)),true);
    private static JsonElement Record(Action<Utf8JsonWriter> write)
    {
        using var bytes=new MemoryStream();using(var writer=new Utf8JsonWriter(bytes)) write(writer);
        using var document=JsonDocument.Parse(bytes.ToArray());return document.RootElement.Clone();
    }
    private string VariantKey(Variant variant)=>variant.ContractHash+":"+Encoding.UTF8.GetString(canonical.Serialize(Record(writer=>
    {
        writer.WriteStartArray();foreach(var dependency in variant.Dependencies.OrderBy(x=>x.EntryId,StringComparer.Ordinal))
        { writer.WriteStartObject();writer.WriteString("entry_id",dependency.EntryId);writer.WriteString("schema_hash",dependency.ContractHash);writer.WriteEndObject(); }
        writer.WriteEndArray();
    })));
    public JsonElement Resolve(IReadOnlyList<JsonElement> inventory,JsonElement facts,DeviceResolutionPins pins)
    {
        _=canonical.Serialize(facts);Validate(facts,"Facts");
        if(!DateTimeOffset.TryParse(Text(facts,"observed_at"),CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) throw new InvalidDataException("Invalid observation timestamp");
        if(facts.GetProperty("capability_epoch").GetInt64()!=pins.CapabilityEpoch) throw new InvalidDataException("Captured facts capability epoch mismatch");
        foreach(var counter in new[]{pins.CatalogGeneration,pins.OverlayGeneration,pins.CapabilityEpoch,pins.ViewGeneration})
            if(counter<0 || counter>CounterLimit) throw new InvalidDataException("Invalid snapshot counter");
        var rows=new SortedDictionary<string,Row>(StringComparer.Ordinal);
        foreach(var entity in inventory)
        {
            _=canonical.Serialize(entity);Validate(entity,"InventoryEntity");var key=Text(entity,"entry_id");var kind=Text(entity,"entity_kind");
            if(rows.ContainsKey(key)) throw new InvalidDataException("Duplicate inventory identity");
            var variants=new List<Variant>();
            foreach(var item in entity.GetProperty("variants").EnumerateArray())
            {
                Decision result;
                try { result=Applicability(item.GetProperty("availability"),facts); }
                catch(Exception error) when(error is ArgumentException or FormatException or OverflowException or InvalidOperationException)
                { result=State("unknown","APPLICABILITY_MALFORMED"); }
                variants.Add(new(Text(item,"schema_hash"),item.GetProperty("dependencies").EnumerateArray().Select(x=>new Dependency(Text(x,"entry_id"),Text(x,"schema_hash"))).ToArray(),result));
            }
            var yes=variants.Where(x=>x.Result.State=="available").ToArray();var maybe=variants.Where(x=>x.Result.State=="unknown").ToArray();Variant? selected=null;Decision baseline;
            if(!facts.GetProperty("identity_verified").GetBoolean()) baseline=State("unknown","DEVICE_IDENTITY_UNVERIFIED");
            else if(kind=="other") baseline=State("unknown","NON_EXECUTABLE_INVENTORY_NODE");
            else if(yes.Length>0)
            {
                if(yes.Concat(maybe).Select(VariantKey).Distinct(StringComparer.Ordinal).Count()!=1) baseline=State("unknown","VARIANT_AMBIGUOUS");
                else { selected=yes[0];baseline=State("available","APPLICABLE_EVIDENCE"); }
            }
            else if(maybe.Length>0) baseline=new("unknown",maybe.SelectMany(x=>x.Result.Reasons).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
            else if(variants.Count>0) baseline=new("unavailable",variants.SelectMany(x=>x.Result.Reasons).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
            else baseline=State("unknown","APPLICABILITY_MISSING");
            rows.Add(key,new(key,kind,OptionalText(entity,"catalog_state") ?? "active",OptionalInteger(entity,"catalog_revision") ?? 0,OptionalText(entity,"catalog_document_sha256"),baseline,selected));
        }
        var converged=false;
        for(var pass=0;pass<=rows.Count;pass++)
        {
            var changed=false;
            foreach(var row in rows.Values)
            {
                if(row.Baseline.State!="available") continue;
                var checks=new List<Decision>();
                foreach(var dependency in row.Dependencies)
                {
                    if(!rows.TryGetValue(dependency.EntryId,out var target)) checks.Add(State("unknown","TYPE_DEPENDENCY_MISSING"));
                    else if(target.Kind!="type") checks.Add(State("unknown","TYPE_DEPENDENCY_NOT_TYPE"));
                    else if(target.Current.State=="unavailable") checks.Add(State("unavailable","TYPE_DEPENDENCY_UNAVAILABLE"));
                    else if(target.Current.State=="unknown") checks.Add(State("unknown","TYPE_DEPENDENCY_UNKNOWN"));
                    else if(target.ContractHash!=dependency.ContractHash) checks.Add(State("unknown","TYPE_DEPENDENCY_SCHEMA_MISMATCH"));
                }
                var next=checks.Count==0 ? row.Baseline : Merge(checks);
                if(next.State!=row.Current.State || !next.Reasons.SequenceEqual(row.Current.Reasons,StringComparer.Ordinal)) { row.Current=next;changed=true; }
            }
            if(!changed) { converged=true;break; }
        }
        if(!converged) throw new InvalidDataException("Type graph did not converge");
        foreach(var row in rows.Values)
        {
            if(row.Current.State!="available") continue;
            var closure=new SortedDictionary<string,string>(StringComparer.Ordinal);var pending=new Stack<string>();pending.Push(row.Id);
            while(pending.Count>0)
            {
                var id=pending.Pop();if(closure.ContainsKey(id)) continue;var target=rows[id];closure.Add(id,target.ContractHash!);
                foreach(var dependency in target.Dependencies) pending.Push(dependency.EntryId);
            }
            row.GraphDigest=canonical.Hash(Record(writer=>
            { writer.WriteStartArray();foreach(var pair in closure) { writer.WriteStartArray();writer.WriteStringValue(pair.Key);writer.WriteStringValue(pair.Value);writer.WriteEndArray(); }writer.WriteEndArray(); }));
            row.SchemaHash=canonical.Hash(Record(writer=>
            { writer.WriteStartObject();writer.WriteString("schema_format","closure3");writer.WriteString("contract_hash",row.ContractHash);writer.WriteString("graph_digest",row.GraphDigest);writer.WriteEndObject(); }));
        }
        using var inventoryBytes=new MemoryStream();
        foreach(var entity in inventory.OrderBy(x=>Text(x,"entry_id"),StringComparer.Ordinal))
        { inventoryBytes.Write(canonical.Serialize(entity));inventoryBytes.WriteByte((byte)'\n'); }
        var inventoryHash=ContentHash.Sha256(inventoryBytes.GetBuffer().AsSpan(0,checked((int)inventoryBytes.Length)));
        var body=Record(writer=>
        {
            writer.WriteStartObject();writer.WriteString("schema_version","3.0.0");writer.WriteString("registry_id",pins.RegistryId);
            writer.WriteNumber("catalog_generation",pins.CatalogGeneration);writer.WriteNumber("overlay_generation",pins.OverlayGeneration);
            writer.WriteNumber("capability_epoch",pins.CapabilityEpoch);writer.WriteNumber("view_generation",pins.ViewGeneration);
            writer.WriteString("facts_id",canonical.Hash(facts));writer.WriteNumber("inventory_count",inventory.Count);writer.WriteString("inventory_sha256",inventoryHash);
            foreach(var field in ScopeFields) { writer.WritePropertyName(field);facts.GetProperty(field).WriteTo(writer); }
            writer.WriteStartArray("entries");foreach(var row in rows.Values) WriteRow(writer,row);writer.WriteEndArray();writer.WriteEndObject();
        });
        var viewId=canonical.Hash(body);
        var snapshot=Record(writer=>
        { writer.WriteStartObject();foreach(var property in body.EnumerateObject()) property.WriteTo(writer);writer.WriteString("view_id",viewId);writer.WriteEndObject(); });
        Validate(snapshot,"Snapshot");return snapshot;
    }
    private static void WriteRow(Utf8JsonWriter writer,Row row)
    {
        writer.WriteStartObject();writer.WriteString("entry_id",row.Id);writer.WriteString("entity_kind",row.Kind);
        writer.WriteString("catalog_state",row.CatalogState);writer.WriteNumber("catalog_revision",row.Revision);writer.WriteString("catalog_document_sha256",row.DocumentHash);
        writer.WriteString("state",row.Current.State);writer.WriteStartArray("reason_codes");foreach(var reason in row.Current.Reasons) writer.WriteStringValue(reason);writer.WriteEndArray();
        writer.WriteString("schema_hash",row.SchemaHash);writer.WriteString("contract_hash",row.ContractHash);writer.WriteString("graph_digest",row.GraphDigest);
        writer.WriteStartArray("dependencies");foreach(var dependency in row.Dependencies)
        { writer.WriteStartObject();writer.WriteString("entry_id",dependency.EntryId);writer.WriteString("contract_hash",dependency.ContractHash);writer.WriteEndObject(); }
        writer.WriteEndArray();writer.WriteEndObject();
    }
}
