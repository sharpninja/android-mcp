using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

// XML declaration facts only. Published index membership is not a complete
// Java signature, permission grant, installed-module observation or wire proof.
public sealed class SdkApiIndexExtractor : ISourceExtractor
{
    public string Kind => "sdk-api-index";

    public ExtractionResult Extract(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!SdkXmlVersion.TryParse(source.Context.PlatformVersion, out var target))
            throw new ArgumentException("Explicit numeric platform API required", nameof(source));
        var index = SdkApiXml.Read(source);
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>(); var classOrdinal = 0;
        foreach (var owner in index.Root.Elements("class"))
        {
            classOrdinal++;
            var ownerBinary = owner.Attribute("name")?.Value ?? "";
            var ownerName = ownerBinary.Replace('/', '.').Replace('$', '.');
            var package = ownerBinary.Contains('/') ? ownerBinary[..ownerBinary.LastIndexOf('/')].Replace('/', '.') : "";
            var ownerAttributes = SdkApiXml.Attributes(owner);
            var ownerLifecycle = Active(ownerAttributes, new Dictionary<string, string>(StringComparer.Ordinal), target!);
            var nodes = new[] { owner }.Concat(owner.Elements().Where(x => x.Name == "method" || x.Name == "field"));
            var memberOrdinal = 0;
            foreach (var node in nodes)
            {
                var literal = SdkApiXml.Attributes(node);
                var lifecycle = node == owner ? ownerLifecycle : Active(literal, ownerLifecycle.Attributes, target!);
                var extensions = new JsonArray(); string? lifecycleReason = null;
                if (lifecycle.Attributes.TryGetValue("sdks", out var extensionText) && extensionText.Length > 0)
                    foreach (var token in extensionText.Split(','))
                    {
                        var match = Regex.Match(token, @"^(0|[1-9][0-9]*):([0-9]+(?:\.[0-9]+)?)$", RegexOptions.CultureInvariant);
                        if (!match.Success || match.Groups[1].Value != "0" &&
                            (!index.SdkIds.Contains(match.Groups[1].Value) || match.Groups[2].Value.Contains('.')))
                        { lifecycleReason = "Unrecognized or undeclared SDK extension lifecycle token"; continue; }
                        extensions.Add(new JsonObject { ["sdk_id"] = match.Groups[1].Value, ["minimum_version"] = match.Groups[2].Value });
                    }
                var current = ownerLifecycle.Current && lifecycle.Current;
                var lifecycleStatus = !ownerLifecycle.Known || !lifecycle.Known || lifecycleReason is not null ? "unknown"
                    : current ? "current-at-target" : "not-current-at-target";
                var extras = new JsonObject
                {
                    ["jvm_owner"] = ownerBinary, ["api_index_attributes"] = SdkApiXml.Json(literal), ["api_lifecycle"] = SdkApiXml.Json(literal),
                    ["container_api_lifecycle"] = SdkApiXml.Json(ownerAttributes), ["sdk_extensions"] = extensions,
                    ["lifecycle_status"] = lifecycleStatus,
                    ["public_sdk_membership"] = current && lifecycleReason is null ? "declared-current-in-official-SDK-API-index" : "declared-in-official-SDK-API-index-not-current-at-target",
                    ["target_platform_api"] = source.Context.PlatformVersion,
                    ["source_kind_semantics"] = "full published API lifecycle inventory; not complete Java signature",
                    ["index_ordinal"] = classOrdinal, ["member_ordinal"] = memberOrdinal
                };
                if (lifecycleReason is not null) extras["lifecycle_unknown_reason"] = lifecycleReason;
                var originalName = node.Attribute("name")?.Value ?? "";
                var canonical = node == owner ? ownerBinary : ownerBinary + "#" + originalName;
                var name = node == owner ? ownerName.Split('.')[^1] : originalName;
                var kind = node == owner ? "type" : "field";
                var parameters = new JsonArray(); string? returned = null;
                if (node == owner)
                {
                    extras["type_kind_status"] = "unknown";
                    extras["type_kind_reason"] = "API index omits interface/class/annotation distinction";
                    extras["supertypes"] = SdkApiXml.Supertypes(owner);
                }
                else if (node.Name == "method")
                {
                    try
                    {
                        var method = JvmDescriptor.Method(originalName);
                        name = method.Name; returned = method.Returned; kind = name == "<init>" ? "constructor" : "method";
                        foreach (var type in method.Parameters)
                            parameters.Add(new JsonObject { ["type"] = type, ["name"] = null, ["direction"] = null, ["annotations"] = new JsonArray() });
                    }
                    catch (FormatException error)
                    {
                        kind = "unsupported"; extras["parse_error"] = error.Message;
                        issues.Add(new("sdk-descriptor", error.Message, null, null));
                    }
                }
                else extras["field_type_status"] = "unknown";
                records.Add(SourceSyntax.Row(kind, canonical, ownerName, package, name, SdkApiXml.EmptyFragment(node),
                    SdkApiXml.Line(node), returned, parameters, extras: extras));
                memberOrdinal++;
            }
        }
        return new(records, issues, issues.Count == 0 ? StructuralParseStatus.Clean : StructuralParseStatus.Partial);
    }

    private sealed record Lifecycle(bool Current, Dictionary<string, string> Attributes, bool Known);

    private static Lifecycle Active(IReadOnlyDictionary<string, string> own, IReadOnlyDictionary<string, string> inherited, BigInteger[] target)
    {
        var attributes = inherited.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var attribute in own) attributes[attribute.Key] = attribute.Value;
        var since = attributes.GetValueOrDefault("since", "1");
        var removed = attributes.GetValueOrDefault("removed");
        var known = SdkXmlVersion.TryParse(since, out var introduced);
        BigInteger[]? removedVersion = null;
        if (!string.IsNullOrEmpty(removed))
            known &= SdkXmlVersion.TryParse(removed, out removedVersion) && introduced is not null && SdkXmlVersion.Compare(removedVersion!, introduced) > 0;
        var current = known && SdkXmlVersion.Compare(introduced!, target) <= 0 &&
            (removedVersion is null || SdkXmlVersion.Compare(removedVersion, target) > 0);
        return new(current, attributes, known);
    }
}

internal static class SdkXmlVersion
{
    // This preserves the legacy source-index tuple semantics. It is private to
    // this source domain, not a competing general platform/module comparer.
    internal static bool TryParse(string? value, out BigInteger[]? version)
    {
        version = null;
        if (value is null) return false;
        var parts = value.Split('.'); var numbers = new List<BigInteger>();
        foreach (var part in parts)
        {
            if (!BigInteger.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return false;
            numbers.Add(number);
        }
        while (numbers.Count < 2) numbers.Add(BigInteger.Zero);
        version = numbers.ToArray(); return true;
    }

    internal static int Compare(BigInteger[] left, BigInteger[] right)
    {
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        { var compared = left[i].CompareTo(right[i]); if (compared != 0) return compared; }
        return left.Length.CompareTo(right.Length);
    }
}

internal static class JvmDescriptor
{
    internal sealed record Signature(string Name, string[] Parameters, string Returned);
    private static readonly IReadOnlyDictionary<char, string> Primitives = new Dictionary<char, string>
    { ['B'] = "byte", ['C'] = "char", ['D'] = "double", ['F'] = "float", ['I'] = "int", ['J'] = "long", ['S'] = "short", ['Z'] = "boolean", ['V'] = "void" };

    internal static Signature Method(string value)
    {
        var opening = value.IndexOf('(');
        if (opening < 1) throw new FormatException("Missing method JVM descriptor");
        var parameters = new List<string>(); var position = opening + 1;
        while (position < value.Length && value[position] != ')') parameters.Add(Type(value, ref position, false));
        if (position >= value.Length) throw new FormatException("Unclosed method JVM descriptor");
        position++;
        var returned = Type(value, ref position, true);
        if (position != value.Length) throw new FormatException("Trailing method JVM descriptor");
        return new(value[..opening], parameters.ToArray(), returned);
    }

    private static string Type(string value, ref int position, bool allowVoid)
    {
        var start = position; var dimensions = 0;
        while (position < value.Length && value[position] == '[') { dimensions++; position++; }
        if (position >= value.Length) throw new FormatException("Incomplete JVM descriptor");
        var token = value[position]; string type;
        if (Primitives.TryGetValue(token, out var primitive))
        {
            if (token == 'V' && (dimensions > 0 || !allowVoid)) throw new FormatException("Illegal void JVM descriptor");
            type = primitive; position++;
        }
        else if (token == 'L')
        {
            var end = value.IndexOf(';', position);
            if (end < 0) throw new FormatException("Unterminated object JVM descriptor");
            type = value[(position + 1)..end].Replace('/', '.').Replace('$', '.'); position = end + 1;
        }
        else throw new FormatException("Unsupported JVM descriptor " + value[start..]);
        return type + string.Concat(Enumerable.Repeat("[]", dimensions));
    }
}

internal sealed record SdkXmlIndex(XElement Root, IReadOnlySet<string> SdkIds);

internal static class SdkApiXml
{
    internal static SdkXmlIndex Read(SourceDocument source)
    {
        try
        {
            using var stream = new MemoryStream(source.Bytes.ToArray(), false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root is null || root.Name != "api" || root.Attribute("version")?.Value != "4") throw new FormatException("Unsupported SDK API index format");
            return new(root, root.Elements("sdk").Select(x => x.Attribute("id")?.Value).Where(x => x is not null).Select(x => x!).ToHashSet(StringComparer.Ordinal));
        }
        catch (XmlException error) { throw new FormatException("SDK XML parse failed: " + error.Message, error); }
    }

    internal static Dictionary<string, string> Attributes(XElement node) => node.Attributes().Where(x => !x.IsNamespaceDeclaration)
        .ToDictionary(x => x.Name.ToString(), x => x.Value, StringComparer.Ordinal);
    internal static JsonObject Json(IReadOnlyDictionary<string, string> attributes) => new(attributes.Select(x => new KeyValuePair<string, JsonNode?>(x.Key, JsonValue.Create(x.Value))));
    internal static int Line(XElement node) => ((IXmlLineInfo)node).LineNumber;
    internal static JsonArray Supertypes(XElement node) => new(node.Elements().Where(x => x.Name == "extends" || x.Name == "implements")
        .Select(x => { var row = Json(Attributes(x)); row["relation"] = x.Name.ToString(); return (JsonNode)row; }).ToArray());

    internal static string EmptyFragment(XElement node) => "<" + node.Name + string.Concat(Attributes(node).Select(x => " " + x.Key + "=\"" + Escape(x.Value) + "\"")) + " />";
    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("\t", "&#09;", StringComparison.Ordinal)
        .Replace("\n", "&#10;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal);
}
