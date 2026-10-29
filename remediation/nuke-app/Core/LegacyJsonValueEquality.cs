using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Original loaded-JSON equality, shared by consumers retaining Python value semantics.</summary>
public sealed class LegacyJsonValueEquality : ILegacyJsonValueEquality
{
    public bool Equal(JsonElement left, JsonElement right)
    {
        if (Numeric(left) && Numeric(right)) return NumbersEqual(left, right);
        if (left.ValueKind != right.ValueKind) return false;
        switch (left.ValueKind)
        {
            case JsonValueKind.Null: return true;
            case JsonValueKind.String: return left.GetString() == right.GetString();
            case JsonValueKind.Array:
                return left.GetArrayLength() == right.GetArrayLength() && left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Equal(pair.First, pair.Second));
            case JsonValueKind.Object:
                var properties = left.EnumerateObject().ToArray();
                return properties.Length == right.EnumerateObject().Count() && properties.All(property => right.TryGetProperty(property.Name, out var value) && Equal(property.Value, value));
            default: throw new ArgumentException("Expected admitted JSON value");
        }
    }

    private static bool Numeric(JsonElement value) => value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
    private static bool Integer(JsonElement value) => value.ValueKind != JsonValueKind.Number || StrictJsonInspector.IsIntegerToken(value.GetRawText());
    private static BigInteger Whole(JsonElement value) => value.ValueKind is JsonValueKind.True ? BigInteger.One : value.ValueKind is JsonValueKind.False ? BigInteger.Zero
        : BigInteger.Parse(value.GetRawText(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    private static bool NumbersEqual(JsonElement left, JsonElement right)
    {
        var leftWhole = Integer(left); var rightWhole = Integer(right);
        if (leftWhole && rightWhole) return Whole(left) == Whole(right);
        if (!leftWhole && !rightWhole) return left.GetDouble() == right.GetDouble();
        var integral = leftWhole ? Whole(left) : Whole(right); var floating = leftWhole ? right.GetDouble() : left.GetDouble();
        // Never round the arbitrary-size integer to binary64. A finite integral
        // double converts exactly to BigInteger, including values above 2^53.
        return double.IsFinite(floating) && Math.Truncate(floating) == floating && integral == new BigInteger(floating);
    }
}
