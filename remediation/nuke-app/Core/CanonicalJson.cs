using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Canonical contract JSON. Ordinary byte hashing is supplied by the shared hash service.</summary>
public sealed class CanonicalJson(Func<ReadOnlyMemory<byte>,string> sha256,IStrictJsonInspector? inspector=null):IProfiledCanonicalJson
{
    public CanonicalJson():this(bytes=>ContentHash.Sha256(bytes.Span)) { }
    private static readonly UTF8Encoding Utf8=new(false,true);
    private readonly IStrictJsonInspector strict=inspector ?? new StrictJsonInspector();
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)=>Serialize(rawUtf8,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(JsonElement value)=>Serialize(value,JsonSerializationProfile.Utf8Canonical);
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile)
    {
        ValidateProfile(profile);
        return SerializeInspection(strict.Inspect(rawUtf8),profile);
    }
    public byte[] Serialize(JsonElement value,JsonSerializationProfile profile)
    {
        ValidateProfile(profile);
        return SerializeInspection(strict.Inspect(value),profile);
    }
    private static byte[] SerializeInspection(StrictJsonInspection inspection,JsonSerializationProfile profile)
    {
        var diagnostics=inspection.CanonicalDiagnostics;
        if(diagnostics.Count!=0) throw new CanonicalJsonException(diagnostics[0].Code,diagnostics[0].Message);
        var value=inspection.Value ?? throw new InvalidOperationException("Strict JSON inspection returned neither a value nor a diagnostic");
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
                var properties=new List<(string Name,JsonElement Value)>();
                foreach(var property in value.EnumerateObject())
                    properties.Add((property.Name,property.Value));
                properties.Sort((left,right)=>StrictJsonInspector.CompareScalars(left.Name,right.Name));
                output.Append('{');
                for(var i=0;i<properties.Count;i++)
                { if(i>0) output.Append(',');Quoted(properties[i].Name,output,profile);output.Append(':');Write(properties[i].Value,output,profile); }
                output.Append('}');return;
            case JsonValueKind.Array:
                output.Append('[');var first=true;
                foreach(var item in value.EnumerateArray()) { if(!first) output.Append(',');Write(item,output,profile);first=false; }
                output.Append(']');return;
            case JsonValueKind.String:
                Quoted(value.GetString()!,output,profile);return;
            case JsonValueKind.Number:
                output.Append(Number(value.GetRawText()));return;
            case JsonValueKind.True:output.Append("true");return;
            case JsonValueKind.False:output.Append("false");return;
            case JsonValueKind.Null:output.Append("null");return;
            default:throw new InvalidOperationException("Strict JSON inspection supplied an unsupported JSON value kind");
        }
    }
    private static void Quoted(string value,StringBuilder output,JsonSerializationProfile profile)
    {
        output.Append('"');
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
    private static string Number(string raw)
    {
        if(StrictJsonInspector.IsIntegerToken(raw)) return BigInteger.Parse(raw,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        var value=double.Parse(raw,NumberStyles.Float,CultureInfo.InvariantCulture);
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
