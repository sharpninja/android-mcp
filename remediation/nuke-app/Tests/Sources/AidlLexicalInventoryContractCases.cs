using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record AidlLexicalInventoryCase(string Id, string Text, int Declarations, int Statements,
    bool Reconciled, string[] Issues, int ExpectedRecords)
{
    public AidlLexicalInventory Correct() => new(Declarations, Statements, Reconciled, Issues.ToArray(), ExpectedRecords);
}

public static class AidlLexicalInventoryContractCases
{
    // Expected values are a manual reading of the retained inventory_counts body, not parser output.
    // LEX-COMMENTS retains the original round8 triple-quoted text, including its indentation.
    public const string OriginalComments = "package a; interface I { /* we're ignoring commas, and semicolons; */\n enum E { A, // ignored, comment;\n B, /* another, comment */ }\n const String X=\"semi;comma,\"; void f(); // trailing comment;\n } interface Empty { // comment-only body;\n }";
    public static readonly AidlLexicalInventoryCase[] All =
    [
        new("LEX-COMMENTS", OriginalComments, 3, 4, true, [], 7),
        new("LEX-SHIFT", "package a; interface I { const int F=1 << 2; void first(); void second(); }", 1, 3, true, [], 4),
        new("LEX-ENUM-SHIFT", "package a; enum E { A=1<<0, B=1<<1, C, }", 1, 3, true, [], 4),
        new("LEX-NESTED-UNKNOWN", "package a; interface I { parcelable P { int x; } broken thing; void f(); }", 2, 3, true, [], 5),
        new("LEX-NESTED-ORIGINAL", "package a;\ninterface I {\n parcelable P { int x; }\n void f();\n enum E { A=1, B=2 }\n union U { P p; int n; }\n interface J { parcelable Q { int y; } void g(); }\n}", 6, 8, true, [], 14),
        new("LEX-UNBALANCED", "package a; interface I { void x();", 1, 0, false, ["Unbalanced AIDL declaration body"], 1),
        new("LEX-NESTED-UNBALANCED", "package a; interface I { parcelable P { int x;", 1, 0, false, ["Unbalanced AIDL declaration body"], 1),
        new("LEX-NO-DECLARATION", "unsupported source", 0, 0, true, [], 1),
        new("LEX-EMPTY", "", 0, 0, true, [], 1),
        new("LEX-QUOTED-EXTERNAL", "package a; parcelable P cpp_header \"x{y}.h\";", 1, 0, true, [], 1),
        new("LEX-QUOTED-DECOY", "package a;\n@Note(\"interface First {}\") interface First { void f(); }\nparcelable Outer.Inner;\ninterface Second { void g(); }", 3, 2, true, [], 5)
    ];

    public static IEnumerable<object[]> Cases() => All.Select(test => new object[] { test.Id });
    public static AidlLexicalInventoryCase Get(string id) => All.Single(test => test.Id == id);
    public static IEnumerable<object[]> Fields() => All.SelectMany(test =>
        new[] { "Declarations", "Statements", "Reconciled", "Issues", "ExpectedRecords" }
            .Select(property => new object[] { test.Id, property }));

    public static void AssertContract(AidlLexicalInventoryCase test, IAidlLexicalInventorySourceFacts provider)
    {
        var result = provider.Inventory(test.Text);
        Assert.NotNull(result);
        Assert.Equal(test.Declarations, result.Declarations);
        Assert.Equal(test.Statements, result.Statements);
        Assert.Equal(test.Reconciled, result.Reconciled);
        Assert.NotNull(result.Issues);
        Assert.Equal(test.Issues, result.Issues.ToArray());
        Assert.Equal(test.ExpectedRecords, result.ExpectedRecords);
    }

    public static AidlLexicalInventory Wrong(AidlLexicalInventoryCase test, string property)
    {
        var correct = test.Correct();
        return property switch
        {
            "Declarations" => correct with { Declarations = correct.Declarations + 1 },
            "Statements" => correct with { Statements = correct.Statements + 1 },
            "Reconciled" => correct with { Reconciled = !correct.Reconciled },
            "Issues" => correct with { Issues = correct.Issues.Concat(["invented lexical issue"]).ToArray() },
            "ExpectedRecords" => correct with { ExpectedRecords = correct.ExpectedRecords + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
    }

    public static void AssertNullPrecondition(IAidlLexicalInventorySourceFacts provider) =>
        Assert.Throws<ArgumentNullException>(() => provider.Inventory(null!));
}

public sealed class ScriptedAidlLexicalInventory(Func<string, AidlLexicalInventory> handler) : IAidlLexicalInventorySourceFacts
{
    public List<string> Received { get; } = [];
    public AidlLexicalInventory Inventory(string text)
    {
        Received.Add(text);
        return handler(text);
    }
}
