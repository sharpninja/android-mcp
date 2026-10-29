using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One byte-hash authority. Legacy SHA1 identifies upstream objects, never our artifacts.</summary>
public static class ContentHash
{
    public static string Sha256(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Sha256(Stream bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string AidlSha1Legacy(ReadOnlySpan<byte> bytes)=>LegacySha1(bytes,[]);
    public static string GitBlobSha1Legacy(ReadOnlySpan<byte> payload)
    {
        var framing=Encoding.ASCII.GetBytes("blob "+payload.Length.ToString(CultureInfo.InvariantCulture)+"\0");
        return LegacySha1(payload,framing);
    }
    private static string LegacySha1(ReadOnlySpan<byte> payload,ReadOnlySpan<byte> framing)
    {
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        digest.AppendData(framing);digest.AppendData(payload);return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
}
