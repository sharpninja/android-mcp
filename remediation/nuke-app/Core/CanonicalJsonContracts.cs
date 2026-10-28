using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One canonical contract-byte authority. Raw number tokens retain integer/float semantics.</summary>
public interface ICanonicalJson
{
    byte[] Serialize(ReadOnlyMemory<byte> rawUtf8);
    byte[] Serialize(JsonElement value);
    string Hash(ReadOnlyMemory<byte> rawUtf8);
    string Hash(JsonElement value);
}

public enum JsonSerializationProfile { Utf8Canonical, LegacyAsciiIdentity }

// Additive seam keeps existing production compilation green while the new
// profile assertions are proved against mocks before the formatter is extended.
public interface IProfiledCanonicalJson:ICanonicalJson
{
    byte[] Serialize(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile);
    byte[] Serialize(JsonElement value,JsonSerializationProfile profile);
    string Hash(ReadOnlyMemory<byte> rawUtf8,JsonSerializationProfile profile);
    string Hash(JsonElement value,JsonSerializationProfile profile);
}

public static class CanonicalJsonErrors
{
    public const string InvalidJson = "INVALID_JSON";
    public const string DuplicateProperty = "DUPLICATE_PROPERTY";
    public const string NonfiniteNumber = "NONFINITE_NUMBER";
    public const string InvalidUnicode = "INVALID_UNICODE";
    public const string UndefinedValue = "UNDEFINED_VALUE";
}

public sealed class CanonicalJsonException(string code, string message, Exception? inner = null)
    : FormatException(message, inner)
{
    public string Code { get; } = code;
}
