using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PermissionReferenceAttributeMockTests
{
    private static IProfiledSourceHtmlDecoder Decoder(PermissionReferenceAttributeCase test)
    {
        // Existing proven text provider is only a fallback for unrelated literal text.
        // The new attribute policy is explicitly scripted; no entity table/parser is duplicated.
        var text = new SourceHtmlDecoder();
        return new ScriptedProfileHtmlDecoder((input, selected) => selected == SourceHtmlDecodingProfile.Attribute && input == test.RawAttribute
            ? test.ExpectedAttribute : text.Decode(input));
    }
    private static IPermissionReferenceSourceFacts Provider(PermissionReferenceAttributeCase test, IProfiledSourceHtmlDecoder decoder, string? fault = null) =>
        new ScriptedPermissionAttributeProvider(bytes =>
        {
            Assert.Equal(Encoding.UTF8.GetBytes(test.Source.Html), bytes.ToArray());
            try
            {
                var introduced = test.ExpectedAttribute;
                if (fault != "omit-attribute") introduced = decoder.Decode(test.RawAttribute,
                    fault == "text-for-attribute" ? SourceHtmlDecodingProfile.Text : SourceHtmlDecodingProfile.Attribute);
                if (fault == "duplicate-attribute") decoder.Decode(test.RawAttribute, SourceHtmlDecodingProfile.Attribute);
                if (fault != "omit-text")
                    Assert.Equal(PermissionReferenceAttributeCase.DecodedText, decoder.Decode(PermissionReferenceAttributeCase.EncodedText,
                        fault == "attribute-for-text" ? SourceHtmlDecodingProfile.Attribute : SourceHtmlDecodingProfile.Text));
                var result = (JsonObject)test.Source.Expected.DeepClone();
                result["fields"]![0]!["introduced_raw"] = fault == "wrong-metadata" ? "wrong metadata" : fault == "ignore-returned-value" ? test.ExpectedAttribute : introduced;
                return result;
            }
            catch (IOException) when (fault == "swallow-failure") { return (JsonObject)test.Source.Expected.DeepClone(); }
        });

    [Theory, MemberData(nameof(PermissionReferenceAttributeContractCases.Cases), MemberType = typeof(PermissionReferenceAttributeContractCases))]
    public void CorrectMocksUseTheProfileBoundaryAndReturnedAttributeValue(string id)
    {
        var test = PermissionReferenceAttributeContractCases.Get(id);
        PermissionReferenceAttributeContractCases.AssertSection(test, decoder => Provider(test, decoder), Decoder(test));
    }
    [Theory, MemberData(nameof(PermissionReferenceAttributeContractCases.Controls), MemberType = typeof(PermissionReferenceAttributeContractCases))]
    public void WrongProfileOmittedCallsAndIgnoredDecoderResultsCannotPass(string id, string fault)
    {
        var test = PermissionReferenceAttributeContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => PermissionReferenceAttributeContractCases.AssertSection(test, decoder => Provider(test, decoder, fault), Decoder(test))));
    }
    [Theory, MemberData(nameof(PermissionReferenceAttributeContractCases.Cases), MemberType = typeof(PermissionReferenceAttributeContractCases))]
    public void DecoderFailurePropagatesAndSwallowingMockIsRejected(string id)
    {
        var test = PermissionReferenceAttributeContractCases.Get(id);
        PermissionReferenceAttributeContractCases.AssertDecoderFailure(test, decoder => Provider(test, decoder), Decoder(test));
        Assert.NotNull(Record.Exception(() => PermissionReferenceAttributeContractCases.AssertDecoderFailure(test, decoder => Provider(test, decoder, "swallow-failure"), Decoder(test))));
    }
}
public sealed class ScriptedPermissionAttributeProvider(Func<ReadOnlyMemory<byte>, JsonObject> handler) : IPermissionReferenceSourceFacts
{
    public JsonObject Parse(ReadOnlyMemory<byte> html) => handler(html);
}
