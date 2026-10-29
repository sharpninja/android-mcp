using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record MetadataProofCase(string Id,string? Change=null,string? Error=null,int Reads=2,int Versions=1,bool Generic=false,int Repeats=1);
public sealed record MetadataProofObservation(string? Error,long? Size,string? ETag,string? Platform,string? Package,
    IReadOnlyList<string> Reads,int Versions,bool SourcePinsPreserved,bool InputsRetained,bool LocalIntegrityOnly,int Proofs,int Writes,int Network,bool WholeArchiveIdentityClaimed=false);
public interface IMetadataProofProbe { Task<MetadataProofObservation> RunAsync(MetadataProofCase test); }
public static class MetadataProofCases
{
    public const string Receipt="cache-index-integrated009v3/sdk37.2/platform-metadata/metadata-range-receipt.json";
    public const string Xml="repository2-3.xml";
    public static IEnumerable<object[]> All=>new[]{
        new MetadataProofCase("original-reference-and-default"),
        new MetadataProofCase("row-platform-wins",Change:"row-wins"),
        new MetadataProofCase("transport-platform-fallback",Change:"transport-platform"),
        new MetadataProofCase("empty-row-platform-fallback",Change:"empty-row-platform"),
        new MetadataProofCase("native-platform-spelling-not-substituted",Change:"wrong-platform-key",Error:"SDK_PLATFORM",Reads:1,Versions:0),
        new MetadataProofCase("missing-platform-after-receipt",Change:"missing-platform",Error:"SDK_PLATFORM",Reads:1,Versions:0),
        new MetadataProofCase("explicit-package",Change:"package"),
        new MetadataProofCase("null-package-not-default",Change:"null-package",Error:"SDK_PACKAGE"),
        new MetadataProofCase("number-package-not-default",Change:"number-package",Error:"SDK_PACKAGE"),
        new MetadataProofCase("missing-receipt-ref",Change:"missing-reference",Error:"METADATA_REFERENCE",Reads:0,Versions:0),
        new MetadataProofCase("null-receipt-ref",Change:"null-reference",Error:"METADATA_REFERENCE",Reads:0,Versions:0),
        new MetadataProofCase("receipt-path-escape",Change:"escape",Error:"PATH",Reads:0,Versions:0),
        new MetadataProofCase("receipt-pin-required",Change:"missing-receipt-pin",Error:"METADATA_PIN_REQUIRED",Reads:0,Versions:0),
        new MetadataProofCase("receipt-size-pin",Change:"receipt-size",Error:"ARTIFACT_PIN_LENGTH",Reads:1,Versions:0),
        new MetadataProofCase("receipt-hash-pin",Change:"receipt-hash",Error:"ARTIFACT_PIN_HASH",Reads:1,Versions:0),
        new MetadataProofCase("missing-receipt-file",Change:"missing-receipt",Error:"NOT_FOUND",Reads:1,Versions:0),
        new MetadataProofCase("malformed-receipt",Change:"bad-json",Error:"METADATA_RECEIPT_JSON",Reads:1,Versions:0),
        new MetadataProofCase("array-receipt",Change:"array-json",Error:"METADATA_RECEIPT_JSON",Reads:1,Versions:0),
        new MetadataProofCase("missing-etag-first",Change:"missing-etag",Error:"ETAG",Reads:1,Versions:0),
        new MetadataProofCase("null-etag-after-xml",Change:"null-etag",Error:"ETAG"),
        new MetadataProofCase("blank-etag-after-xml",Change:"blank-etag",Error:"ETAG"),
        new MetadataProofCase("xml-pin-required-after-receipt",Change:"missing-xml-pin",Error:"METADATA_PIN_REQUIRED",Reads:1,Versions:0),
        new MetadataProofCase("xml-hash-pin",Change:"xml-hash",Error:"ARTIFACT_PIN_HASH",Versions:0),
        new MetadataProofCase("missing-xml-file",Change:"missing-xml",Error:"NOT_FOUND",Versions:0),
        new MetadataProofCase("malformed-xml",Change:"bad-xml",Error:"SDK_XML"),
        new MetadataProofCase("bad-xml-before-null-package",Change:"bad-xml-null-package",Error:"SDK_XML"),
        new MetadataProofCase("wrong-package",Change:"wrong-package",Error:"SDK_PACKAGE"),
        new MetadataProofCase("size-disagreement",Change:"expected-size",Error:"SDK_SIZE"),
        new MetadataProofCase("proof-bytes-reused",Change:"reuse",Repeats:2,Versions:2),
        new MetadataProofCase("local-pin-does-not-prove-official",Change:"official-field"),
        new MetadataProofCase("inline-proof-does-not-replace-reference",Change:"inline"),
        new MetadataProofCase("generic-explicit-bypass",Change:"bad-json",Reads:0,Versions:0,Generic:true),
        new MetadataProofCase("bounded-metadata-never-promotes-declared-container-hash",Change:"declared-hash")
    }.Select(value=>new object[]{value});
    public static MetadataProofObservation Expected(MetadataProofCase test)
    {
        var bound=test.Error is null&&!test.Generic;
        return new(test.Error,bound?170000:null,bound?"\"one\"":null,bound?"37.2":null,bound&&test.Change=="package"?"platforms;android-37.2":null,
            test.Reads==0?[]:test.Reads==1?[Receipt]:[Receipt,Xml],test.Versions,true,true,true,bound?test.Repeats:0,0,0);
    }
    public static async Task AssertCase(MetadataProofCase test,IMetadataProofProbe probe)
    {
        var expected=Expected(test);var actual=await probe.RunAsync(test);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.Size,actual.Size);Assert.Equal(expected.ETag,actual.ETag);
        Assert.Equal(expected.Platform,actual.Platform);Assert.Equal(expected.Package,actual.Package);Assert.Equal(expected.Reads,actual.Reads);
        Assert.Equal(expected.Versions,actual.Versions);Assert.True(actual.SourcePinsPreserved);Assert.True(actual.InputsRetained);Assert.True(actual.LocalIntegrityOnly);
        Assert.Equal(expected.Proofs,actual.Proofs);Assert.Equal(0,actual.Writes);Assert.Equal(0,actual.Network);
        Assert.False(actual.WholeArchiveIdentityClaimed);
    }
}
public sealed class ScriptedMetadataProofProbe(string? mutation=null):IMetadataProofProbe
{
    public Task<MetadataProofObservation> RunAsync(MetadataProofCase test)
    {
        var value=MetadataProofCases.Expected(test);
        return Task.FromResult(mutation switch{
            "error"=>value with{Error="WRONG"},"size"=>value with{Size=99},"etag"=>value with{ETag="WRONG"},
            "platform"=>value with{Platform="WRONG"},"package"=>value with{Package="WRONG"},"reads"=>value with{Reads=["WRONG"]},
            "versions"=>value with{Versions=99},"source"=>value with{SourcePinsPreserved=false},"input"=>value with{InputsRetained=false},
            "authority"=>value with{LocalIntegrityOnly=false},"proofs"=>value with{Proofs=99},"writes"=>value with{Writes=1},"network"=>value with{Network=1},"whole"=>value with{WholeArchiveIdentityClaimed=true},_=>value});
    }
}
[Trait("Gate","Mock")]
public sealed class MetadataProofMockTests
{
    [Theory,MemberData(nameof(MetadataProofCases.All),MemberType=typeof(MetadataProofCases))]
    public Task CorrectOriginalProofInputCases(MetadataProofCase test)=>MetadataProofCases.AssertCase(test,new ScriptedMetadataProofProbe());
    [Theory,MemberData(nameof(MetadataProofCases.All),MemberType=typeof(MetadataProofCases))]
    public async Task EveryInputOrderResultAndEffectControlDiscriminates(MetadataProofCase test)
    {
        foreach(var mutation in new[]{"error","size","etag","platform","package","reads","versions","source","input","authority","proofs","writes","network","whole"})
            Assert.NotNull(await Record.ExceptionAsync(()=>MetadataProofCases.AssertCase(test,new ScriptedMetadataProofProbe(mutation))));
    }
}
