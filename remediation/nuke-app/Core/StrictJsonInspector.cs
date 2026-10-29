using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One strict JSON detector, retaining raw values and both consumer traversal orders.</summary>
public sealed class StrictJsonInspector : IStrictJsonInspector
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public StrictJsonInspection Inspect(ReadOnlyMemory<byte> utf8)
    {
        try { _ = Utf8.GetCharCount(utf8.Span); }
        catch (DecoderFallbackException) { return Failure(CanonicalJsonErrors.InvalidUnicode, "Input is not valid UTF8"); }
        try
        {
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = int.MaxValue });
            return Inspect(document.RootElement);
        }
        catch (JsonException) { return Failure(CanonicalJsonErrors.InvalidJson, "Input is not one valid JSON value"); }
    }

    public StrictJsonInspection Inspect(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            return Failure(CanonicalJsonErrors.UndefinedValue, "Undefined JsonElement has no JSON representation");
        var owned = value.Clone();
        var postorder = new List<DetectedIssue>();
        var issues = Detect(owned, "", postorder);
        long rank = 0;
        Rank(issues, ref rank);
        // Public instances are created once. The canonical projection reorders these same instances.
        var diagnostics = postorder.Select(x => new StrictJsonDiagnostic(x.Code, x.Path, x.Name, x.Message, x.Fatal, x.Rank)).ToArray();
        return new StrictJsonInspection(owned, diagnostics);
    }

    private static StrictJsonInspection Failure(string code, string message) =>
        new(null, [new StrictJsonDiagnostic(code, "", null, message, true)]);

    private sealed class DetectedIssue(string code, string path, string? name, string message, bool fatal)
    {
        public string Code { get; } = code;
        public string Path { get; } = path;
        public string? Name { get; } = name;
        public string Message { get; } = message;
        public bool Fatal { get; } = fatal;
        public long Rank { get; set; }
    }

    // Only nodes with diagnostics or diagnostic descendants are retained.
    private sealed record IssueNode(IReadOnlyList<DetectedIssue> Own, IReadOnlyList<(string Key, int Index, IssueNode Node)> Children, bool Object);

    private static IssueNode? Detect(JsonElement value, string path, List<DetectedIssue> postorder)
    {
        var own = new List<DetectedIssue>();
        var children = new List<(string Key, int Index, IssueNode Node)>();
        var isObject = value.ValueKind == JsonValueKind.Object;
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                var properties = new List<(string Key, JsonElement Value, int Index)>();
                var propertyIndex = 0;
                foreach (var property in value.EnumerateObject())
                {
                    string? name = null;
                    try { name = property.Name; }
                    catch (InvalidOperationException) { own.Add(UnicodeIssue(path)); }
                    if (name is not null)
                    {
                        if (!IsUnicodeScalarString(name)) own.Add(UnicodeIssue(path));
                        else if (!names.Add(name))
                            own.Add(new DetectedIssue(CanonicalJsonErrors.DuplicateProperty, ChildPath(path, name), name, "Duplicate decoded object property", false));
                    }
                    properties.Add((name ?? "", property.Value, propertyIndex++));
                }
                foreach (var property in properties)
                {
                    var child = Detect(property.Value, ChildPath(path, property.Key), postorder);
                    if (child is not null) children.Add((property.Key, property.Index, child));
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var child = Detect(item, ChildPath(path, index.ToString(CultureInfo.InvariantCulture)), postorder);
                    if (child is not null) children.Add(("", index, child));
                    index++;
                }
                break;
            case JsonValueKind.String:
                try { if (!IsUnicodeScalarString(value.GetString()!)) own.Add(UnicodeIssue(path)); }
                catch (InvalidOperationException) { own.Add(UnicodeIssue(path)); }
                break;
            case JsonValueKind.Number:
                var raw = value.GetRawText();
                if (!IsIntegerToken(raw) && (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)))
                    own.Add(new DetectedIssue(CanonicalJsonErrors.NonfiniteNumber, path, null, "Floating JSON number is outside finite IEEE754 binary64", true));
                break;
        }
        // The legacy source diagnostics observe completed nested objects before their parent.
        postorder.AddRange(own);
        return own.Count == 0 && children.Count == 0 ? null : new IssueNode(own, children, isObject);
    }

    private static DetectedIssue UnicodeIssue(string path) =>
        new(CanonicalJsonErrors.InvalidUnicode, path, null, "Unpaired Unicode surrogate", true);

    private static void Rank(IssueNode? node, ref long rank)
    {
        if (node is null) return;
        foreach (var issue in node.Own) issue.Rank = rank++;
        if (node.Object)
        {
            var sorted = node.Children.ToArray();
            Array.Sort(sorted, (left, right) =>
            {
                var order = CompareScalars(left.Key, right.Key);
                return order == 0 ? left.Index.CompareTo(right.Index) : order;
            });
            foreach (var child in sorted) Rank(child.Node, ref rank);
        }
        else foreach (var child in node.Children) Rank(child.Node, ref rank);
    }

    private static string ChildPath(string parent, string name) =>
        parent + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    internal static bool IsIntegerToken(string raw) => raw.IndexOfAny(['.', 'e', 'E']) < 0;

    internal static bool IsUnicodeScalarString(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[++index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }
        return true;
    }

    internal static int CompareScalars(string left, string right)
    {
        var a = left.EnumerateRunes().GetEnumerator();
        var b = right.EnumerateRunes().GetEnumerator();
        while (true)
        {
            var moreA = a.MoveNext();
            var moreB = b.MoveNext();
            if (!moreA || !moreB) return moreA ? 1 : moreB ? -1 : 0;
            var order = a.Current.Value.CompareTo(b.Current.Value);
            if (order != 0) return order;
        }
    }
}
