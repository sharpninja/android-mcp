using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record PermissionReferenceAttributeCase(PermissionReferenceCase Source, string RawAttribute, string ExpectedAttribute)
{
    public const string EncodedText = "Constant Value: &quot;other.permission.X&quot;";
    public const string DecodedText = "Constant Value: \"other.permission.X\"";
}
public static class PermissionReferenceAttributeContractCases
{
    private static PermissionReferenceAttributeCase Create(string id, string attribute)
    {
        var original = PermissionReferenceContractCases.Get("REF-01");
        var expected = (JsonObject)original.Expected.DeepClone();
        expected["fields"]![0]!["introduced_raw"] = attribute;
        return new(new(id, original.Html.Replace("data-version-added=\"37.2\"", "data-version-added=\"" + attribute + "\"", StringComparison.Ordinal), expected), attribute, attribute);
    }
    public static readonly PermissionReferenceAttributeCase[] All =
        [Create("REFERENCE-ATTRIBUTE-PREFIX", "&notit;"), Create("REFERENCE-ATTRIBUTE-EQUALS", "&copy=")];
    public static IEnumerable<object[]> Cases() => All.Select(test => new object[] { test.Source.Id });
    public static IEnumerable<object[]> Controls() => All.SelectMany(test =>
        new[] { "omit-attribute", "text-for-attribute", "omit-text", "attribute-for-text", "wrong-metadata", "duplicate-attribute", "ignore-returned-value" }
            .Select(fault => new object[] { test.Source.Id, fault }));
    public static PermissionReferenceAttributeCase Get(string id) => All.Single(test => test.Source.Id == id);

    public static void AssertSection(PermissionReferenceAttributeCase test,
        Func<IProfiledSourceHtmlDecoder, IPermissionReferenceSourceFacts> factory, IProfiledSourceHtmlDecoder decoder)
    {
        foreach (var overrideValue in new string?[] { null, "source-attribute-result-probe" })
        {
            var recording = new ObservedPermissionHtmlDecoder(decoder, test.RawAttribute, overrideValue);
            var expected = (JsonObject)test.Source.Expected.DeepClone();
            expected["fields"]![0]!["introduced_raw"] = overrideValue ?? test.ExpectedAttribute;
            PermissionReferenceContractCases.AssertContract(test.Source with { Expected = expected }, factory(recording));
            var attributeCall = Assert.Single(recording.Received, call => call.Text == test.RawAttribute);
            Assert.Equal(SourceHtmlDecodingProfile.Attribute, attributeCall.Profile); Assert.True(attributeCall.ExplicitProfile);
            var textCall = Assert.Single(recording.Received, call => call.Text == PermissionReferenceAttributeCase.EncodedText);
            Assert.Equal(SourceHtmlDecodingProfile.Text, textCall.Profile);
        }
    }
    public static void AssertDecoderFailure(PermissionReferenceAttributeCase test,
        Func<IProfiledSourceHtmlDecoder, IPermissionReferenceSourceFacts> factory, IProfiledSourceHtmlDecoder decoder)
    {
        var failure = new IOException("scripted attribute decoder failure");
        var recording = new ObservedPermissionHtmlDecoder(decoder, test.RawAttribute, failure: failure);
        var actual = Record.Exception(() => factory(recording).Parse(Encoding.UTF8.GetBytes(test.Source.Html)));
        Assert.Same(failure, actual);
        var call = Assert.Single(recording.Received, call => call.Text == test.RawAttribute);
        Assert.Equal(SourceHtmlDecodingProfile.Attribute, call.Profile);
    }
}

public sealed class ObservedPermissionHtmlDecoder(IProfiledSourceHtmlDecoder inner, string attribute,
    string? overrideValue = null, Exception? failure = null) : IProfiledSourceHtmlDecoder
{
    public List<HtmlProfileCall> Received { get; } = [];
    public string Decode(string text)
    {
        Received.Add(new(text, SourceHtmlDecodingProfile.Text, false));
        return inner.Decode(text);
    }
    public string Decode(string text, SourceHtmlDecodingProfile profile)
    {
        Received.Add(new(text, profile, true));
        if (profile == SourceHtmlDecodingProfile.Attribute && text == attribute)
        {
            if (failure is not null) throw failure;
            if (overrideValue is not null) return overrideValue;
        }
        return inner.Decode(text, profile);
    }
}
