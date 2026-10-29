using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Five retained Binder declaration token families, never compiled wire or runtime proof.</summary>
public sealed class NativeDescriptorSourceFacts : INativeDescriptorSourceFacts, INativeDescriptorValueSourceFacts
{
    public JsonObject ParseValue(JsonNode? text, JsonNode? language)
    {
        if (language is not JsonValue scalar || !scalar.TryGetValue<string>(out var selected) || selected is not ("cpp" or "rust"))
            throw new ArgumentException("Explicit source language required", nameof(language));
        if (text is JsonValue source && source.TryGetValue<string>(out var characters)) return Parse(characters, selected);
        // lex checks len before any string operation; mask joins an empty
        // iterable into an empty string. Preserve that original accepted path.
        if (text is JsonArray { Count: 0 } or JsonObject { Count: 0 }) return Parse("", selected);
        throw new ArgumentException("Expected source text with original lexical operations", nameof(text));
    }
    private readonly record struct LexicalSpan(int Start, int End, bool Comment);
    private const string CppFamilies = "IMPLEMENT_META_INTERFACE|IMPLEMENT_HYBRID_META_INTERFACE|IMPLEMENT_META_NESTED_INTERFACE|AIBinder_Class_define";
    private const string Space = @"[\s\u001c-\u001f]";
    private const string RustRawStart = "(?:b|c)?r(#{0,255})\"";
    // The retained limit counts source characters, not UTF16 code units.
    // Atomic scalar pairs prevent backtracking from splitting a surrogate pair.
    private const string CppRawStart = "(?:u8|u|U|L)?R\"((?>[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]|[^ ()\\t\\r\\n\\\\]){0,16})\\(";
    // Each language declares its raw prefix once. The lexer and exact literal
    // decoder bind different anchors to that same grammar. Regexes are reused
    // across the lexical pass rather than allocated at every source character.
    private static readonly Regex RustRawPrefix = SourceSyntax.Rx(@"\G" + RustRawStart);
    private static readonly Regex CppRawPrefix = SourceSyntax.Rx(@"\G" + CppRawStart);
    private static readonly Regex RustRawLiteral = SourceSyntax.Rx(@"\A" + RustRawStart + "([\\s\\S]*)\"\\1\\z");
    private static readonly Regex CppRawLiteral = SourceSyntax.Rx(@"\A" + CppRawStart + "([\\s\\S]*)\\)\\1\"\\z");
    private static readonly Regex QuotedLiteral = SourceSyntax.Rx("\\A(?:u8|u|U|L|b|c)?(\"(?:\\\\.|[^\"\\\\])*\")\\z");
    private static readonly Regex RustCharacter = SourceSyntax.Rx(@"\G'(?:\\(?:u\{[0-9a-fA-F]+\}|x[0-9a-fA-F]{2}|[^\n])|[\uD800-\uDBFF][\uDC00-\uDFFF]|[^'\\\r\n])'");
    private static readonly Regex CppCharacter = SourceSyntax.Rx(@"\G'(?:\\[^\n]|[^'\\\r\n])*'");

    public JsonObject Parse(string text, string language)
    {
        if (language is not ("cpp" or "rust")) throw new ArgumentException("Explicit source language required", nameof(language));
        ArgumentNullException.ThrowIfNull(text);
        var (spans, issues) = Lex(text, language);
        var code = Mask(text, spans, true); var commentsOnly = Mask(text, spans, false); var facts = new JsonArray();
        var pattern = language == "rust" ? @"(declare_binder_interface)" + Space + @"*!" + Space + @"*([({])" : @"(" + CppFamilies + @")" + Space + @"*(\()";
        foreach (Match match in SourceSyntax.Rx(pattern).Matches(code))
        {
            if (match.Index > 0 && SourceText.OriginalWord(Rune.GetRuneAt(code, char.IsLowSurrogate(code[match.Index - 1]) && match.Index > 1 && char.IsHighSurrogate(code[match.Index - 2]) ? match.Index - 2 : match.Index - 1))) continue;
            var family = match.Groups[1].Value; var opening = match.Index + match.Length - 1;
            var lineStart = code.LastIndexOf('\n', Math.Max(0, match.Index - 1), match.Index) + 1;
            if (language == "cpp" && SourceSyntax.Rx(@"#" + Space + @"*define" + Space + @"*$").IsMatch(code[lineStart..match.Index])) continue;
            var end = Balanced(code, opening);
            if (end < 0)
            {
                facts.Add(new JsonObject
                {
                    ["family"] = family, ["line"] = SourceSyntax.Line(text, match.Index), ["status"] = "unclosed-token-form",
                    ["raw"] = text[match.Index..], ["descriptor_literal"] = null, ["compiled_endpoint_proven"] = false
                });
                continue;
            }
            string? symbol = null; string? expression = null;
            if (language == "rust")
            {
                var prefix = SourceSyntax.Rx(@"\A" + Space + @"*((?:r#)?[A-Za-z_][^\s\u001c-\u001f\[]*)" + Space + @"*\[").Match(code[(opening + 1)..end]);
                if (prefix.Success && prefix.Groups[1].Value[(prefix.Groups[1].Value.StartsWith("r#", StringComparison.Ordinal) ? 2 : 0)..].EnumerateRunes().All(SourceText.OriginalWord))
                {
                    var bracket = opening + prefix.Length; var closing = Balanced(code, bracket);
                    if (closing >= 0 && closing < end)
                    { symbol = prefix.Groups[1].Value; expression = SourceText.Strip(commentsOnly[(bracket + 1)..closing]); }
                }
            }
            else
            {
                // The existing shared delimiter iterator consumes the already
                // language-masked input, including nested call arguments.
                var arguments = SourceSyntax.DelimiterSpans(text, opening + 1, end, ',', masked: code)
                    .Select(span => SourceText.Strip(commentsOnly[span.Start..span.End])).ToArray();
                expression = family == "AIBinder_Class_define" ? arguments[0] : arguments[^1];
                if (family != "AIBinder_Class_define") symbol = arguments[0];
            }
            var literal = expression is null ? null : Literal(expression, language);
            facts.Add(new JsonObject
            {
                ["family"] = family, ["line"] = SourceSyntax.Line(text, match.Index),
                ["character_span"] = new JsonArray(SourceSyntax.CodePointOffset(text, match.Index), SourceSyntax.CodePointOffset(text, end + 1)),
                ["declared_symbol_token"] = symbol, ["descriptor_expression"] = expression, ["descriptor_literal"] = literal,
                ["status"] = literal is null ? "computed-or-unresolved-argument" : "literal-argument-candidate", ["raw"] = text[match.Index..(end + 1)],
                ["compiled_endpoint_proven"] = false, ["wire_transaction_schema"] = null,
                ["preprocessor_or_build_conditions"] = "unresolved", ["runtime_registration"] = "unknown"
            });
        }
        return new JsonObject { ["facts"] = facts, ["lexical_issues"] = issues, ["compiled_or_runtime_proven"] = false };
    }

    // DeclarationSource masking cannot represent C++ raw delimiters/continued
    // comments or Rust nested comments/raw literals/lifetimes. This single
    // source-domain lexical pass feeds both code and comments-only projections.
    private static (IReadOnlyList<LexicalSpan> Spans, JsonArray Issues) Lex(string text, string language)
    {
        var spans = new List<LexicalSpan>(); var issues = new JsonArray(); var position = 0;
        void Issue(int start, string reason) => issues.Add(new JsonObject { ["position"] = SourceSyntax.CodePointOffset(text, start), ["reason"] = reason });
        while (position < text.Length)
        {
            var start = position;
            if (text.AsSpan(position).StartsWith("//", StringComparison.Ordinal))
            {
                var end = text.IndexOf('\n', position); if (end < 0) end = text.Length;
                while (language == "cpp" && end < text.Length && text[position..end].TrimEnd('\r').EndsWith('\\'))
                { var next = text.IndexOf('\n', end + 1); end = next < 0 ? text.Length : next; }
                spans.Add(new(start, end, true)); position = end; continue;
            }
            if (text.AsSpan(position).StartsWith("/*", StringComparison.Ordinal))
            {
                var depth = 1; position += 2;
                while (position < text.Length && depth > 0)
                {
                    if (language == "rust" && text.AsSpan(position).StartsWith("/*", StringComparison.Ordinal)) { depth++; position += 2; }
                    else if (text.AsSpan(position).StartsWith("*/", StringComparison.Ordinal)) { depth--; position += 2; }
                    else position++;
                }
                if (depth > 0) Issue(start, "Unclosed block comment");
                spans.Add(new(start, position, true)); continue;
            }
            var raw = (language == "rust" ? RustRawPrefix : CppRawPrefix).Match(text, position);
            if (raw.Success)
            {
                var closing = language == "rust" ? "\"" + raw.Groups[1].Value : ")" + raw.Groups[1].Value + "\"";
                var end = text.IndexOf(closing, position + raw.Length, StringComparison.Ordinal);
                if (end < 0) { Issue(start, "Unclosed raw literal"); end = text.Length; }
                else end += closing.Length;
                spans.Add(new(start, end, false)); position = end; continue;
            }
            if (text[position] == '"')
            {
                position++; var closed = false;
                while (position < text.Length)
                {
                    if (text[position] == '\\') position += 2;
                    else if (text[position] == '"') { position++; closed = true; break; }
                    else position++;
                }
                if (!closed) Issue(start, "Unclosed string literal");
                spans.Add(new(start, Math.Min(position, text.Length), false)); continue;
            }
            if (text[position] == '\'')
            {
                // A Rust lifetime is not a character literal. Supplementary
                // Unicode characters count as one character in the source.
                var character = (language == "rust" ? RustCharacter : CppCharacter).Match(text, position);
                if (character.Success) { position += character.Length; spans.Add(new(start, position, false)); continue; }
            }
            position++;
        }
        return (spans, issues);
    }

    private static string Mask(string text, IReadOnlyList<LexicalSpan> spans, bool literals)
    {
        var characters = text.ToCharArray();
        foreach (var span in spans.Where(span => span.Comment || literals))
            for (var index = span.Start; index < span.End; index++) characters[index] = text[index] == '\n' ? '\n' : ' ';
        return new string(characters);
    }

    private static int Balanced(string code, int start)
    {
        var stack = new Stack<char>();
        for (var index = start; index < code.Length; index++)
        {
            var token = code[index];
            if (token is '(' or '[' or '{') stack.Push(token switch { '(' => ')', '[' => ']', _ => '}' });
            else if (token is ')' or ']' or '}')
            {
                if (stack.Count == 0 || token != stack.Pop()) return -1;
                if (stack.Count == 0) return index;
            }
        }
        return -1;
    }

    private static string? Literal(string raw, string language)
    {
        var value = SourceText.Strip(raw);
        var matched = (language == "rust" ? RustRawLiteral : CppRawLiteral).Match(value);
        if (matched.Success) return matched.Groups[2].Value;
        matched = QuotedLiteral.Match(value);
        if (!matched.Success) return null;
        try { return JsonSerializer.Deserialize<string>(matched.Groups[1].Value); }
        catch (JsonException) { return null; }
    }
}
