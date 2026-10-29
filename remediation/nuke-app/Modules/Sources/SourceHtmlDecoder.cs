using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The legacy HTML5 entity-unescape rules over pinned official entity data.</summary>
public sealed class SourceHtmlDecoder : IProfiledSourceHtmlDecoder
{
    private const string Resource = "AndroidMcp.BuildTool.Sources.entities.whatwg.json";
    private const string DataSha256 = "d741d877ac77c4194c4ad526b5b4a19aef8dfe411ab840a466891cdbb9f362e6";
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Entries = new(LoadEntries);
    private static readonly Regex Reference = new(@"&(#[0-9]+;?|#[xX][0-9a-fA-F]+;?|[^\t\n\f <&#;]{1,32};?)", RegexOptions.CultureInvariant);
    private static readonly Regex AttributeReference = new(@"&(#[0-9]+|#[xX][0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*)[;=]?", RegexOptions.CultureInvariant);
    // U+0080 through U+009F substitutions, including the five unchanged C1 controls.
    private const string C1 = "\u20ac\u0081\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u008d\u017d\u008f\u0090\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u009d\u017e\u0178";

    public string Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DecodeText(text);
    }

    public string Decode(string text, SourceHtmlDecodingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(text);
        return profile switch
        {
            SourceHtmlDecodingProfile.Text => DecodeText(text),
            SourceHtmlDecodingProfile.Attribute => AttributeReference.Replace(text, match => ReplaceAttribute(match.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
    }

    private static string DecodeText(string text) => Reference.Replace(text, match => Replace(match.Groups[1].Value));

    private static string ReplaceAttribute(string reference)
    {
        // Numeric matches reuse the existing text replacement, including any
        // unmatched suffix. Named matches require one exact table entry.
        if (reference.StartsWith("&#", StringComparison.Ordinal)) return DecodeText(reference);
        return !reference.EndsWith('=') && Entries.Value.TryGetValue(reference[1..], out var value) ? value : reference;
    }

    private static IReadOnlyDictionary<string, string> LoadEntries()
    {
        using var stream = typeof(SourceHtmlDecoder).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("Pinned HTML entity data resource is missing");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); var raw = bytes.ToArray();
        if (ContentHash.Sha256(raw) != DataSha256) throw new InvalidOperationException("Pinned HTML entity data SHA256 mismatch");
        return HtmlEntityDataAudit.ReadOfficial(raw, new StrictJsonInspector());
    }

    private static string Replace(string reference)
    {
        if (reference.StartsWith('#')) return Numeric(reference);
        if (Entries.Value.TryGetValue(reference, out var full)) return full;
        for (var length = reference.Length - 1; length >= 2; length--)
            if (Entries.Value.TryGetValue(reference[..length], out var prefix)) return prefix + reference[length..];
        return "&" + reference;
    }

    private static string Numeric(string reference)
    {
        var token = reference.AsSpan(1);
        if (token.EndsWith(";", StringComparison.Ordinal)) token = token[..^1];
        BigInteger value;
        if (token.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            value = BigInteger.Parse("0" + token[1..].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        else value = BigInteger.Parse(token, NumberStyles.None, CultureInfo.InvariantCulture);
        if (value == 0 || value > 0x10ffff || value >= 0xd800 && value <= 0xdfff) return "\ufffd";
        var scalar = (int)value;
        if (scalar is >= 0x80 and <= 0x9f) return C1[scalar - 0x80].ToString();
        if (scalar is >= 1 and <= 8 or 11 or >= 14 and <= 31 or 127 or >= 0xfdd0 and <= 0xfdef || (scalar & 0xffff) is 0xfffe or 0xffff) return "";
        return char.ConvertFromUtf32(scalar);
    }
}
