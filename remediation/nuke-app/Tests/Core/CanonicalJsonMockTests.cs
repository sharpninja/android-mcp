using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record CanonicalJsonCase(string Id, string Raw, string? Expected, string? Error = null,
    bool ElementInput = true, bool Undefined = false, byte[]? RawBytes = null);

public static class CanonicalJsonCases
{
    // These expected bytes are specified independently, including the original
    // metadata Unicode/numeric vector. No legacy executable produced them.
    public static IReadOnlyList<CanonicalJsonCase> Values { get; } =
    [
        new("JSON-01-sorted-compact", " { \"b\" : 2, \"a\" : [true,null,false] } ", "{\"a\":[true,null,false],\"b\":2}"),
        new("JSON-02-nested", "{\"z\":{\"b\":1,\"a\":2},\"a\":[]}", "{\"a\":[],\"z\":{\"a\":2,\"b\":1}}"),
        new("JSON-03-integer", "1", "1"),
        new("JSON-04-float", "1.0", "1.0"),
        new("JSON-05-integer-negative-zero", "-0", "0"),
        new("JSON-06-float-negative-zero", "-0.0", "-0.0"),
        new("JSON-07-float-positive-zero", "0e4", "0.0"),
        new("JSON-08-exponent-padding", "1e-7", "1e-07"),
        new("JSON-09-exponent-upper", "1e20", "1e+20"),
        new("JSON-10-fixed-small-boundary", "1e-4", "0.0001"),
        new("JSON-11-scientific-small-boundary", "1e-5", "1e-05"),
        new("JSON-12-fixed-large-boundary", "1e15", "1000000000000000.0"),
        new("JSON-13-scientific-large-boundary", "1e16", "1e+16"),
        new("JSON-14-arbitrary-integer", "123456789012345678901234567890", "123456789012345678901234567890"),
        new("JSON-15-shortest-float", "0.10000000000000001", "0.1"),
        new("JSON-16-subnormal", "5e-324", "5e-324"),
        new("JSON-17-largest-finite", "1.7976931348623157e308", "1.7976931348623157e+308"),
        new("JSON-18-smallest-normal", "2.2250738585072014e-308", "2.2250738585072014e-308"),
        new("JSON-19-unicode-scalar-order", "{\"😀\":\"é\",\"\uE000\":\"e\u0301\"}", "{\"\uE000\":\"e\u0301\",\"😀\":\"é\"}"),
        new("JSON-20-no-normalization", "[\"é\",\"e\u0301\"]", "[\"é\",\"e\u0301\"]"),
        new("JSON-21-no-ascii-escape", "\"\\u00e9\\ud83d\\ude00\"", "\"é😀\""),
        new("JSON-22-string-escapes", "\"\\b\\t\\n\\f\\r\\u0000\\\"\\\\/\"", "\"\\b\\t\\n\\f\\r\\u0000\\\"\\\\/\""),
        new("JSON-23-original-metadata-vector", "{\"😀\":\"é\",\"\uE000\":\"e\u0301\",\"z\":[-0.0,1.0,1,1e-7,1e20,9223372036854775807]}", "{\"z\":[-0.0,1.0,1,1e-07,1e+20,9223372036854775807],\"\uE000\":\"e\u0301\",\"😀\":\"é\"}"),
        new("JSON-24-duplicate", "{\"a\":1,\"a\":2}", null, CanonicalJsonErrors.DuplicateProperty),
        new("JSON-25-escaped-duplicate", "{\"a\":1,\"\\u0061\":2}", null, CanonicalJsonErrors.DuplicateProperty),
        new("JSON-26-nested-duplicate", "{\"x\":[{\"a\":1,\"a\":2}]}", null, CanonicalJsonErrors.DuplicateProperty),
        new("JSON-27-overflow", "1e999", null, CanonicalJsonErrors.NonfiniteNumber),
        new("JSON-28-unpaired-surrogate", "\"\\ud800\"", null, CanonicalJsonErrors.InvalidUnicode),
        new("JSON-29-leading-zero", "01", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-30-trailing-comma", "[1,]", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-31-nan", "NaN", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-32-empty", "", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-33-two-values", "true false", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-34-comment", "/* x */null", null, CanonicalJsonErrors.InvalidJson, false),
        new("JSON-35-undefined-element", "", null, CanonicalJsonErrors.UndefinedValue, Undefined:true),
        new("JSON-36-invalid-utf8", "", null, CanonicalJsonErrors.InvalidUnicode, false, RawBytes:[0x22,0xff,0x22])
    ];
    public static IEnumerable<object[]> All => Values.SelectMany(x=>new[]{false,true}
        .Where(element=>x.Undefined ? element : !element || x.ElementInput).Select(element=>new object[]{x,element}));
    public static string ExpectedHash(CanonicalJsonCase test) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(test.Expected!)));

    public static void AssertCase(CanonicalJsonCase test, bool elementInput, ICanonicalJson provider)
    {
        var raw=test.RawBytes ?? Encoding.UTF8.GetBytes(test.Raw);
        using var document=elementInput && !test.Undefined ? JsonDocument.Parse(raw) : null;
        var element=document?.RootElement ?? default;
        byte[] Serialize() => elementInput ? provider.Serialize(element) : provider.Serialize(raw);
        string Hash() => elementInput ? provider.Hash(element) : provider.Hash(raw);
        if(test.Error is not null)
        {
            Assert.Equal(test.Error,Assert.Throws<CanonicalJsonException>(()=>Serialize()).Code);
            Assert.Equal(test.Error,Assert.Throws<CanonicalJsonException>(()=>Hash()).Code);
        }
        else
        {
            Assert.Equal(Encoding.UTF8.GetBytes(test.Expected!),Serialize());
            Assert.Equal(ExpectedHash(test),Hash());
        }
    }
}

public sealed class ScriptedCanonicalJson(CanonicalJsonCase expected,string? wrongBytes=null,string? wrongHash=null,string? wrongError=null,bool omitError=false):ICanonicalJson
{
    public int Calls { get; private set; }
    private void CheckError()
    {
        Calls++;
        if(expected.Error is not null && !omitError) throw new CanonicalJsonException(wrongError ?? expected.Error,"Scripted error");
    }
    private void Check(ReadOnlyMemory<byte> raw)
    {
        Assert.Equal(expected.RawBytes ?? Encoding.UTF8.GetBytes(expected.Raw),raw.ToArray());CheckError();
    }
    private void Check(JsonElement value)
    {
        if(expected.Undefined) Assert.Equal(JsonValueKind.Undefined,value.ValueKind);
        else Assert.Equal(expected.Raw.Trim(),value.GetRawText().Trim());
        CheckError();
    }
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8) { Check(rawUtf8);return Encoding.UTF8.GetBytes(wrongBytes ?? expected.Expected ?? "null"); }
    public byte[] Serialize(JsonElement value) { Check(value);return Encoding.UTF8.GetBytes(wrongBytes ?? expected.Expected ?? "null"); }
    public string Hash(ReadOnlyMemory<byte> rawUtf8) { Check(rawUtf8);return wrongHash ?? (expected.Expected is null ? "wrong" : CanonicalJsonCases.ExpectedHash(expected)); }
    public string Hash(JsonElement value) { Check(value);return wrongHash ?? (expected.Expected is null ? "wrong" : CanonicalJsonCases.ExpectedHash(expected)); }
}

[Trait("Gate","Mock")]
public sealed class CanonicalJsonMockTests
{
    [Theory,MemberData(nameof(CanonicalJsonCases.All),MemberType=typeof(CanonicalJsonCases))]
    public void CorrectResponsePasses(CanonicalJsonCase test,bool elementInput)
    {
        var fake=new ScriptedCanonicalJson(test);CanonicalJsonCases.AssertCase(test,elementInput,fake);Assert.Equal(2,fake.Calls);
    }
    [Theory,MemberData(nameof(CanonicalJsonCases.All),MemberType=typeof(CanonicalJsonCases))]
    public void WrongBytesHashOrErrorRejectedIndividually(CanonicalJsonCase test,bool elementInput)
    {
        if(test.Error is null)
        {
            Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,new ScriptedCanonicalJson(test,wrongBytes:"[\"wrong\"]"))));
            Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,new ScriptedCanonicalJson(test,wrongHash:new string('0',64)))));
        }
        else
        {
            Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,new ScriptedCanonicalJson(test,wrongError:"WRONG_CODE"))));
            Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,new ScriptedCanonicalJson(test,omitError:true))));
        }
    }
}

[Trait("Gate","Real")]
public sealed class CanonicalJsonRealTests
{
    internal static ICanonicalJson Native()=>new CanonicalJson();
    [Theory,MemberData(nameof(CanonicalJsonCases.All),MemberType=typeof(CanonicalJsonCases))]
    public void NativeCanonicalizerUsesSameCasesAndAssertions(CanonicalJsonCase test,bool elementInput)=>
        CanonicalJsonCases.AssertCase(test,elementInput,Native());
}

public sealed record ProfiledCanonicalCase(CanonicalJsonCase Value,JsonSerializationProfile Profile);
public static class ProfiledCanonicalCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var data in CanonicalJsonCases.All)
            {
                var value=(CanonicalJsonCase)data[0];var element=(bool)data[1];
                yield return [new ProfiledCanonicalCase(value,JsonSerializationProfile.Utf8Canonical),element];
                var legacy=value.Id switch
                {
                    "JSON-19-unicode-scalar-order"=>value with{Expected="{\"\\ue000\":\"e\\u0301\",\"\\ud83d\\ude00\":\"\\u00e9\"}"},
                    "JSON-20-no-normalization"=>value with{Expected="[\"\\u00e9\",\"e\\u0301\"]"},
                    "JSON-21-no-ascii-escape"=>value with{Expected="\"\\u00e9\\ud83d\\ude00\""},
                    "JSON-23-original-metadata-vector"=>value with{Expected="{\"z\":[-0.0,1.0,1,1e-07,1e+20,9223372036854775807],\"\\ue000\":\"e\\u0301\",\"\\ud83d\\ude00\":\"\\u00e9\"}"},
                    _=>value
                };
                yield return [new ProfiledCanonicalCase(legacy,JsonSerializationProfile.LegacyAsciiIdentity),element];
            }
            var binding=new string('a',64);
            var extras=new[]
            {
                new CanonicalJsonCase("PROFILE-DEL","\"\u007f~\u0080\"","\"\u007f~\u0080\""),
                new CanonicalJsonCase("PROFILE-LINE-SEPARATORS","\"\u2028\u2029\"","\"\u2028\u2029\""),
                new CanonicalJsonCase("PROFILE-CALLBACK-ID","{\"type_spelling\":\"a.É😀Listener\",\"parameter_index\":0,\"type_id\":null,\"binding_id\":\""+binding+"\"}",
                    "{\"binding_id\":\""+binding+"\",\"parameter_index\":0,\"type_id\":null,\"type_spelling\":\"a.É😀Listener\"}")
            };
            foreach(var value in extras)
            foreach(var element in new[]{false,true})
            {
                yield return [new ProfiledCanonicalCase(value,JsonSerializationProfile.Utf8Canonical),element];
                var legacy=value.Id switch
                {
                    "PROFILE-DEL"=>value with{Expected="\"\\u007f~\\u0080\""},
                    "PROFILE-LINE-SEPARATORS"=>value with{Expected="\"\\u2028\\u2029\""},
                    _=>value with{Expected="{\"binding_id\":\""+binding+"\",\"parameter_index\":0,\"type_id\":null,\"type_spelling\":\"a.\\u00c9\\ud83d\\ude00Listener\"}"}
                };
                yield return [new ProfiledCanonicalCase(legacy,JsonSerializationProfile.LegacyAsciiIdentity),element];
            }
        }
    }
}
public sealed class ProfileBoundCanonicalJson(IProfiledCanonicalJson provider,JsonSerializationProfile profile):ICanonicalJson
{
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)=>provider.Serialize(rawUtf8,profile);
    public byte[] Serialize(JsonElement value)=>provider.Serialize(value,profile);
    public string Hash(ReadOnlyMemory<byte> rawUtf8)=>provider.Hash(rawUtf8,profile);
    public string Hash(JsonElement value)=>provider.Hash(value,profile);
}
public sealed class ScriptedProfiledCanonicalJson(ProfiledCanonicalCase expected,string? wrongBytes=null,string? wrongHash=null,
    string? wrongError=null,bool omitError=false,bool rejectUnknown=true):IProfiledCanonicalJson
{
    private readonly ScriptedCanonicalJson scripted=new(expected.Value,wrongBytes,wrongHash,wrongError,omitError);
    public int Calls=>scripted.Calls;
    private void Profile(JsonSerializationProfile profile)
    {
        if(rejectUnknown && profile is not (JsonSerializationProfile.Utf8Canonical or JsonSerializationProfile.LegacyAsciiIdentity))
            throw new ArgumentOutOfRangeException(nameof(profile));
        Assert.Equal(expected.Profile,profile);
    }
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)=>Serialize(rawUtf8,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(JsonElement value)=>Serialize(value,JsonSerializationProfile.Utf8Canonical);
    public string Hash(ReadOnlyMemory<byte> rawUtf8)=>Hash(rawUtf8,JsonSerializationProfile.Utf8Canonical);
    public string Hash(JsonElement value)=>Hash(value,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile) { Profile(profile);return scripted.Serialize(rawUtf8); }
    public byte[] Serialize(JsonElement value,JsonSerializationProfile profile) { Profile(profile);return scripted.Serialize(value); }
    public string Hash(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile) { Profile(profile);return scripted.Hash(rawUtf8); }
    public string Hash(JsonElement value,JsonSerializationProfile profile) { Profile(profile);return scripted.Hash(value); }
}
[Trait("Gate","Mock")]
public sealed class ProfiledCanonicalJsonMockTests
{
    internal static void AssertUndefinedProfileRejected(IProfiledCanonicalJson provider)
    {
        var profile=(JsonSerializationProfile)42;var raw=Encoding.UTF8.GetBytes(CanonicalJsonCases.Values[0].Raw);
        using var document=JsonDocument.Parse(raw);
        Assert.Throws<ArgumentOutOfRangeException>(()=>provider.Serialize(raw,profile));
        Assert.Throws<ArgumentOutOfRangeException>(()=>provider.Serialize(document.RootElement,profile));
        Assert.Throws<ArgumentOutOfRangeException>(()=>provider.Hash(raw,profile));
        Assert.Throws<ArgumentOutOfRangeException>(()=>provider.Hash(document.RootElement,profile));
    }
    [Theory,MemberData(nameof(ProfiledCanonicalCases.All),MemberType=typeof(ProfiledCanonicalCases))]
    public void CorrectProfileResponsesPassSameAssertions(ProfiledCanonicalCase test,bool elementInput)
    {
        var fake=new ScriptedProfiledCanonicalJson(test);
        CanonicalJsonCases.AssertCase(test.Value,elementInput,new ProfileBoundCanonicalJson(fake,test.Profile));Assert.Equal(2,fake.Calls);
    }
    [Theory,MemberData(nameof(ProfiledCanonicalCases.All),MemberType=typeof(ProfiledCanonicalCases))]
    public void WrongProfileBytesHashAndErrorsRejectedIndividually(ProfiledCanonicalCase test,bool elementInput)
    {
        void Reject(ScriptedProfiledCanonicalJson fake)=>Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test.Value,elementInput,new ProfileBoundCanonicalJson(fake,test.Profile))));
        if(test.Value.Error is null)
        { Reject(new(test,wrongBytes:"wrong"));Reject(new(test,wrongHash:new string('0',64))); }
        else
        { Reject(new(test,wrongError:"WRONG_CODE"));Reject(new(test,omitError:true)); }
        var wrongProfile=test.Profile==JsonSerializationProfile.Utf8Canonical ? JsonSerializationProfile.LegacyAsciiIdentity : JsonSerializationProfile.Utf8Canonical;
        Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test.Value,elementInput,new ProfileBoundCanonicalJson(new ScriptedProfiledCanonicalJson(test),wrongProfile))));
    }
    [Fact]
    public void UndefinedProfileCannotSilentlyChooseAFormatter()
    {
        var profile=(JsonSerializationProfile)42;
        var test=new ProfiledCanonicalCase(CanonicalJsonCases.Values[0],profile);
        AssertUndefinedProfileRejected(new ScriptedProfiledCanonicalJson(test));
        Assert.NotNull(Record.Exception(()=>AssertUndefinedProfileRejected(new ScriptedProfiledCanonicalJson(test,rejectUnknown:false))));
    }
}

[Trait("Gate","Real")]
public sealed class ProfiledCanonicalJsonRealTests
{
    [Theory,MemberData(nameof(ProfiledCanonicalCases.All),MemberType=typeof(ProfiledCanonicalCases))]
    public void NativeProfilesUseSameCasesAndAssertions(ProfiledCanonicalCase test,bool elementInput)=>
        CanonicalJsonCases.AssertCase(test.Value,elementInput,new ProfileBoundCanonicalJson(new CanonicalJson(),test.Profile));
    [Fact]
    public void NativeUnknownProfileUsesSameAssertions()=>ProfiledCanonicalJsonMockTests.AssertUndefinedProfileRejected(new CanonicalJson());
}

// Append to the owned CanonicalJsonMockTests.cs only after the current compile window.
// Existing baseline/profile vectors and assertions remain byte-for-byte unchanged.
public static class MixedCanonicalJsonCases
{
    public static IReadOnlyList<CanonicalJsonCase> Values { get; } =
    [
        new("MIX-01-root-duplicate-before-nonfinite","{\"x\":1,\"n\":1e400,\"x\":2}",null,CanonicalJsonErrors.DuplicateProperty),
        new("MIX-02-sorted-earlier-child-nonfinite","{\"z\":{\"x\":1,\"x\":2},\"a\":1e400}",null,CanonicalJsonErrors.NonfiniteNumber),
        new("MIX-03-sorted-earlier-child-duplicate","{\"z\":1e400,\"a\":{\"x\":1,\"x\":2}}",null,CanonicalJsonErrors.DuplicateProperty),
        new("MIX-04-escaped-root-duplicate-before-child-unicode","{\"bad\":\"\\ud800\",\"x\":1,\"\\u0078\":2}",null,CanonicalJsonErrors.DuplicateProperty),
        new("MIX-05-own-name-unicode-before-own-duplicate","{\"\\ud800\":1,\"x\":1,\"x\":2}",null,CanonicalJsonErrors.InvalidUnicode),
        new("MIX-06-own-duplicate-before-later-own-name-unicode","{\"x\":1,\"x\":2,\"\\ud800\":1}",null,CanonicalJsonErrors.DuplicateProperty),
        new("MIX-07-array-index-nonfinite-first","[1e400,{\"x\":1,\"x\":2}]",null,CanonicalJsonErrors.NonfiniteNumber),
        new("MIX-08-array-index-duplicate-first","[{\"x\":1,\"x\":2},1e400]",null,CanonicalJsonErrors.DuplicateProperty),
        new("MIX-09-unicode-scalar-child-order","{\"😀\":{\"x\":1,\"x\":2},\"\uE000\":1e400}",null,CanonicalJsonErrors.NonfiniteNumber),
        new("MIX-10-own-late-name-unicode-before-child-nonfinite","{\"a\":1e400,\"\\ud800\":1}",null,CanonicalJsonErrors.InvalidUnicode),
        new("MIX-11-child-own-escaped-duplicate-before-its-values","{\"a\":{\"bad\":\"\\ud800\",\"x\":1,\"\\u0078\":2}}",null,CanonicalJsonErrors.DuplicateProperty)
    ];
    public static IEnumerable<object[]> All=>Values.SelectMany(value=>new[]{false,true}.SelectMany(element=>
        new JsonSerializationProfile?[]{null,JsonSerializationProfile.Utf8Canonical,JsonSerializationProfile.LegacyAsciiIdentity}
            .Select(profile=>new object[]{value,element,profile!})));
}
[Trait("Gate","Mock")]
public sealed class MixedCanonicalJsonMockTests
{
    [Theory,MemberData(nameof(MixedCanonicalJsonCases.All),MemberType=typeof(MixedCanonicalJsonCases))]
    public void CorrectPrecedencePasses(CanonicalJsonCase test,bool elementInput,JsonSerializationProfile? profile)
    {
        if(profile is null)
        {
            var fake=new ScriptedCanonicalJson(test);CanonicalJsonCases.AssertCase(test,elementInput,fake);Assert.Equal(2,fake.Calls);
        }
        else
        {
            var fake=new ScriptedProfiledCanonicalJson(new(test,profile.Value));
            CanonicalJsonCases.AssertCase(test,elementInput,new ProfileBoundCanonicalJson(fake,profile.Value));Assert.Equal(2,fake.Calls);
        }
    }
    [Theory,MemberData(nameof(MixedCanonicalJsonCases.All),MemberType=typeof(MixedCanonicalJsonCases))]
    public void EveryCompetingCodeAndMissingErrorRejected(CanonicalJsonCase test,bool elementInput,JsonSerializationProfile? profile)
    {
        var codes=new[]{CanonicalJsonErrors.DuplicateProperty,CanonicalJsonErrors.NonfiniteNumber,CanonicalJsonErrors.InvalidUnicode,
            CanonicalJsonErrors.InvalidJson,CanonicalJsonErrors.UndefinedValue};
        foreach(var code in codes.Where(code=>code!=test.Error))
        {
            ICanonicalJson fake=profile is null ? new ScriptedCanonicalJson(test,wrongError:code) :
                new ProfileBoundCanonicalJson(new ScriptedProfiledCanonicalJson(new(test,profile.Value),wrongError:code),profile.Value);
            Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,fake)));
        }
        ICanonicalJson missing=profile is null ? new ScriptedCanonicalJson(test,omitError:true) :
            new ProfileBoundCanonicalJson(new ScriptedProfiledCanonicalJson(new(test,profile.Value),omitError:true),profile.Value);
        Assert.NotNull(Record.Exception(()=>CanonicalJsonCases.AssertCase(test,elementInput,missing)));
    }
}

[Trait("Gate","Real")]
public sealed class MixedCanonicalJsonRealTests
{
    [Theory,MemberData(nameof(MixedCanonicalJsonCases.All),MemberType=typeof(MixedCanonicalJsonCases))]
    public void NativeSharedInspectionPreservesSamePrecedence(CanonicalJsonCase test,bool elementInput,JsonSerializationProfile? profile)
    {
        ICanonicalJson provider=profile is null ? new CanonicalJson() : new ProfileBoundCanonicalJson(new CanonicalJson(),profile.Value);
        CanonicalJsonCases.AssertCase(test,elementInput,provider);
    }
}
