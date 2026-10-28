using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Pure source spellings only; scoped catalog resolution remains in Catalog.</summary>
public sealed class SourceTypeFacts(IJvmSourceLinker jvm, ICanonicalJson canonical) : IAnnotationTargetSourceFacts, IStructuralTypeHeaderSourceFacts
{
    private const SourceSyntaxProfile Legacy = SourceSyntaxProfile.LegacySignature;

    public string? XmlJvmKey(string itemName, IReadOnlyDictionary<string, string> owners)
    {
        var target = SourceSyntax.Rx(@"\s+\d+$").Replace(itemName, ""); var separator = target.IndexOf(' ');
        if (separator < 0) return null;
        var owner = target[..separator]; var tail = target[(separator + 1)..];
        if (!owners.TryGetValue(owner, out var binary)) return null;
        var start = tail.IndexOf('('); var end = tail.LastIndexOf(')');
        if (start < 0 || end < start || tail[(end + 1)..].Trim().Length != 0) return null;
        var nameMatch = SourceSyntax.Rx(@"([\w$]+)$").Match(tail[..start]);
        if (!nameMatch.Success) return null;
        var name = nameMatch.Groups[1].Value; var returned = tail[..nameMatch.Index].Trim();
        var constructor = returned.Length == 0 && name == owner[(owner.LastIndexOf('.') + 1)..];
        if (returned.Length == 0 && !constructor) return null;
        var arguments = new List<string>();
        foreach (var (raw, _) in SourceSyntax.Chunks(tail[(start + 1)..end], ',', true, Legacy))
        {
            var encoded = jvm.Encode(SourceSyntax.TypeName(raw, Legacy), owner, owners);
            if (encoded is null or "V") return null;
            arguments.Add(encoded);
        }
        var result = constructor ? "V" : jvm.Encode(SourceSyntax.TypeName(returned, Legacy), owner, owners);
        return result is null ? null : binary + "#" + (constructor ? "<init>" : name) + "(" + string.Concat(arguments) + ")" + result;
    }

    public IReadOnlySet<string> FormalNames(string raw)
    {
        var clean = SourceSyntax.ParseAnnotations(raw, Legacy).Masked; var starts = new List<int>();
        var type = SourceSyntax.Rx(@"\b(?:class|interface|enum)\s+[\w.$]+\s*(<)").Match(clean);
        if (type.Success) starts.Add(type.Groups[1].Index);
        var parenthesis = clean.IndexOf('('); var prefix = parenthesis < 0 ? clean : clean[..parenthesis];
        starts.AddRange(SourceSyntax.Rx(@"(?<![\w.$])<").Matches(prefix).Select(x => x.Index));
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in starts)
        {
            var end = ClosingAngle(clean, start); if (end < 0) continue;
            foreach (var (part, _) in SourceSyntax.Chunks(clean[(start + 1)..end], ',', true, Legacy))
            {
                var name = SourceSyntax.Rx(@"\A\s*([A-Za-z_$][\w$]*)").Match(part);
                if (name.Success) names.Add(name.Groups[1].Value);
            }
        }
        return names;
    }

    private static int ClosingAngle(string text, int start)
    {
        var depth = 0;
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] == '<') depth++;
            else if (text[index] == '>' && --depth == 0) return index;
        }
        return -1;
    }

    public StructuralTypeHeader StructuralHeader(JsonObject document)
    {
        var extensions = document["extensions"]!.AsObject(); var parameters = extensions["type_parameters"]?.DeepClone(); var supers = new List<JsonObject>();
        var header = extensions["type_header"]?.GetValue<string>() ?? "";
        if (header.Length > 0)
        {
            var match = SourceSyntax.Rx(@"(?:\b(?:class|interface|enum)|@interface)\s+[\w.$]+(.*)").Match(header);
            var tail = match.Success ? match.Groups[1].Value.Trim().TrimEnd('{').Trim() : "";
            if (tail.StartsWith('<'))
            {
                var end = ClosingAngle(tail, 0);
                if (end >= 0) { parameters = JsonValue.Create(tail[..(end + 1)]); tail = tail[(end + 1)..]; }
            }
            foreach (System.Text.RegularExpressions.Match clause in SourceSyntax.Rx(@"\b(extends|implements)\s+(.+?)(?=\b(?:extends|implements)\b|$)").Matches(tail))
                foreach (var (value, _) in SourceSyntax.Chunks(clause.Groups[2].Value, ',', true, Legacy)) supers.Add(Super(clause.Groups[1].Value, value.Trim()));
        }
        foreach (var (field, relation) in new[] { ("superclass", "extends"), ("interfaces", "implements") })
        {
            var text = extensions[field]?.GetValue<string>();
            if (string.IsNullOrEmpty(text)) continue;
            text = SourceSyntax.Rx(@"\A\s*(?:extends|implements)\s+").Replace(text, "");
            foreach (var (value, _) in SourceSyntax.Chunks(text, ',', true, Legacy)) supers.Add(Super(relation, value.Trim()));
        }
        if (extensions["supertypes"] is JsonArray supertypes)
            foreach (var item in supertypes)
            {
                var value = item!.AsObject(); var name = value["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(name)) supers.Add(new JsonObject { ["relation"] = value.ContainsKey("relation") ? value["relation"]?.DeepClone() : JsonValue.Create("unknown"), ["text"] = name.Replace('/', '.').Replace('$', '.') });
            }
        if (extensions["declared_extends"]?.GetValue<string>() is { Length: > 0 } declared) supers.Add(Super("hidl-declared-extends", declared));
        if (extensions["language"]?.GetValue<string>() == "hidl" && extensions["target_type_raw"]?.GetValue<string>() is { Length: > 0 } target) supers.Add(Super("hidl-typedef-target", target));
        var unique = new SortedDictionary<string, JsonObject>(Comparer<string>.Create(StrictJsonInspector.CompareScalars));
        foreach (var value in supers) unique[Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(value)))] = value;
        return new(parameters, unique.Values.ToArray());
    }

    private static JsonObject Super(string relation, string text) => new() { ["relation"] = relation, ["text"] = text };
}
