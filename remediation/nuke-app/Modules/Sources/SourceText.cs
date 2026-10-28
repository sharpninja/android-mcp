using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Sources;

// Literal source-format compatibility primitives shared by build, APEX and hash facts.
internal static class SourceText
{
    private static readonly Regex BuildTrivia = new("\"(?:\\\\.|[^\"\\\\])*\"|/\\*.*?\\*/|//[^\\n]*", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    internal static string MaskBuildComments(string text) => BuildTrivia.Replace(text, match =>
        match.Value.StartsWith('"') ? match.Value : new string(match.Value.Select(character => character == '\n' ? '\n' : ' ').ToArray()));

    internal static IEnumerable<(int Number, string Raw)> Lines(string text)
    {
        var start = 0; var number = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\n' or '\r' or '\v' or '\f' or '\u001c' or '\u001d' or '\u001e' or '\u0085' or '\u2028' or '\u2029')) continue;
            yield return (number++, text[start..index]);
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
            start = index + 1;
        }
        if (start < text.Length) yield return (number, text[start..]);
    }

    internal static string Strip(string value)
    {
        var start = 0; var end = value.Length;
        static bool Space(char c) => char.IsWhiteSpace(c) || c is >= '\u001c' and <= '\u001f';
        while (start < end && Space(value[start])) start++;
        while (end > start && Space(value[end - 1])) end--;
        return value[start..end];
    }
}
