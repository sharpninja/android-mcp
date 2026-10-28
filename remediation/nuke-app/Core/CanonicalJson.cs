using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Canonical contract JSON. Ordinary byte hashing is supplied by the shared hash service.</summary>
public sealed class CanonicalJson(Func<ReadOnlyMemory<byte>,string> sha256):IProfiledCanonicalJson
{
    public CanonicalJson():this(bytes=>ContentHash.Sha256(bytes.Span)) { }
    private static readonly UTF8Encoding Utf8=new(false,true);
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)=>Serialize(rawUtf8,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(JsonElement value)=>Serialize(value,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile)
    {
        ValidateProfile(profile);
        try { _=Utf8.GetCharCount(rawUtf8.Span); }
        catch(DecoderFallbackException error) { throw new CanonicalJsonException(CanonicalJsonErrors.InvalidUnicode,"Input is not valid UTF8",error); }
        try
        {
            using var document=JsonDocument.Parse(rawUtf8,new JsonDocumentOptions{MaxDepth=int.MaxValue});
            return Serialize(document.RootElement,profile);
        }
        catch(JsonException error) { throw new CanonicalJsonException(CanonicalJsonErrors.InvalidJson,"Input is not one valid JSON value",error); }
    }
    public byte[] Serialize(JsonElement value,JsonSerializationProfile profile)
    {
        ValidateProfile(profile);
        var result=new StringBuilder();Write(value,result,profile);return Utf8.GetBytes(result.ToString());
    }
    public string Hash(ReadOnlyMemory<byte> rawUtf8)=>sha256(Serialize(rawUtf8));
    public string Hash(JsonElement value)=>sha256(Serialize(value));
    public string Hash(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile)=>sha256(Serialize(rawUtf8,profile));
    public string Hash(JsonElement value,JsonSerializationProfile profile)=>sha256(Serialize(value,profile));
    private static void ValidateProfile(JsonSerializationProfile profile)
    {
        if(profile is not (JsonSerializationProfile.Utf8Canonical or JsonSerializationProfile.LegacyAsciiIdentity))
            throw new ArgumentOutOfRangeException(nameof(profile));
    }

    private static void Write(JsonElement value,StringBuilder output,JsonSerializationProfile profile)
    {
        switch(value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties=new List<(string Name,JsonElement Value)>();var names=new HashSet<string>(StringComparer.Ordinal);
                foreach(var property in value.EnumerateObject())
                {
                    string name;
                    try { name=property.Name; }
                    catch(InvalidOperationException error) { throw InvalidUnicode(error); }
                    ValidateUnicode(name);
                    if(!names.Add(name)) throw new CanonicalJsonException(CanonicalJsonErrors.DuplicateProperty,"Duplicate decoded object property");
                    properties.Add((name,property.Value));
                }
                properties.Sort((left,right)=>CompareScalars(left.Name,right.Name));
                output.Append('{');
                for(var i=0;i<properties.Count;i++)
                { if(i>0) output.Append(',');Quoted(properties[i].Name,output,profile);output.Append(':');Write(properties[i].Value,output,profile); }
                output.Append('}');return;
            case JsonValueKind.Array:
                output.Append('[');var first=true;
                foreach(var item in value.EnumerateArray()) { if(!first) output.Append(',');Write(item,output,profile);first=false; }
                output.Append(']');return;
            case JsonValueKind.String:
                try { Quoted(value.GetString()!,output,profile); }
                catch(InvalidOperationException error) { throw InvalidUnicode(error); }
                return;
            case JsonValueKind.Number:
                output.Append(Number(value.GetRawText()));return;
            case JsonValueKind.True:output.Append("true");return;
            case JsonValueKind.False:output.Append("false");return;
            case JsonValueKind.Null:output.Append("null");return;
            default:throw new CanonicalJsonException(CanonicalJsonErrors.UndefinedValue,"Undefined JsonElement has no JSON representation");
        }
    }
    private static CanonicalJsonException InvalidUnicode(Exception? inner=null)=>new(CanonicalJsonErrors.InvalidUnicode,"Unpaired Unicode surrogate",inner);
    private static void ValidateUnicode(string value)
    {
        for(var i=0;i<value.Length;i++)
        {
            if(char.IsHighSurrogate(value[i])) { if(i+1>=value.Length || !char.IsLowSurrogate(value[++i])) throw InvalidUnicode(); }
            else if(char.IsLowSurrogate(value[i])) throw InvalidUnicode();
        }
    }
    private static void Quoted(string value,StringBuilder output,JsonSerializationProfile profile)
    {
        ValidateUnicode(value);output.Append('"');
        foreach(var character in value)
        {
            switch(character)
            {
                case '"':output.Append("\\\"");break;
                case '\\':output.Append("\\\\");break;
                case '\b':output.Append("\\b");break;
                case '\t':output.Append("\\t");break;
                case '\n':output.Append("\\n");break;
                case '\f':output.Append("\\f");break;
                case '\r':output.Append("\\r");break;
                default:
                    if(character<0x20 || profile==JsonSerializationProfile.LegacyAsciiIdentity && character>0x7e)
                        output.Append("\\u").Append(((int)character).ToString("x4",CultureInfo.InvariantCulture));
                    else output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }
    private static int CompareScalars(string left,string right)
    {
        var a=left.EnumerateRunes().GetEnumerator();var b=right.EnumerateRunes().GetEnumerator();
        while(true)
        {
            var moreA=a.MoveNext();var moreB=b.MoveNext();
            if(!moreA || !moreB) return moreA ? 1 : moreB ? -1 : 0;
            var comparison=a.Current.Value.CompareTo(b.Current.Value);if(comparison!=0) return comparison;
        }
    }
    private static string Number(string raw)
    {
        if(raw.IndexOfAny(['.','e','E'])<0) return BigInteger.Parse(raw,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        var value=double.Parse(raw,NumberStyles.Float,CultureInfo.InvariantCulture);
        if(!double.IsFinite(value)) throw new CanonicalJsonException(CanonicalJsonErrors.NonfiniteNumber,"Floating JSON number is outside finite IEEE754 binary64");
        var negative=BitConverter.DoubleToInt64Bits(value)<0;
        if(value==0) return negative ? "-0.0" : "0.0";
        var roundtrip=Math.Abs(value).ToString("R",CultureInfo.InvariantCulture);
        var exponentAt=roundtrip.IndexOfAny(['e','E']);
        var mantissa=exponentAt<0 ? roundtrip : roundtrip[..exponentAt];
        var exponent=exponentAt<0 ? 0 : int.Parse(roundtrip[(exponentAt+1)..],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
        var point=mantissa.IndexOf('.');var fractionalDigits=point<0 ? 0 : mantissa.Length-point-1;
        var digits=mantissa.Replace(".","",StringComparison.Ordinal).TrimStart('0');
        var scientificExponent=digits.Length+exponent-fractionalDigits-1;
        digits=digits.TrimEnd('0');
        string formatted;
        if(scientificExponent is >=-4 and <16)
        {
            var position=scientificExponent+1;
            formatted=position<=0 ? "0."+new string('0',-position)+digits
                : position>=digits.Length ? digits+new string('0',position-digits.Length)+".0"
                : digits[..position]+"."+digits[position..];
        }
        else
        {
            var coefficient=digits.Length==1 ? digits : digits[0]+"."+digits[1..];
            formatted=coefficient+"e"+(scientificExponent<0 ? "-" : "+")+Math.Abs(scientificExponent).ToString("D2",CultureInfo.InvariantCulture);
        }
        return negative ? "-"+formatted : formatted;
    }
}
