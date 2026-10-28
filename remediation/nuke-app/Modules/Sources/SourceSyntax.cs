using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Sources;

internal readonly record struct SourceSpan(int Start, int End);
internal sealed record SourceAnnotation(int Start, int End, JsonObject Document);
internal sealed record AnnotatedSource(string Masked, IReadOnlyList<SourceAnnotation> Annotations);

// Shared lexical primitives. Imported input is data; no expression evaluator,
// compiler, runtime type loader, network operation or subprocess exists here.
internal static class SourceSyntax
{
    internal static Regex Rx(string expression) => new(expression, RegexOptions.CultureInvariant | RegexOptions.Singleline);
    internal static JsonArray Array(IEnumerable<JsonNode?>? nodes = null) => new((nodes ?? []).Select(x => x?.DeepClone()).ToArray());
    internal static JsonArray Strings(IEnumerable<string> values) => new(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
    internal static JsonArray Annotations(IEnumerable<SourceAnnotation> values) => Array(values.Select(x => x.Document));

    internal static string Mask(string text, bool strings)
    {
        var result = text.ToCharArray();
        for (var i = 0; i < text.Length;)
        {
            var start = i;
            var isComment = false;
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                isComment = true; i += 2;
                while (i < text.Length && text[i] != '\n') i++;
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                isComment = true; i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = i + 1 < text.Length ? i + 2 : text.Length;
            }
            else if (text[i] is '"' or '\'')
            {
                var quote = text[i++];
                while (i < text.Length)
                {
                    if (text[i] == '\\') { i = Math.Min(i + 2, text.Length); continue; }
                    if (text[i++] == quote) break;
                }
            }
            else { i++; continue; }
            if (isComment || strings)
                for (var j = start; j < i; j++) result[j] = text[j] is '\n' or '\r' ? text[j] : ' ';
        }
        return new string(result);
    }

    internal static AnnotatedSource ParseAnnotations(string text)
    {
        var clean = text.ToCharArray(); var annotations = new List<SourceAnnotation>();
        var code = Mask(text, true);
        for (var i = 0; i < text.Length; i++)
        {
            if (code[i] != '@') continue;
            var match = Rx(@"\G@([\w.$]+)").Match(text, i);
            if (!match.Success || match.Groups[1].Value == "interface") continue;
            var end = i + match.Length; var probe = end;
            while (probe < text.Length && char.IsWhiteSpace(text[probe])) probe++;
            var arguments = "";
            if (probe < text.Length && text[probe] == '(')
            {
                var close = ClosingDelimiter(text, probe, '(', ')');
                if (close < 0) throw new FormatException("Unclosed annotation");
                arguments = text[(probe + 1)..close]; end = close + 1;
            }
            var document = new JsonObject
            {
                ["name"] = match.Groups[1].Value, ["arguments"] = arguments, ["raw"] = text[i..end],
                ["start"] = CodePointOffset(text, i), ["end"] = CodePointOffset(text, end)
            };
            annotations.Add(new(i, end, document));
            for (var j = i; j < end; j++) clean[j] = text[j] is '\n' or '\r' ? text[j] : ' ';
            i = end - 1;
        }
        return new(new string(clean), annotations);
    }

    internal static int ClosingDelimiter(string text, int start, char opening, char closing)
    {
        var code = Mask(text, true); var depth = 0;
        for (var i = start; i < code.Length; i++)
        {
            if (code[i] == opening) depth++;
            else if (code[i] == closing && --depth == 0) return i;
        }
        return -1;
    }

    internal static IEnumerable<(string Raw, int Start)> Chunks(string text, char separator, bool genericAngles = false)
    {
        foreach (var span in DelimiterSpans(text, 0, text.Length, separator, genericAngles))
        {
            var part = Trimmed(text, span.Start, span.End);
            if (part.Raw.Length > 0) yield return part;
        }
    }

    internal static IEnumerable<SourceSpan> DelimiterSpans(string text, int start, int end, char separator,
        bool genericAngles = false, bool anglesAtTopLevel = false, string? masked = null)
    {
        var code = masked ?? Mask(text, true); var stack = new Stack<char>(); var begin = start; var angle = 0;
        for (var i = start; i < end; i++)
        {
            var c = code[i];
            if (c is '(' or '[' or '{' || genericAngles && !anglesAtTopLevel && c == '<') stack.Push(c);
            else if (c is ')' or ']' or '}' || genericAngles && !anglesAtTopLevel && c == '>') { if (stack.Count > 0) stack.Pop(); }
            else if (genericAngles && anglesAtTopLevel && stack.Count == 0 && c == '<') angle++;
            else if (genericAngles && anglesAtTopLevel && stack.Count == 0 && c == '>') angle = Math.Max(0, angle - 1);
            else if (c == separator && stack.Count == 0 && angle == 0)
            {
                yield return new(begin, i); begin = i + 1;
            }
        }
        yield return new(begin, end);
    }

    private static (string Raw, int Start) Trimmed(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        return (text[start..end], start);
    }

    internal static IReadOnlyList<SourceSpan> DeclarationSpans(string text)
    {
        var code = Mask(text, true); var declarations = Rx(@"\b(interface|parcelable|union|enum)\s+([\w]+(?:\.[\w]+)*)")
            .Matches(code).Select(x => x.Index).ToHashSet();
        var result = new List<SourceSpan>(); var braces = 0; var parens = 0; var boundary = 0; int? active = null;
        for (var i = 0; i < code.Length; i++)
        {
            if (declarations.Contains(i) && braces == 0 && parens == 0)
            {
                if (active.HasValue) result.Add(new(active.Value, i));
                active = boundary;
            }
            switch (code[i])
            {
                case '(': parens++; break;
                case ')': parens = Math.Max(0, parens - 1); break;
                case '{': braces++; break;
                case '}':
                    braces = Math.Max(0, braces - 1);
                    if (braces == 0 && parens == 0)
                    {
                        if (active.HasValue) { result.Add(new(active.Value, i + 1)); active = null; }
                        boundary = i + 1;
                    }
                    break;
                case ';' when braces == 0 && parens == 0:
                    if (active.HasValue) { result.Add(new(active.Value, i + 1)); active = null; }
                    boundary = i + 1;
                    break;
            }
        }
        if (active.HasValue) result.Add(new(active.Value, text.Length));
        return result;
    }

    internal static string MaskSpans(string text, IEnumerable<SourceSpan> spans)
    {
        var chars = text.ToCharArray();
        foreach (var span in spans)
            for (var i = span.Start; i < span.End; i++) chars[i] = text[i] is '\n' or '\r' ? text[i] : ' ';
        return new string(chars);
    }

    internal static string TypeName(string text)
    {
        var clean = ParseAnnotations(text).Masked;
        clean = Rx(@"\b(final|optional)\s+").Replace(clean, "").Trim();
        clean = Rx(@"\s+").Replace(clean, " ");
        return Rx(@"\s*([<>,\[\]])\s*").Replace(clean, "$1");
    }

    internal static string ReturnType(string head)
    {
        var clean = Rx(@"^(?:(?:public|protected|private|static|final|abstract|default|synchronized|native|strictfp|deprecated|inline|operator|suspend|oneway)\s+)+").Replace(head, "").Trim();
        if (clean.StartsWith('<'))
        {
            var close = ClosingDelimiter(clean, 0, '<', '>');
            if (close < 0) throw new FormatException("Unbalanced callable generic parameters");
            clean = clean[(close + 1)..].Trim();
        }
        return TypeName(clean);
    }

    internal static JsonObject Parameter(string raw, bool aidl)
    {
        var annotated = ParseAnnotations(raw); var clean = annotated.Masked.Trim(); string? direction = null; string? name = null;
        if (aidl)
        {
            var match = Rx(@"^(inout|in|out)\s+").Match(clean);
            if (match.Success) { direction = match.Groups[1].Value; clean = clean[match.Length..]; }
            match = Rx(@"^(.+?)(?:\s+|(?<=[\]>]))([A-Za-z_$][\w$]*)$").Match(clean);
            if (!match.Success) throw new FormatException("AIDL parameter type/name unresolved");
            clean = match.Groups[1].Value; name = match.Groups[2].Value;
        }
        else
        {
            clean = Rx(@"\b(final|optional)\s+").Replace(clean, "");
            var match = Rx(@"^(.+?)\s+([A-Za-z_$][\w$]*)(?:\s*=.*)?$").Match(clean);
            if (match.Success && !match.Groups[1].Value.EndsWith("extends", StringComparison.Ordinal) && !match.Groups[1].Value.EndsWith("super", StringComparison.Ordinal))
            { clean = match.Groups[1].Value; name = match.Groups[2].Value; }
        }
        return new() { ["type"] = TypeName(clean), ["name"] = name, ["direction"] = direction, ["annotations"] = Annotations(annotated.Annotations) };
    }

    internal static JsonObject Row(string kind, string canonical, string? owner, string package, string name,
        string raw, int line, string? returnType = null, JsonArray? parameters = null, IEnumerable<SourceAnnotation>? annotations = null,
        IEnumerable<SourceAnnotation>? allAnnotations = null, JsonObject? extras = null) => new()
    {
        ["kind"] = kind, ["canonical"] = canonical, ["owner"] = owner, ["package"] = package, ["name"] = name,
        ["raw"] = raw, ["line"] = line, ["return_type"] = returnType, ["parameters"] = parameters ?? new JsonArray(),
        ["annotations"] = Annotations(annotations ?? []), ["all_annotations"] = Annotations(allAnnotations ?? annotations ?? []),
        ["extras"] = extras ?? new JsonObject()
    };

    internal static JsonObject Callable(string raw, string owner, string package, int line, bool aidl)
    {
        var annotated = ParseAnnotations(raw); var clean = annotated.Masked.Trim().TrimEnd(';').Trim();
        var constructor = !aidl && clean.StartsWith("ctor ", StringComparison.Ordinal);
        var body = aidl ? clean : clean[(clean.IndexOf(' ') + 1)..].Trim();
        var open = body.IndexOf('('); var close = body.LastIndexOf(')');
        if (open < 0 || close < open) throw new FormatException("Unclosed parameter list");
        var head = body[..open].Trim();
        if (aidl && head.IndexOfAny(['{', '}', ';']) >= 0) throw new FormatException("Unsupported merged/nested AIDL callable head");
        var name = Rx(@"([\w$]+)$").Match(head);
        if (!name.Success) throw new FormatException("Unresolved callable name");
        var originalOpen = annotated.Masked.IndexOf('('); var originalClose = annotated.Masked.LastIndexOf(')');
        var parameters = new JsonArray(Chunks(raw[(originalOpen + 1)..originalClose], ',', true).Select(x => (JsonNode)Parameter(x.Raw, aidl)).ToArray());
        var canonical = owner + "#" + (constructor ? "<init>" : name.Value) + "(" + string.Join(',', parameters.Select(x => x!["type"]!.GetValue<string>())) + ")";
        var extras = new JsonObject();
        if (aidl)
        {
            var suffix = body[(close + 1)..].Trim(); var explicitId = Rx(@"^=\s*(\d+)$").Match(suffix);
            extras["oneway"] = Rx(@"\boneway\b").IsMatch(head);
            extras["declared_method_id"] = explicitId.Success ? JsonNode.Parse(explicitId.Groups[1].Value) : null;
            extras["wire_transaction_code"] = null;
            extras["wire_status"] = "unknown; generated Binder transaction constants and codecs not imported";
            if (suffix.Length > 0 && !explicitId.Success) extras["unresolved_suffix"] = suffix;
        }
        return Row(constructor ? "constructor" : "method", canonical, owner, package, name.Value, raw, line,
            constructor ? null : ReturnType(head[..name.Index].Trim()), parameters,
            annotated.Annotations.Where(x => x.End <= originalOpen), annotated.Annotations, extras);
    }

    internal static int Line(string text, int position) => 1 + text.AsSpan(0, position).Count('\n');
    internal static int ByteOffset(string text, int position) => Encoding.UTF8.GetByteCount(text.AsSpan(0, position));
    internal static int CodePointOffset(string text, int position) => text[..position].EnumerateRunes().Count();
}
