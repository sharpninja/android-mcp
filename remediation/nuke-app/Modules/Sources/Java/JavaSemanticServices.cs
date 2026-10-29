using System.Text;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources.Java;

public sealed class JavaFileContext : IJavaFileContext
{
    private static JsonNode? Required(JsonObject value,string name)=>value.TryGetPropertyValue(name,out var field)?field:throw new KeyNotFoundException(name);
    public void Attach(JsonArray sources, JsonArray reports)
    {
        var byCache = new Dictionary<string,JsonObject>(StringComparer.Ordinal);
        JsonObject? nullPathReport=null;
        foreach(var item in reports)
        {
            var report=item!.AsObject();var path=Required(report,"cache_path")?.GetValue<string>();
            if(path is null)nullPathReport=report;else byCache[path]=report;
        }
        foreach(var item in sources)
        {
            var source=item!.AsObject();if(Required(source,"kind")?.GetValue<string>() != "java-source") continue;
            var cache=Required(source,"metadata_cache")!.AsObject();var path=Required(cache,"path")?.GetValue<string>();
            var report=path is null?nullPathReport:byCache.GetValueOrDefault(path);
            if(report is null || !JsonNode.DeepEquals(Required(report,"cache_sha256"),Required(cache,"sha256")) ||
                !JsonNode.DeepEquals(Required(report,"parser_sha256"),Required(cache,"parser_sha256")))
                throw new InvalidDataException("Java context cache mismatch " + Required(source,"path")?.GetValue<string>());
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
        value=SourceText.Strip(value.Replace("...","[]",StringComparison.Ordinal));var dimensions=0;
        while(value.EndsWith("[]",StringComparison.Ordinal)){dimensions++;value=SourceText.Strip(value[..^2]);}
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
        return owners.TryGetValue(SourceText.Strip(outer.ToString()),out var binary) && !string.IsNullOrEmpty(binary) ? prefix+"L"+binary+";" : null;
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
