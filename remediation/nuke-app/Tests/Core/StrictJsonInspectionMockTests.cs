using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record StrictJsonInspectionCase(string Id, byte[] Input, string? ExpectedRaw, IReadOnlyList<StrictJsonDiagnostic> ExpectedDiagnostics, bool ElementOnly = false)
{
    public StrictJsonInspection CorrectResult() => new(ExpectedRaw is null ? null : Parse(ExpectedRaw), ExpectedDiagnostics.Select(x => x with { }).ToArray());
    public static JsonElement Parse(string text)
    { using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = int.MaxValue }); return document.RootElement.Clone(); }
}

public static class StrictJsonInspectionCases
{
    private static StrictJsonDiagnostic Error(string code, string path = "", string? name = null, bool fatal = true, long rank = 0) => new(code, path, name, "Independent fixture diagnostic", fatal, rank);
    private static StrictJsonInspectionCase Good(string id, string text, string? raw = null) => new(id, Encoding.UTF8.GetBytes(text), raw ?? text, []);
    private static StrictJsonInspectionCase Semantic(string id, string text, params StrictJsonDiagnostic[] errors) => new(id, Encoding.UTF8.GetBytes(text), text, errors);
    private static StrictJsonDiagnostic Duplicate(string path, string name, long rank = 0) => Error(CanonicalJsonErrors.DuplicateProperty, path, name, false, rank);
    public static IReadOnlyList<StrictJsonInspectionCase> All { get; } =
    [
        Good("INSPECT-01", "{}"), Good("INSPECT-02", "[]"), Good("INSPECT-03", "null"), Good("INSPECT-04", "true"), Good("INSPECT-05", "false"),
        Good("INSPECT-06", "\"café😀\""), Good("INSPECT-07", "  {\"a\":1} \r\n", "{\"a\":1}"),
        Good("INSPECT-08", "[1,1.0,-0,-0.0,1e+00,1e-9999]"), Good("INSPECT-09", new string('9', 2000)),
        Good("INSPECT-10", "\"\\uD83D\\uDE00\""), Good("INSPECT-11", "{\"😀\":\"é\",\"𐀀\":1}"),
        Good("INSPECT-12", new string('[', 256) + "0" + new string(']', 256)), Good("INSPECT-13", "1e308"),
        Semantic("INSPECT-14", "{\"a\":1,\"a\":2}", Duplicate("/a", "a")),
        Semantic("INSPECT-15", "{\"a\":1,\"\\u0061\":2}", Duplicate("/a", "a")),
        Semantic("INSPECT-16", "{\"outer\":{\"x\":1,\"x\":2}}", Duplicate("/outer/x", "x")),
        Semantic("INSPECT-17", "{\"a/b~c\":1,\"a/b~c\":2}", Duplicate("/a~1b~0c", "a/b~c")),
        Semantic("INSPECT-18", "[{\"x\":1,\"x\":2},{\"x\":3,\"x\":4}]", Duplicate("/0/x", "x"), Duplicate("/1/x", "x", 1)),
        Semantic("INSPECT-19", "{\"x\":1,\"nested\":{\"y\":1,\"y\":2},\"x\":2}", Duplicate("/nested/y", "y", 1), Duplicate("/x", "x")),
        Semantic("INSPECT-20", "{\"a\":1,\"a\":2,\"a\":3}", Duplicate("/a", "a"), Duplicate("/a", "a", 1)),
        Semantic("INSPECT-21", "1e400", Error(CanonicalJsonErrors.NonfiniteNumber)),
        Semantic("INSPECT-22", "[-1e999]", Error(CanonicalJsonErrors.NonfiniteNumber, "/0")),
        Semantic("INSPECT-23", "\"\\uD800\"", Error(CanonicalJsonErrors.InvalidUnicode)),
        Semantic("INSPECT-24", "{\"\\uD800\":1}", Error(CanonicalJsonErrors.InvalidUnicode)),
        Semantic("INSPECT-25", "{\"a\":[\"\\uDC00\"]}", Error(CanonicalJsonErrors.InvalidUnicode, "/a/0")),
        // Source diagnostics are postorder; canonical rank is independent and
        // preserves the original formatter's own-name-before-value precedence.
        Semantic("INSPECT-26", "{\"x\":1,\"n\":1e400,\"x\":2}", Error(CanonicalJsonErrors.NonfiniteNumber, "/n", rank: 1), Duplicate("/x", "x")),
        new("INSPECT-27", [0xff], null, [Error(CanonicalJsonErrors.InvalidUnicode)]),
        new("INSPECT-28", [34, 0xff, 34], null, [Error(CanonicalJsonErrors.InvalidUnicode)]),
        new("INSPECT-29", Encoding.UTF8.GetBytes("{"), null, [Error(CanonicalJsonErrors.InvalidJson)]),
        new("INSPECT-30", Encoding.UTF8.GetBytes("{} {}"), null, [Error(CanonicalJsonErrors.InvalidJson)]),
        new("INSPECT-31", Encoding.UTF8.GetBytes("NaN"), null, [Error(CanonicalJsonErrors.InvalidJson)]),
        new("INSPECT-32", [], null, [Error(CanonicalJsonErrors.InvalidJson)]),
        new("INSPECT-33", [0xef, 0xbb, 0xbf, 123, 125], null, [Error(CanonicalJsonErrors.InvalidJson)]),
        new("INSPECT-34", [], null, [Error(CanonicalJsonErrors.UndefinedValue)], true),
        new("INSPECT-35", Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2,"), null, [Error(CanonicalJsonErrors.InvalidJson)]),
        Semantic("INSPECT-36", "{\"\":1,\"\":2}", Duplicate("/", "")),
        Semantic("INSPECT-37", "{\"a\":1e400,\"b\":{\"x\":1,\"x\":2}}", Error(CanonicalJsonErrors.NonfiniteNumber, "/a"), Duplicate("/b/x", "x", 1)),
        Semantic("INSPECT-38", "{\"a\":{\"x\":1,\"x\":2},\"b\":1e400}", Duplicate("/a/x", "x"), Error(CanonicalJsonErrors.NonfiniteNumber, "/b", rank: 1)),
        Semantic("INSPECT-39", "{\"z\":1e400,\"a\":{\"x\":1,\"x\":2}}", Error(CanonicalJsonErrors.NonfiniteNumber, "/z", rank: 1), Duplicate("/a/x", "x")),
        Semantic("INSPECT-40", "{\"z\":{\"x\":1,\"x\":2},\"a\":1e400}", Duplicate("/z/x", "x", 1), Error(CanonicalJsonErrors.NonfiniteNumber, "/a")),
        Semantic("INSPECT-41", "{\"a\":1,\"\\u0061\":2,\"b\":\"\\uD800\"}", Error(CanonicalJsonErrors.InvalidUnicode, "/b", rank: 1), Duplicate("/a", "a")),
        Semantic("INSPECT-42", "{\"\\uD800\":1,\"a\":1,\"a\":2}", Error(CanonicalJsonErrors.InvalidUnicode), Duplicate("/a", "a", 1)),
        Semantic("INSPECT-43", "{\"a\":1,\"a\":2,\"\\uD800\":3}", Duplicate("/a", "a"), Error(CanonicalJsonErrors.InvalidUnicode, rank: 1)),
        Semantic("INSPECT-44", "{\"𐀀\":1e400,\"\ue000\":{\"x\":1,\"x\":2}}", Error(CanonicalJsonErrors.NonfiniteNumber, "/𐀀", rank: 1), Duplicate("/\ue000/x", "x")),
        Semantic("INSPECT-45", "[1e400,{\"x\":1,\"x\":2}]", Error(CanonicalJsonErrors.NonfiniteNumber, "/0"), Duplicate("/1/x", "x", 1)),
        Semantic("INSPECT-46", "[0,0,1e400,0,0,0,0,0,0,0,{\"x\":1,\"x\":2}]", Error(CanonicalJsonErrors.NonfiniteNumber, "/2"), Duplicate("/10/x", "x", 1))
    ];
    public static IEnumerable<object[]> ByteCases => All.Where(x => !x.ElementOnly).Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> ElementCases => All.Where(x => x.ExpectedRaw is not null || x.ElementOnly).Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> DiagnosticMutations => All.SelectMany(x => x.ExpectedDiagnostics.SelectMany((_, index) => new[] { "code", "path", "name", "message", "fatal", "rank" }.Select(diagnosticField => new object[] { x.Id, index, diagnosticField })));
    public static StrictJsonInspectionCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertByte(StrictJsonInspectionCase test, IStrictJsonInspector provider)
    {
        var bytes = test.Input.ToArray(); var result = provider.Inspect(bytes); Assert.Equal(test.Input, bytes); AssertResult(test, result);
    }
    public static void AssertElement(StrictJsonInspectionCase test, IStrictJsonInspector provider)
    {
        StrictJsonInspection result;
        if (test.ElementOnly) result = provider.Inspect(default(JsonElement));
        else
        {
            using var document = JsonDocument.Parse(test.Input, new JsonDocumentOptions { MaxDepth = int.MaxValue });
            result = provider.Inspect(document.RootElement);
            Assert.Equal(test.ExpectedRaw, document.RootElement.GetRawText());
        }
        // Deliberately after the caller-owned JsonDocument has been disposed.
        AssertResult(test, result);
    }
    public static void AssertResult(StrictJsonInspectionCase test, StrictJsonInspection result)
    {
        Assert.Equal(test.ExpectedRaw is not null, result.Value.HasValue);
        if (test.ExpectedRaw is not null) Assert.Equal(test.ExpectedRaw, result.Value!.Value.GetRawText());
        Assert.Equal(test.ExpectedDiagnostics.Count, result.Diagnostics.Count);
        for (var index = 0; index < test.ExpectedDiagnostics.Count; index++)
        {
            var expected = test.ExpectedDiagnostics[index]; var actual = result.Diagnostics[index];
            Assert.Equal(expected.Code, actual.Code); Assert.Equal(expected.Path, actual.Path); Assert.Equal(expected.DecodedPropertyName, actual.DecodedPropertyName);
            Assert.Equal(expected.IsFatal, actual.IsFatal); Assert.Equal(expected.CanonicalRank, actual.CanonicalRank); Assert.False(string.IsNullOrWhiteSpace(actual.Message));
        }
        Assert.Equal(test.ExpectedDiagnostics.Any(x => x.IsFatal), result.HasFatalErrors);
        Assert.Equal(test.ExpectedDiagnostics.Any(x => x.Code == CanonicalJsonErrors.DuplicateProperty), result.HasDuplicateProperties);
        var ordered = test.ExpectedDiagnostics.OrderBy(x => x.CanonicalRank).ToArray();
        Assert.Equal(ordered.Length, result.CanonicalDiagnostics.Count);
        Assert.Equal(ordered.Length, result.Diagnostics.Select(x => x.CanonicalRank).Distinct().Count());
        for (var index = 0; index < ordered.Length; index++)
        {
            Assert.Equal(ordered[index].Code, result.CanonicalDiagnostics[index].Code); Assert.Equal(ordered[index].Path, result.CanonicalDiagnostics[index].Path);
            Assert.Same(result.Diagnostics.Single(x => x.CanonicalRank == ordered[index].CanonicalRank), result.CanonicalDiagnostics[index]);
        }
    }
}

public sealed class ScriptedStrictJsonInspector(Func<ReadOnlyMemory<byte>,StrictJsonInspection> bytes, Func<JsonElement,StrictJsonInspection> elements) : IStrictJsonInspector
{
    public List<ReadOnlyMemory<byte>> ByteCalls { get; } = [];
    public List<JsonElement> ElementCalls { get; } = [];
    public StrictJsonInspection Inspect(ReadOnlyMemory<byte> utf8) { ByteCalls.Add(utf8); return bytes(utf8); }
    public StrictJsonInspection Inspect(JsonElement value) { ElementCalls.Add(value.ValueKind == JsonValueKind.Undefined ? value : value.Clone()); return elements(value); }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class StrictJsonInspectionMockTests
{
    [Theory, MemberData(nameof(StrictJsonInspectionCases.ByteCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void CorrectByteMocksProveExactFutureInspectorAssertions(string id)
    {
        var test = StrictJsonInspectionCases.Get(id); var fake = new ScriptedStrictJsonInspector(_ => test.CorrectResult(), _ => throw new IOException("wrong overload"));
        StrictJsonInspectionCases.AssertByte(test, fake); Assert.Equal(test.Input, Assert.Single(fake.ByteCalls).ToArray()); Assert.Empty(fake.ElementCalls);
    }
    [Theory, MemberData(nameof(StrictJsonInspectionCases.ElementCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void CorrectElementMocksProveOwnedCloneOutsideCallerLifetime(string id)
    {
        var test = StrictJsonInspectionCases.Get(id); var fake = new ScriptedStrictJsonInspector(_ => throw new IOException("wrong overload"), _ => test.CorrectResult());
        StrictJsonInspectionCases.AssertElement(test, fake); Assert.Single(fake.ElementCalls); Assert.Empty(fake.ByteCalls);
    }
    [Theory, MemberData(nameof(StrictJsonInspectionCases.DiagnosticMutations), MemberType = typeof(StrictJsonInspectionCases))]
    public void EveryDiagnosticFieldDiscriminatesIndividuallyWrongMocks(string id, int index, string diagnosticField)
    {
        var test = StrictJsonInspectionCases.Get(id); var correct = test.CorrectResult(); var diagnostics = correct.Diagnostics.ToArray(); var item = diagnostics[index];
        diagnostics[index] = diagnosticField switch
        {
            "code" => item with { Code = "wrong" }, "path" => item with { Path = item.Path + "/wrong" },
            "name" => item with { DecodedPropertyName = (item.DecodedPropertyName ?? "") + "wrong" },
            "message" => item with { Message = "" }, "fatal" => item with { IsFatal = !item.IsFatal }, "rank" => item with { CanonicalRank = item.CanonicalRank + 100 }, _ => throw new ArgumentOutOfRangeException(nameof(diagnosticField))
        };
        var wrong = correct with { Diagnostics = diagnostics };
        var fake = new ScriptedStrictJsonInspector(_ => wrong, _ => wrong);
        Assert.NotNull(Record.Exception(() => { if (test.ElementOnly) StrictJsonInspectionCases.AssertElement(test, fake); else StrictJsonInspectionCases.AssertByte(test, fake); }));
    }
    [Theory, MemberData(nameof(StrictJsonInspectionCases.ByteCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void MissingOrInventedValuesDiagnosticsInputMutationAndExceptionsAreRejected(string id)
    {
        var test = StrictJsonInspectionCases.Get(id); var correct = test.CorrectResult();
        var wrongValue = correct with { Value = test.ExpectedRaw is null ? StrictJsonInspectionCase.Parse("null") : null };
        Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(_ => wrongValue, _ => wrongValue))));
        var wrongCount = correct with { Diagnostics = correct.Diagnostics.Concat([new StrictJsonDiagnostic("wrong", "", null, "invented", true)]).ToArray() };
        Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(_ => wrongCount, _ => wrongCount))));
        if (correct.Diagnostics.Count > 0) Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(_ => correct with { Diagnostics = [] }, _ => correct))));
        if (test.ExpectedRaw is not null)
        {
            var wrongRaw = StrictJsonInspectionCase.Parse(test.ExpectedRaw == "{}" ? "[]" : "{}");
            Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(_ => correct with { Value = wrongRaw }, _ => correct))));
        }
        if (test.Input.Length > 0)
            Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(bytes =>
            { Assert.True(MemoryMarshal.TryGetArray(bytes, out var input)); input.Array![input.Offset] ^= 1; return test.CorrectResult(); }, _ => correct))));
        Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertByte(test, new ScriptedStrictJsonInspector(_ => throw new IOException("scripted failure"), _ => correct))));
    }
    [Theory, MemberData(nameof(StrictJsonInspectionCases.ElementCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void BorrowedCallerDocumentAndWrongElementOutcomesAreRejected(string id)
    {
        var test = StrictJsonInspectionCases.Get(id);
        if (!test.ElementOnly)
            Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertElement(test, new ScriptedStrictJsonInspector(_ => test.CorrectResult(), value => test.CorrectResult() with { Value = value }))));
        var wrong = test.CorrectResult() with { Value = test.ExpectedRaw is null ? StrictJsonInspectionCase.Parse("null") : null };
        Assert.NotNull(Record.Exception(() => StrictJsonInspectionCases.AssertElement(test, new ScriptedStrictJsonInspector(_ => wrong, _ => wrong))));
    }
}
