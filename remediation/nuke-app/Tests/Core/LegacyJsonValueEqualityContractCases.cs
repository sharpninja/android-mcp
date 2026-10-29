using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record LegacyJsonEqualityCase(string Id, string Left, string Right, bool Expected);
public static class LegacyJsonValueEqualityContractCases
{
    // Literal expected results follow loaded Python JSON values and binary64
    // rounding, independently of canonical serialization or production output.
    public static IReadOnlyList<LegacyJsonEqualityCase> All { get; } =
    [
        new("EQ-TRUE-ONE", "true", "1", true), new("EQ-FALSE-ZERO", "false", "0", true),
        new("EQ-TRUE-FLOAT", "true", "1.0", true), new("EQ-FALSE-NEGATIVE-ZERO", "false", "-0.0", true),
        new("EQ-BOOL-OTHER", "true", "2", false), new("EQ-BOOL-BOOL", "true", "false", false),
        new("EQ-INT-FLOAT", "12", "12.0", true), new("EQ-FRACTION", "1", "1.5", false),
        new("EQ-FLOAT-ROUNDING", "0.1", "0.10000000000000001", true),
        new("EQ-LARGE-INTEGER-EXACT", "9007199254740992", "9007199254740992.0", true),
        new("EQ-LARGE-INTEGER-NOT-ROUNDED", "9007199254740993", "9007199254740993.0", false),
        new("EQ-LARGE-INTEGER-FLOAT-ROUNDS", "9007199254740992", "9007199254740993.0", true),
        new("EQ-LARGE-NEGATIVE", "-9007199254740993", "-9007199254740993.0", false),
        new("EQ-BIG-INTEGER", "10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", "1e100", false),
        new("EQ-EXACT-POWER", "18446744073709551616", "1.8446744073709552e19", true),
        new("EQ-INT-DIFFERENT", "9007199254740992", "9007199254740993", false),
        new("EQ-UNDERFLOW", "0", "1e-400", true), new("EQ-NEGATIVE-ZERO", "0", "-0", true),
        new("EQ-NULL", "null", "null", true), new("EQ-NULL-BOOL", "null", "false", false),
        new("EQ-STRING-NUMBER", "\"1\"", "1", false), new("EQ-STRING-CASE", "\"A\"", "\"a\"", false),
        new("EQ-STRING-UNICODE", "\"😀\"", "\"\\ud83d\\ude00\"", true),
        new("EQ-ORDERED-ARRAY", "[true,{\"x\":0},[3]]", "[1,{\"x\":false},[3.0]]", true),
        new("EQ-ARRAY-ORDER", "[1,2]", "[2,1]", false), new("EQ-ARRAY-LENGTH", "[1]", "[1,1]", false),
        new("EQ-ARRAY-KIND", "[]", "{}", false),
        new("EQ-OBJECT-ORDER", "{\"a\":true,\"b\":[false]}", "{\"b\":[0.0],\"a\":1}", true),
        new("EQ-OBJECT-KEY-CASE", "{\"a\":1}", "{\"A\":1}", false),
        new("EQ-OBJECT-COUNT", "{\"a\":1}", "{\"a\":1,\"b\":2}", false),
        new("EQ-OBJECT-NESTED-DIFFERENCE", "{\"a\":[1,2]}", "{\"a\":[1,3]}", false),
        new("EQ-EMPTY-OBJECT", "{}", "{}", true), new("EQ-EMPTY-ARRAY", "[]", "[]", true)
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static LegacyJsonEqualityCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(LegacyJsonEqualityCase test, ILegacyJsonValueEquality equality)
    {
        using var left = JsonDocument.Parse(test.Left); using var right = JsonDocument.Parse(test.Right);
        var leftBefore = left.RootElement.GetRawText(); var rightBefore = right.RootElement.GetRawText();
        Assert.Equal(test.Expected, equality.Equal(left.RootElement, right.RootElement));
        Assert.Equal(test.Expected, equality.Equal(right.RootElement, left.RootElement));
        Assert.Equal(leftBefore, left.RootElement.GetRawText()); Assert.Equal(rightBefore, right.RootElement.GetRawText());
    }
}
public sealed class ScriptedLegacyJsonEquality(bool result) : ILegacyJsonValueEquality
{
    public List<(string Left, string Right)> Calls { get; } = [];
    public bool Equal(JsonElement left, JsonElement right) { Calls.Add((left.GetRawText(), right.GetRawText())); return result; }
}
