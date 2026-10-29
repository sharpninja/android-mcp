using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;
using Xunit.Abstractions;

namespace AndroidMcp.BuildTool.Tests.Sources;

// Read-only diagnostic requested by Root. This is an observation of current
// admission/decoder/candidate effects, not an assertion of Python parity.
[Trait("Gate", "Diagnostic")]
public sealed class SourceUnicodeBoundaryDiagnosticTests(ITestOutputHelper output)
{
    [Theory, InlineData("d800"), InlineData("dc00")]
    public void ObserveExactEscapedLoneSurrogateWithoutChangingAdmission(string escape)
    {
        var literal = "\"\\u" + escape + "\""; var observations = new JsonObject { ["escape"] = escape, ["literal_source"] = literal };
        try
        {
            var decoded = JsonSerializer.Deserialize<string>(literal);
            observations["decoder_code_units"] = decoded is null ? null : new JsonArray(decoded.Select(c => (JsonNode?)JsonValue.Create((int)c)).ToArray());
            observations["decoder_replacement"] = decoded?.Contains('\ufffd');
        }
        catch (Exception error) { observations["decoder_exception"] = error.GetType().FullName; observations["decoder_message"] = error.Message; }
        try
        {
            var inspection = new StrictJsonInspector().Inspect(Encoding.UTF8.GetBytes(literal));
            observations["inner_literal_admission_fatal"] = inspection.HasFatalErrors;
            observations["inner_literal_admission_diagnostics"] = new JsonArray(inspection.Diagnostics.Select(item => (JsonNode?)JsonValue.Create(item.Code)).ToArray());
        }
        catch (Exception error) { observations["inner_literal_admission_exception"] = error.GetType().FullName; observations["inner_literal_admission_message"] = error.Message; }
        var document = (JsonObject)SourceFactContractCases.Get("FACT-FN01").Input.DeepClone();
        document["extensions"]!["annotations"]![0]!["arguments"] = literal;
        var outer = JsonSerializer.SerializeToUtf8Bytes(document); var outerInspection = new StrictJsonInspector().Inspect(outer);
        observations["outer_document_admission_fatal"] = outerInspection.HasFatalErrors;
        var producer = new SourceFactProducer(new CanonicalJson()); var taxonomy = producer.Taxonomy(document);
        var retained = taxonomy["functional_evidence"]![0]!;
        observations["taxonomy_raw_preserved"] = retained["source_annotation"]!["arguments"]!.GetValue<string>() == literal;
        observations["taxonomy_literal_null"] = retained["service_string_literal"] is null;
        observations["taxonomy_symbol_null"] = retained["service_symbol_expression"] is null;
        var source = "AIBinder_Class_define(" + literal + ",a,b,c)";
        var descriptor = new NativeDescriptorSourceFacts().Parse(source, "cpp"); var fact = descriptor["facts"]![0]!;
        observations["descriptor_raw_preserved"] = fact["raw"]!.GetValue<string>() == source;
        observations["descriptor_expression_preserved"] = fact["descriptor_expression"]!.GetValue<string>() == literal;
        observations["descriptor_literal_null"] = fact["descriptor_literal"] is null;
        observations["descriptor_status"] = fact["status"]?.DeepClone(); observations["descriptor_lexical_issues"] = descriptor["lexical_issues"]?.DeepClone();
        Assert.Equal(literal, document["extensions"]!["annotations"]![0]!["arguments"]!.GetValue<string>());
        Assert.False(outerInspection.HasFatalErrors); Assert.True(observations["taxonomy_raw_preserved"]!.GetValue<bool>());
        Assert.True(observations["descriptor_raw_preserved"]!.GetValue<bool>()); Assert.True(observations["descriptor_expression_preserved"]!.GetValue<bool>());
        output.WriteLine(observations.ToJsonString());
    }
}
