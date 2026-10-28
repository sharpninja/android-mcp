using System.Text;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources.Java;

public sealed class JavaFileContext : IJavaFileContext
{
    public void Attach(JsonArray sources, JsonArray reports)
    {
        var byCache = new Dictionary<string,JsonObject>(StringComparer.Ordinal);
        foreach(var report in reports.OfType<JsonObject>()) byCache[report["cache_path"]!.GetValue<string>()] = report;
        foreach(var source in sources.OfType<JsonObject>())
        {
            if(source["kind"]?.GetValue<string>() != "java-source") continue;
            var cache=source["metadata_cache"]?.AsObject();
            if(cache is null || string.IsNullOrEmpty(cache["sha256"]?.GetValue<string>()) || string.IsNullOrEmpty(cache["parser_sha256"]?.GetValue<string>()) ||
                !byCache.TryGetValue(cache["path"]?.GetValue<string>() ?? "",out var report) ||
                !JsonNode.DeepEquals(report["cache_sha256"],cache["sha256"]) || !JsonNode.DeepEquals(report["parser_sha256"],cache["parser_sha256"]))
                throw new InvalidDataException("Java context cache mismatch " + source["path"]?.GetValue<string>());
            var context=new JsonObject();
            foreach(var field in new[]{"package","imports","wildcard_imports","parser","parse_errors","excluded_implementation_nodes"}) context[field]=report[field]?.DeepClone();
            context["static_import_resolution"]="Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.";
            source["java_file_context"]=context;
        }
    }
}

public sealed class JvmSourceLinker : IJvmSourceLinker
{
    private static readonly IReadOnlyDictionary<string,string> Primitives=new Dictionary<string,string>(StringComparer.Ordinal)
        { ["byte"]="B",["char"]="C",["double"]="D",["float"]="F",["int"]="I",["long"]="J",["short"]="S",["boolean"]="Z",["void"]="V" };
    public string? Encode(string? value,string owner,IReadOnlyDictionary<string,string> owners)
    {
        if(string.IsNullOrEmpty(value))return null;
        value=value.Replace("...","[]",StringComparison.Ordinal).Trim();var dimensions=0;
        while(value.EndsWith("[]",StringComparison.Ordinal)){dimensions++;value=value[..^2].Trim();}
        var prefix=new string('[',dimensions);
        if(Primitives.TryGetValue(value,out var primitive))return value=="void" && dimensions>0 ? null : prefix+primitive;
        var depth=0;var outer=new StringBuilder();
        foreach(var item in value)
        {
            if(item=='<')depth++;
            else if(item=='>'){if(--depth<0)return null;}
            else if(depth==0)outer.Append(item);
        }
        if(depth!=0)return null;
        return owners.TryGetValue(outer.ToString().Trim(),out var binary) ? prefix+"L"+binary+";" : null;
    }
    public string? Key(JsonObject declaration,IReadOnlyDictionary<string,string> owners)
    {
        var binary=declaration["extras"]?["owner_binary_name"]?.GetValue<string>();var owner=declaration["owner"]?.GetValue<string>();
        if(string.IsNullOrEmpty(binary) || owner is null || !owners.TryGetValue(owner,out var known) || known!=binary)return null;
        var kind=declaration["kind"]?.GetValue<string>();
        if(kind is "class" or "interface" or "enum" or "annotation")return binary;
        if(kind is not ("method" or "constructor"))return null;
        var arguments=new List<string>();
        foreach(var parameter in declaration["parameters"]!.AsArray())
        {
            var argument=Encode(parameter!["type"]?.GetValue<string>(),owner,owners);
            if(argument is null or "V")return null;arguments.Add(argument);
        }
        var result=kind=="constructor"?"V":Encode(declaration["return_type"]?.GetValue<string>(),owner,owners);
        return result is null ? null : binary+"#"+(kind=="constructor"?"<init>":declaration["name"]!.GetValue<string>())+"("+string.Concat(arguments)+")"+result;
    }
}
