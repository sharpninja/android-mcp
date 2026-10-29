using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// One SDK-only legacy truth/scalar conversion authority. The planner supplies
// detached typed facts; this service never reads or reparses an original row.
public sealed class SdkLegacyPlatformValue(ICanonicalJson canonical)
{
    public string Resolve(RehydrationLegacyPlatformFacts facts)=>Resolve(Fact(facts.Row),Fact(facts.Transport));
    private static JsonElement? Fact(RehydrationLegacyPlatformValue? fact)
    {
        if(fact is null||(!fact.Present&&(fact.Kind!=JsonValueKind.Undefined||fact.Value is not null))||
            fact.Present&&(fact.Value is null||fact.Kind==JsonValueKind.Undefined||fact.Value.Value.ValueKind!=fact.Kind))
            throw new TransportException("SDK_PLATFORM_FACTS","Detached legacy SDK platform facts are inconsistent.");
        return fact.Present?fact.Value:null;
    }
    public string Resolve(JsonElement? row,JsonElement? transport)
    {
        var selected=Truthy(row)?row:transport;
        if(!Truthy(selected))throw new TransportException("SDK_PLATFORM","A truthy legacy SDK platform is required.");
        var value=selected!.Value;
        return value.ValueKind switch
        {
            JsonValueKind.String=>value.GetString()!,
            JsonValueKind.True=>"True",
            JsonValueKind.Number=>Encoding.UTF8.GetString(canonical.Serialize(value)),
            _=>throw new TransportException("SDK_PLATFORM_KIND","Nonempty legacy SDK platform containers require an explicit unsupported-domain disposition.")
        };
    }
    private static bool Truthy(JsonElement? value)=>value?.ValueKind switch
    {
        null or JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False=>false,
        JsonValueKind.String=>value.Value.GetString()!.Length!=0,
        JsonValueKind.Number=>value.Value.GetDouble()!=0,
        JsonValueKind.Array=>value.Value.GetArrayLength()!=0,
        JsonValueKind.Object=>value.Value.EnumerateObject().Any(),
        _=>true
    };
}
