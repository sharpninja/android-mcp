using System.Text;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record ReceiptPrivacyCase(string Id,byte[] Bytes,string Error,long Events,long Invalid,string Categories);
public sealed record ReceiptPrivacyObservation(string Error,long Events,long Invalid,string Categories,bool OriginalRetained,bool ValuesExported);
public static class ReviewReceiptPrivacyCases
{
    private static ReceiptPrivacyCase Row(string id,string text,long events=1,long invalid=0,string categories="")=>new(id,Encoding.UTF8.GetBytes(text),"",events,invalid,categories);
    public static IReadOnlyList<ReceiptPrivacyCase> All {get;}=[
        Row("empty","",0),Row("plain","{}\n"),
        Row("nested-hidden","{\"a\":[{\"type\":\"thinking\"},{\"x\":{\"type\":\"redacted_thinking\"}}]}",categories:"hidden_reasoning_block=2"),
        Row("ignored-hidden-deltas","[{\"type\":\"thinking_delta\"},{\"type\":\"signature_delta\"}]"),
        Row("nested-quota","{\"a\":[{\"type\":\"rate_limit_event\"},{\"type\":\"rate_limit_event\"}]}",categories:"account_quota_event=2"),
        Row("all-init-fields","{\"subtype\":\"init\",\"plugins\":null,\"slash_commands\":[],\"terminal_slash_commands\":0,\"skills\":\"\",\"agents\":false,\"messaging_socket_path\":{}}",categories:"unrelated_runtime_configuration_field=6"),
        Row("non-init-fields","{\"subtype\":\"other\",\"plugins\":[],\"skills\":[]}"),
        Row("nested-init-fields","{\"a\":[{\"subtype\":\"init\",\"skills\":[]},{\"x\":{\"subtype\":\"init\",\"agents\":[]}}]}",categories:"unrelated_runtime_configuration_field=2"),
        Row("string-mentions","{\"text\":\"thinking redacted_thinking rate_limit_event plugins\"}"),
        Row("multiple-signed-one-line","{\"a\":\"https://fixture.test?p=0&token=PRIVATE_FIXTURE_VALUE\",\"b\":\"HTTPS://fixture.test?sig=PRIVATE_FIXTURE_VALUE\"}",categories:"signed_transfer_url=1"),
        Row("signed-two-lines","\"https://fixture.test?signature=PRIVATE_FIXTURE_VALUE\"\n\"https://fixture.test?X-Amz-Signature=PRIVATE_FIXTURE_VALUE\"\n",2,categories:"signed_transfer_url=2"),
        Row("signed-empty-value","\"https://fixture.test?token=\""),
        Row("signed-invalid-json","https://fixture.test?X-Goog-Signature=PRIVATE_FIXTURE_VALUE",invalid:1,categories:"signed_transfer_url=1"),
        Row("escaped-url","\"https:\\/\\/fixture.test?token=PRIVATE_FIXTURE_VALUE\""),
        Row("signed-unicode-case","\"httpſ://fixture.test?sig=PRIVATE_FIXTURE_VALUE\"\n\"https://fixture.test?sıgnature=PRIVATE_FIXTURE_VALUE\"\n\"https://fixture.test?sİgnature=PRIVATE_FIXTURE_VALUE\"\n\"https://fixture.test?toKen=PRIVATE_FIXTURE_VALUE\"",4,categories:"signed_transfer_url=4"),
        Row("signed-control-whitespace","https://fixture.test?\u001ctoken=PRIVATE_FIXTURE_VALUE",invalid:1),
        Row("bearer20","Bearer ghp_ABCDEFGHIJKLMNOPQRST",invalid:1,categories:"bearer_secret=1"),
        Row("bearer19","Bearer ghp_ABCDEFGHIJKLMNOPQRS",invalid:1),
        Row("bearer-case","bearer ghp_ABCDEFGHIJKLMNOPQRST",invalid:1),
        Row("both-raw-categories","Bearer sk-ant-ABCDEFGHIJKLMNOPQRST https://fixture.test?token=PRIVATE_FIXTURE_VALUE",invalid:1,categories:"bearer_secret=1|signed_transfer_url=1"),
        Row("malformed","{",invalid:1),Row("blank-lines","\n{}\n\n",3,2),
        Row("universal-newlines","{\"type\":\"rate_limit_event\"}\r\n{}\r{\"type\":\"thinking\"}\n",3,categories:"account_quota_event=1|hidden_reasoning_block=1"),
        Row("duplicate-native-boundary","{\"type\":\"thinking\",\"type\":\"rate_limit_event\"}",invalid:1),
        Row("nonfinite-native-boundary","{\"x\":NaN}",invalid:1),
        Row("surrogate-native-boundary","{\"x\":\"\\ud800\"}",invalid:1),
        Row("scalar-and-array-roots","null\n[{\"type\":\"thinking\"}]\n3\n",3,categories:"hidden_reasoning_block=1"),
        Row("wrong-property-types","{\"type\":1,\"subtype\":false,\"plugins\":[]}"),
        Row("mixed-lines","garbage\n{\"type\":\"thinking\"}\nhttps://fixture.test?sig=PRIVATE_FIXTURE_VALUE\n",3,2,"hidden_reasoning_block=1|signed_transfer_url=1"),
        Row("case-sensitive-fields","{\"type\":\"Thinking\",\"subtype\":\"INIT\",\"Plugins\":[]}"),
        new("invalid-utf8",[255],"INVALID_REVIEW_RECEIPT",0,0,"")
    ];
    public static IEnumerable<object[]> Data=>All.Select(x=>new object[]{x.Id});
    public static ReceiptPrivacyCase Get(string id)=>All.Single(x=>x.Id==id);
    public static ReceiptPrivacyObservation Expected(string id){var c=Get(id);return new(c.Error,c.Events,c.Invalid,c.Categories,true,false);}
    public static void AssertContract(string id,ReceiptPrivacyObservation actual)
    {var e=Expected(id);Assert.Equal(e.Error,actual.Error);Assert.Equal(e.Events,actual.Events);Assert.Equal(e.Invalid,actual.Invalid);Assert.Equal(e.Categories,actual.Categories);Assert.True(actual.OriginalRetained);Assert.False(actual.ValuesExported);}
}
public sealed class ReviewReceiptPrivacyMockTests
{
    [Theory,MemberData(nameof(ReviewReceiptPrivacyCases.Data),MemberType=typeof(ReviewReceiptPrivacyCases)),Trait("Gate","Mock")]
    public void Exact_original_categories_and_native_boundaries_reject_each_wrong_response(string id)
    {
        var good=ReviewReceiptPrivacyCases.Expected(id);ReviewReceiptPrivacyCases.AssertContract(id,good);
        foreach(var wrong in new[]{good with{Error="WRONG"},good with{Events=good.Events+1},good with{Invalid=good.Invalid+1},good with{Categories=good.Categories+"wrong"},good with{OriginalRetained=false},good with{ValuesExported=true}})
            Assert.ThrowsAny<Exception>(()=>ReviewReceiptPrivacyCases.AssertContract(id,wrong));
    }
}
public sealed record ReceiptPrivacySelectionObservation(string Code,bool HasPin,bool HasCounts,int Opens,int Observations,bool OriginalRetained,bool ValuesExported);
public static class ReviewReceiptPrivacySelectionCases
{
    public static string[] Names=>["stable","stable-unflagged","mtime-change","size-change","identity-change","missing-before","private-path","invalid-path","missing-after","wrong-pin-hash","wrong-pin-length","cancellation","unsupported-metadata"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static ReceiptPrivacySelectionObservation Expected(string id)=>id switch
    {
        "stable"=>new("original_preserved; publication filtering required",true,true,1,2,true,false),
        "stable-unflagged"=>new("no flagged structured fields; still subject to final scope review",true,true,1,2,true,false),
        "mtime-change" or "size-change" or "identity-change"=>new("changed_during_scan_not_sealed",false,false,1,2,true,false),
        "missing-before"=>new("REVIEW_RECEIPT_MISSING",false,false,1,0,true,false),
        "private-path"=>new("PRIVATE_PAYLOAD",false,false,0,0,true,false),
        "invalid-path"=>new("INVALID_PATH",false,false,0,0,true,false),
        "missing-after"=>new("REVIEW_RECEIPT_MISSING",false,false,1,2,true,false),
        "wrong-pin-hash"=>new("ARTIFACT_PIN_HASH",false,false,1,1,true,false),
        "wrong-pin-length"=>new("ARTIFACT_PIN_LENGTH",false,false,1,1,true,false),
        "cancellation"=>new("cancelled",false,false,0,0,true,false),
        "unsupported-metadata"=>new("unsupported",false,false,1,0,true,false),
        _=>throw new ArgumentException("Unknown selected-file contract")
    };
    public static void AssertContract(string id,ReceiptPrivacySelectionObservation actual)
    {var e=Expected(id);Assert.Equal(e.Code,actual.Code);Assert.Equal(e.HasPin,actual.HasPin);Assert.Equal(e.HasCounts,actual.HasCounts);Assert.Equal(e.Opens,actual.Opens);Assert.Equal(e.Observations,actual.Observations);Assert.True(actual.OriginalRetained);Assert.False(actual.ValuesExported);}
}
public sealed class ReviewReceiptPrivacySelectionMockTests
{
    [Theory,MemberData(nameof(ReviewReceiptPrivacySelectionCases.Data),MemberType=typeof(ReviewReceiptPrivacySelectionCases)),Trait("Gate","Mock")]
    public void Selected_file_scope_pin_status_metadata_and_no_values_contract_is_discriminating(string id)
    {
        var good=ReviewReceiptPrivacySelectionCases.Expected(id);ReviewReceiptPrivacySelectionCases.AssertContract(id,good);
        foreach(var wrong in new[]{good with{Code="WRONG"},good with{HasPin=!good.HasPin},good with{HasCounts=!good.HasCounts},good with{Opens=good.Opens+1},good with{Observations=good.Observations+1},good with{OriginalRetained=false},good with{ValuesExported=true}})
            Assert.ThrowsAny<Exception>(()=>ReviewReceiptPrivacySelectionCases.AssertContract(id,wrong));
    }
}
