using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record LosslessCase(string Id,FrozenEvidenceInput Input,string ExpectedView,int Replacements,int Retained,int Embedded,string ExpectedError="")
{
    public override string ToString()=>Id;
}

public static class LosslessContractCases
{
    public const string KnownSvg="<svg xmlns=\"http://www.w3.org/2000/svg\"><text>known</text></svg>";
    public static string KnownUri=>"data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(KnownSvg));
    public static string KnownReference=>"review-asset://sha256/"+DocumentationFixture.Hash(Encoding.UTF8.GetBytes(KnownSvg));
    public static Artifact File(string path,string body,string media="text/html")=>new(path,Encoding.UTF8.GetBytes(body),media);
    public static FrozenEvidenceInput Input(string html,string svg=KnownSvg)
    {
        var files=new[]{File("known.svg",svg,"image/svg+xml"),File("page.html",html)};
        return new("page.html",files,files.Select(a=>new StoredArtifact(a.RelativePath,DocumentationFixture.Hash(a.Bytes.Span),a.Bytes.Length,a.MediaType)).ToArray());
    }
    public static IEnumerable<object[]> Cases=>Create().Select(c=>new object[]{c});
    public static IReadOnlyList<LosslessCase> Create()
    {
        var unknown="data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg>unknown</svg>"));
        var original="<p>UTF8:é</p><img src=\""+KnownUri+"\"><img src=\""+KnownUri+"\"><img src=\""+unknown+"\">";
        var expected="<p>UTF8:é</p><img src=\""+KnownReference+"\"><img src=\""+KnownReference+"\"><img src=\""+unknown+"\">";
        var adjacent="data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg>x</svg>"));
        var paddedSvg="<svg>x</svg>\n";var canonical="data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(paddedSvg));
        var noncanonical=canonical[..^3]+"h==";
        var changed=Input(KnownUri);changed=changed with{Artifacts=changed.Artifacts.Select(a=>a.RelativePath=="known.svg"?File("known.svg","changed","image/svg+xml"):a).ToArray()};
        return
        [
            new("FACTOR-ORIGINAL-UTF8-REPEATED-UNKNOWN",Input(original),expected,2,1,3),
            new("FACTOR-ORIGINAL-ADJACENT",Input(adjacent+adjacent,"<svg>x</svg>"),adjacent+adjacent,0,1,1),
            new("FACTOR-INVALID-BASE64",Input("data:image/svg+xml;base64,A"),"data:image/svg+xml;base64,A",0,1,1),
            new("FACTOR-NONCANONICAL-PADDING",Input(noncanonical,paddedSvg),noncanonical,0,1,1),
            new("FACTOR-UNKNOWN-CANONICAL",Input(unknown),unknown,0,1,1),
            new("FACTOR-KNOWN-SINGLE",Input(KnownUri),KnownReference,1,0,1),
            new("FACTOR-NO-URI",Input("<p>all original bytes é</p>"),"<p>all original bytes é</p>",0,0,0),
            new("FACTOR-MODIFIED-FROZEN-ASSET",changed,"",0,0,0,"FROZEN_HASH_CHANGED"),
            new("FACTOR-RESERVED-TOKEN-COLLISION",Input(KnownReference+KnownUri),"",0,0,0,"RESERVED_REFERENCE_COLLISION")
        ];
    }

    public static FactoredEvidence Correct(LosslessCase test)
    {
        var original=test.Input.Artifacts.Single(a=>a.RelativePath==test.Input.HtmlPath);var view=File("storyboards.factored.html",test.ExpectedView);
        var replacements=new JsonArray();
        if(test.Replacements>0)
        {
            var first=test.Id=="FACTOR-ORIGINAL-UTF8-REPEATED-UNKNOWN"?24:0;
            for(var i=0;i<test.Replacements;i++)
            {
                var start=first+i*(KnownUri.Length+12);
                replacements.Add(new JsonObject{{"original_start",start},{"original_end",start+KnownUri.Length},{"original_uri_sha256",DocumentationFixture.Hash(Encoding.ASCII.GetBytes(KnownUri))},{"replacement_uri",KnownReference},{"asset","known.svg"},{"asset_sha256",DocumentationFixture.Hash(Encoding.UTF8.GetBytes(KnownSvg))},{"encoding","RFC4648standardbase64withpadding/no-whitespace"}});
            }
        }
        var retained=new JsonArray();
        if(test.Retained>0)
        {
            var originalText=Encoding.UTF8.GetString(original.Bytes.Span);
            var start=test.Id=="FACTOR-ORIGINAL-UTF8-REPEATED-UNKNOWN"?48+2*KnownUri.Length:0;
            var end=test.Id=="FACTOR-ORIGINAL-ADJACENT"?original.Bytes.Length/2+4:original.Bytes.Length-(test.Id=="FACTOR-ORIGINAL-UTF8-REPEATED-UNKNOWN"?2:0);
            var reason=test.Id=="FACTOR-INVALID-BASE64"?"invalid base64 retained literally":test.Id=="FACTOR-NONCANONICAL-PADDING"?"noncanonical base64 retained literally":"not an assigned canonical SVG; retained literally";
            retained.Add(new JsonObject{{"start",start},{"end",end},{"reason",reason}});
        }
        var map=new JsonObject{{"original_artifact",test.Input.HtmlPath},{"original_sha256",DocumentationFixture.Hash(original.Bytes.Span)},{"original_bytes",original.Bytes.Length},{"view_sha256",DocumentationFixture.Hash(view.Bytes.Span)},{"view_bytes",view.Bytes.Length},{"replacements",replacements},{"unmatched_retained_literals",retained},{"all_other_html_bytes_unchanged",true},{"frozen_inputs_modified",false},{"design_scope_reduced",false},{"model_or_network_calls",0}};
        var verification=new JsonObject{{"round_trip_byte_identical",true},{"original_sha256",DocumentationFixture.Hash(original.Bytes.Span)},{"embedded_svg_instances",test.Embedded},{"replaced_known_instances",test.Replacements},{"distinct_assets",test.Replacements>0?1:0},{"original_bytes",original.Bytes.Length},{"review_view_bytes",view.Bytes.Length},{"bytes_represented_by_exact_asset_references",original.Bytes.Length-view.Bytes.Length},{"new_requirements_omitted",0}};
        return new(view,File("reconstruction-map.json",map.ToJsonString(),"application/json"),File("verification.json",verification.ToJsonString(),"application/json"));
    }

    public static void AssertContract(LosslessCase test,ILosslessEvidenceService provider)
    {
        var before=test.Input.Artifacts.Select(a=>DocumentationFixture.Hash(a.Bytes.Span)).ToArray();
        if(test.ExpectedError.Length>0){Assert.Equal(test.ExpectedError,Assert.Throws<EvidenceIntegrityException>(()=>provider.Factor(test.Input)).Code);Assert.Equal(before,test.Input.Artifacts.Select(a=>DocumentationFixture.Hash(a.Bytes.Span)).ToArray());return;}
        var result=provider.Factor(test.Input);var original=test.Input.Artifacts.Single(a=>a.RelativePath==test.Input.HtmlPath);
        Assert.Equal("storyboards.factored.html",result.ReviewHtml.RelativePath);Assert.Equal("reconstruction-map.json",result.ReconstructionMap.RelativePath);Assert.Equal("verification.json",result.Verification.RelativePath);
        Assert.Equal(Encoding.UTF8.GetBytes(test.ExpectedView),result.ReviewHtml.Bytes.ToArray());
        var map=JsonNode.Parse(result.ReconstructionMap.Bytes.Span)!;var verify=JsonNode.Parse(result.Verification.Bytes.Span)!;
        Assert.Equal(test.Input.HtmlPath,map["original_artifact"]!.GetValue<string>());
        Assert.Equal(DocumentationFixture.Hash(original.Bytes.Span),map["original_sha256"]!.GetValue<string>());
        Assert.Equal(original.Bytes.Length,map["original_bytes"]!.GetValue<int>());
        Assert.Equal(DocumentationFixture.Hash(result.ReviewHtml.Bytes.Span),map["view_sha256"]!.GetValue<string>());
        Assert.Equal(result.ReviewHtml.Bytes.Length,map["view_bytes"]!.GetValue<int>());
        Assert.Equal(test.Replacements,map["replacements"]!.AsArray().Count);Assert.Equal(test.Retained,map["unmatched_retained_literals"]!.AsArray().Count);
        Assert.True(map["all_other_html_bytes_unchanged"]!.GetValue<bool>());Assert.False(map["frozen_inputs_modified"]!.GetValue<bool>());Assert.False(map["design_scope_reduced"]!.GetValue<bool>());Assert.Equal(0,map["model_or_network_calls"]!.GetValue<int>());
        foreach(var item in map["replacements"]!.AsArray())
        {
            Assert.Equal(KnownReference,item!["replacement_uri"]!.GetValue<string>());Assert.Equal("known.svg",item["asset"]!.GetValue<string>());
            Assert.Equal(DocumentationFixture.Hash(Encoding.UTF8.GetBytes(KnownSvg)),item["asset_sha256"]!.GetValue<string>());
            var start=item["original_start"]!.GetValue<int>();var end=item["original_end"]!.GetValue<int>();Assert.Equal(KnownUri,Encoding.ASCII.GetString(original.Bytes.Span[start..end]));Assert.Equal(DocumentationFixture.Hash(original.Bytes.Span[start..end]),item["original_uri_sha256"]!.GetValue<string>());
            Assert.Equal("RFC4648standardbase64withpadding/no-whitespace",item["encoding"]!.GetValue<string>());
        }
        foreach(var item in map["unmatched_retained_literals"]!.AsArray())
        {
            var expected=JsonNode.Parse(Correct(test).ReconstructionMap.Bytes.Span)!["unmatched_retained_literals"]![0]!;
            Assert.Equal(expected["start"]!.GetValue<int>(),item!["start"]!.GetValue<int>());Assert.Equal(expected["end"]!.GetValue<int>(),item["end"]!.GetValue<int>());Assert.Equal(expected["reason"]!.GetValue<string>(),item["reason"]!.GetValue<string>());
        }
        Assert.True(verify["round_trip_byte_identical"]!.GetValue<bool>());Assert.Equal(test.Embedded,verify["embedded_svg_instances"]!.GetValue<int>());Assert.Equal(test.Replacements,verify["replaced_known_instances"]!.GetValue<int>());Assert.Equal(test.Replacements>0?1:0,verify["distinct_assets"]!.GetValue<int>());Assert.Equal(0,verify["new_requirements_omitted"]!.GetValue<int>());
        Assert.Equal(DocumentationFixture.Hash(original.Bytes.Span),verify["original_sha256"]!.GetValue<string>());Assert.Equal(original.Bytes.Length,verify["original_bytes"]!.GetValue<int>());Assert.Equal(result.ReviewHtml.Bytes.Length,verify["review_view_bytes"]!.GetValue<int>());Assert.Equal(original.Bytes.Length-result.ReviewHtml.Bytes.Length,verify["bytes_represented_by_exact_asset_references"]!.GetValue<int>());
        Assert.Equal(original.Bytes.ToArray(),provider.Reconstruct(test.Input,result.ReviewHtml,result.ReconstructionMap).Bytes.ToArray());
        Assert.Equal(before,test.Input.Artifacts.Select(a=>DocumentationFixture.Hash(a.Bytes.Span)).ToArray());
    }
}

public sealed class ScriptedLosslessEvidence(FactoredEvidence? response,string error="",Artifact? restored=null,bool mutateFactorInput=false) : ILosslessEvidenceService
{
    public FrozenEvidenceInput? Received{get;private set;}
    public FactoredEvidence Factor(FrozenEvidenceInput input){Received=input;if(mutateFactorInput){var array=(Artifact[])input.Artifacts;array[0]=array[0] with{Bytes=Encoding.UTF8.GetBytes("unexpected mutation")};}if(error.Length>0)throw new EvidenceIntegrityException(error,"Scripted original integrity failure");return response!;}
    public Artifact Reconstruct(FrozenEvidenceInput input,Artifact reviewHtml,Artifact reconstructionMap)=>restored??input.Artifacts.Single(a=>a.RelativePath==input.HtmlPath);
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class LosslessEvidenceMockTests
{
    [Theory,MemberData(nameof(LosslessContractCases.Cases),MemberType=typeof(LosslessContractCases))]
    public void CorrectOriginalAndBoundaryMocksPass(LosslessCase test){var mock=new ScriptedLosslessEvidence(test.ExpectedError.Length==0?LosslessContractCases.Correct(test):null,test.ExpectedError);LosslessContractCases.AssertContract(test,mock);Assert.Same(test.Input,mock.Received);}

    [Theory,MemberData(nameof(LosslessContractCases.Cases),MemberType=typeof(LosslessContractCases))]
    public void WrongIntegrityOutcomeIsRejected(LosslessCase test){var wrong=test.ExpectedError.Length>0?new ScriptedLosslessEvidence(null,"WRONG_ERROR"):new ScriptedLosslessEvidence(null,"UNEXPECTED_ERROR");Assert.NotNull(Record.Exception(()=>LosslessContractCases.AssertContract(test,wrong)));}

    [Theory,MemberData(nameof(LosslessContractCases.Cases),MemberType=typeof(LosslessContractCases))]
    public void FactorInputMutationIsRejectedOnSuccessAndFailure(LosslessCase test)
    {
        var originals=test.Input.Artifacts.ToArray();
        try{Assert.NotNull(Record.Exception(()=>LosslessContractCases.AssertContract(test,new ScriptedLosslessEvidence(test.ExpectedError.Length==0?LosslessContractCases.Correct(test):null,test.ExpectedError,mutateFactorInput:true))));}
        finally{var array=(Artifact[])test.Input.Artifacts;for(var i=0;i<array.Length;i++)array[i]=originals[i];}
    }

    [Fact]
    public void NoncanonicalFixtureDecodesToKnownBytesButFailsExactRfcSpelling()
    {
        var fixture=LosslessContractCases.Create().Single(c=>c.Id=="FACTOR-NONCANONICAL-PADDING");
        var html=fixture.Input.Artifacts.Single(a=>a.RelativePath==fixture.Input.HtmlPath);var encoded=Encoding.UTF8.GetString(html.Bytes.Span)["data:image/svg+xml;base64,".Length..];
        var decoded=Convert.FromBase64String(encoded);Assert.Equal(fixture.Input.Artifacts.Single(a=>a.RelativePath=="known.svg").Bytes.ToArray(),decoded);Assert.NotEqual(encoded,Convert.ToBase64String(decoded));
    }

    [Fact]
    public void EveryMetadataAndByteAssertionRejectsItsOwnIncorrectResponse()
    {
        var test=LosslessContractCases.Create().Single(c=>c.Id=="FACTOR-ORIGINAL-UTF8-REPEATED-UNKNOWN");var correct=LosslessContractCases.Correct(test);
        void Reject(FactoredEvidence response,Artifact? restored=null)=>Assert.NotNull(Record.Exception(()=>LosslessContractCases.AssertContract(test,new ScriptedLosslessEvidence(response,restored:restored))));
        Reject(correct with{ReviewHtml=LosslessContractCases.File(correct.ReviewHtml.RelativePath,"changed")});
        foreach(var field in new[]{"original_artifact","original_sha256","view_sha256"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map[field]="wrong";Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"original_bytes","view_bytes","model_or_network_calls"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map[field]= -1;Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"all_other_html_bytes_unchanged","frozen_inputs_modified","design_scope_reduced"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map[field]=!map[field]!.GetValue<bool>();Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"replacements","unmatched_retained_literals"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map[field]!.AsArray().RemoveAt(0);Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"replacement_uri","asset","asset_sha256","original_uri_sha256","encoding"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map["replacements"]![0]![field]="wrong";Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"original_start","original_end"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map["replacements"]![0]![field]=0;Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"start","end","reason"}){var map=JsonNode.Parse(correct.ReconstructionMap.Bytes.Span)!;map["unmatched_retained_literals"]![0]![field]=field=="reason"?JsonValue.Create("wrong"):JsonValue.Create(-1);Reject(correct with{ReconstructionMap=LosslessContractCases.File("reconstruction-map.json",map.ToJsonString())});}
        foreach(var field in new[]{"embedded_svg_instances","replaced_known_instances","distinct_assets","new_requirements_omitted","original_bytes","review_view_bytes","bytes_represented_by_exact_asset_references"}){var verification=JsonNode.Parse(correct.Verification.Bytes.Span)!;verification[field]= -1;Reject(correct with{Verification=LosslessContractCases.File("verification.json",verification.ToJsonString())});}
        var wrongOriginalHash=JsonNode.Parse(correct.Verification.Bytes.Span)!;wrongOriginalHash["original_sha256"]="wrong";Reject(correct with{Verification=LosslessContractCases.File("verification.json",wrongOriginalHash.ToJsonString())});
        Reject(correct with{ReviewHtml=correct.ReviewHtml with{RelativePath="wrong"}});Reject(correct with{ReconstructionMap=correct.ReconstructionMap with{RelativePath="wrong"}});Reject(correct with{Verification=correct.Verification with{RelativePath="wrong"}});
        var wrongRoundtrip=JsonNode.Parse(correct.Verification.Bytes.Span)!;wrongRoundtrip["round_trip_byte_identical"]=false;Reject(correct with{Verification=LosslessContractCases.File("verification.json",wrongRoundtrip.ToJsonString())});
        Reject(correct,LosslessContractCases.File("page.html","not original bytes"));
    }
}
