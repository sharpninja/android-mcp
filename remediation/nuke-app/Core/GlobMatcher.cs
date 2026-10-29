namespace AndroidMcp.BuildTool.Core;

/// <summary>One pure scalar matcher with explicit filename and POSIX path profiles.</summary>
public sealed class GlobMatcher : IGlobMatcher
{
    private enum Kind { Literal, Character, Star, CharacterSet, Directories }
    private sealed record Token(Kind Kind, int Scalar = 0, bool CrossSlash = true, bool Negated = false, IReadOnlyList<(int Start, int End)>? Ranges = null);

    public bool Matches(string text, string pattern, GlobMatchingProfile profile)
    {
        if (profile is not (GlobMatchingProfile.PublicationFilename or GlobMatchingProfile.PosixFullPath)) throw new ArgumentOutOfRangeException(nameof(profile));
        ArgumentNullException.ThrowIfNull(text); ArgumentNullException.ThrowIfNull(pattern);
        if (profile == GlobMatchingProfile.PosixFullPath)
        {
            text = NormalizePosixPath(text); pattern = NormalizePosixPath(pattern);
        }
        // Literal policy entries need no token graph. Keep lexical normalization
        // and case sensitivity identical to their explicit matching profile.
        if (pattern.AsSpan().IndexOfAny('*', '?', '[') < 0)
            return string.Equals(text, pattern, StringComparison.Ordinal);
        var tokens = new List<Token>();
        if (profile == GlobMatchingProfile.PublicationFilename) Segment(pattern, true, tokens);
        else
        {
            var parts = pattern.Split('/');
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index]; var last = index == parts.Length - 1;
                if (part == "*")
                {
                    // glob.translate uses a nonempty complete segment for a bare '*'.
                    tokens.Add(new(Kind.Character, CrossSlash: false)); tokens.Add(new(Kind.Star, CrossSlash: false));
                    if (!last) tokens.Add(new(Kind.Literal, '/'));
                }
                else if (part == "**")
                {
                    if (last) tokens.Add(new(Kind.Star));
                    else if (parts[index + 1] != "**") tokens.Add(new(Kind.Directories));
                }
                else
                {
                    Segment(part, false, tokens);
                    if (!last) tokens.Add(new(Kind.Literal, '/'));
                }
            }
        }
        return Evaluate(Scalars(text), tokens);
    }

    // PurePosixPath normalizes repeated separators and '.' without resolving '..'.
    // This is a lexical operation, never filesystem containment or symlink admission.
    internal static string NormalizePosixPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var anchor = value.StartsWith("//", StringComparison.Ordinal) && !value.StartsWith("///", StringComparison.Ordinal) ? "//" : value.StartsWith('/') ? "/" : "";
        return anchor + string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(x => x != "."));
    }

    private static int[] Scalars(string value)
    {
        var result = new List<int>(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) result.Add(char.ConvertToUtf32(value[index], value[++index]));
            else result.Add(value[index]);
        }
        return result.ToArray();
    }

    private static void Segment(string pattern, bool crossSlash, List<Token> output)
    {
        var characters = Scalars(pattern);
        for (var index = 0; index < characters.Length; index++)
        {
            var scalar = characters[index];
            if (scalar == '*')
            {
                output.Add(new(Kind.Star, CrossSlash: crossSlash));
                while (index + 1 < characters.Length && characters[index + 1] == '*') index++;
            }
            else if (scalar == '?') output.Add(new(Kind.Character, CrossSlash: crossSlash));
            else if (scalar == '[')
            {
                var end = index + 1;
                if (end < characters.Length && characters[end] == '!') end++;
                if (end < characters.Length && characters[end] == ']') end++;
                while (end < characters.Length && characters[end] != ']') end++;
                if (end == characters.Length) { output.Add(new(Kind.Literal, '[')); continue; }
                var first = index + 1; var negated = characters[first] == '!'; if (negated) first++;
                var ranges = new List<(int Start, int End)>();
                for (var part = first; part < end; part++)
                {
                    if (part + 2 < end && characters[part + 1] == '-')
                    {
                        // Descending/empty ranges are removed, as in legacy fnmatch.
                        if (characters[part] <= characters[part + 2]) ranges.Add((characters[part], characters[part + 2]));
                        part += 2;
                    }
                    else ranges.Add((characters[part], characters[part]));
                }
                output.Add(new(Kind.CharacterSet, Negated: negated, Ranges: ranges)); index = end;
            }
            else output.Add(new(Kind.Literal, scalar));
        }
    }

    private static bool Evaluate(IReadOnlyList<int> text, IReadOnlyList<Token> tokens)
    {
        // Bottom-up NFA evaluation avoids recursion/backtracking over imported patterns.
        var next = new bool[text.Count + 1]; next[text.Count] = true;
        for (var tokenIndex = tokens.Count - 1; tokenIndex >= 0; tokenIndex--)
        {
            var token = tokens[tokenIndex]; var current = new bool[text.Count + 1];
            if (token.Kind == Kind.Directories)
            {
                var endings = new bool[text.Count + 1];
                for (var index = text.Count - 1; index >= 0; index--) endings[index] = endings[index + 1] || text[index] == '/' && next[index + 1];
                for (var index = text.Count; index >= 0; index--) current[index] = next[index] || index < text.Count && endings[index + 1];
            }
            else
            {
                for (var index = text.Count; index >= 0; index--)
                {
                    var available = index < text.Count;
                    if (token.Kind == Kind.Star) current[index] = next[index] || available && (token.CrossSlash || text[index] != '/') && current[index + 1];
                    else if (available && next[index + 1])
                    {
                        current[index] = token.Kind switch
                        {
                            Kind.Literal => text[index] == token.Scalar,
                            Kind.Character => token.CrossSlash || text[index] != '/',
                            Kind.CharacterSet => token.Ranges!.Any(range => text[index] >= range.Start && text[index] <= range.End) != token.Negated,
                            _ => false
                        };
                    }
                }
            }
            next = current;
        }
        return next[0];
    }
}
