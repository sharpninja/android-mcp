using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class PublishedHashCompletionContractCases
{
    public static IReadOnlyList<PublishedHashCase> All { get; } = Create();
    private static IReadOnlyList<PublishedHashCase> Create()
    {
        var rows = new List<PublishedHashCase>(); var hash = new string('a', 64);
        void Add(string identifier, bool accepted, string separator = " ", string prefix = "", string suffix = "")
        {
            var raw = prefix + hash + separator + identifier + suffix;
            var result = (JsonObject)PublishedInterfaceHashContractCases.Get("HASH-08").Expected.DeepClone();
            result["facts"] = accepted ? new JsonArray(new JsonObject
            {
                ["line"] = 1, ["literal"] = hash, ["declared_identifier"] = identifier,
                ["format"] = "published-hidl-hash-hex64-and-identifier", ["raw"] = raw
            }) : new JsonArray();
            result["unknown_lines"] = accepted ? new JsonArray() : new JsonArray(new JsonObject { ["line"] = 1, ["raw"] = raw });
            rows.Add(new("HASH-COMP-" + rows.Count, "hidl", raw, result));
        }
        // Independently derived Unicode categories for original re: word means
        // letters/numbers/underscore, digits mean Nd, whitespace includes 001F.
        foreach (var tail in new[] { "_", "é", "Ⅷ", "²", "𐐀", "𝟘", "١" })
        {
            Add("a" + tail + ".b@1.0::IFoo", true);
            Add("a.b@1.0::I" + tail + ".Inner", true);
        }
        foreach (var tail in new[] { "\u0301", "\u203f", "\u200d", "😀" })
        {
            Add("a" + tail + "@1.0::IFoo", false);
            Add("a@1.0::I" + tail, false);
        }
        Add("a@١.٢::IFoo", true);
        Add("a@𝟘.𝟙::IFoo", true);
        Add("a@Ⅷ.0::IFoo", false);
        Add("a@1.²::IFoo", false);
        Add("é@1.0::IFoo", false); // Each identifier segment must start ASCII.
        Add("a@1.0::𐐀Foo", false);
        Add("a@1.0::IFoo", true, "\u001f");
        Add("a@1.0::IFoo", true, "\t", "\u001f", "\u001f # comment");
        Add("a@1.0::IFoo", false, "\u200b");
        Add("a..b@1.0::IFoo", false);
        Add("a@1.0::I..Inner", false);
        return rows;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static PublishedHashCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertCase(PublishedHashCase test, IPublishedInterfaceHashSourceFacts producer) => PublishedInterfaceHashContractCases.AssertContract(test, producer);
    public static IEnumerable<object[]> OriginalMethods => Enumerable.Range(1, 4).Select(index => new object[] { index });
    public static PublishedHashCase OriginalFixture(int method) => PublishedInterfaceHashContractCases.Get(method switch { 1 => "HASH-01", 2 => "HASH-02", 3 => "HASH-06", 4 => "HASH-07", _ => throw new ArgumentOutOfRangeException(nameof(method)) });
    public static void AssertOriginal(int method, IPublishedInterfaceHashSourceFacts producer)
    {
        var fixture = OriginalFixture(method); var result = fixture.Format == "aidl" ? producer.AidlHashes(fixture.Input) : producer.HidlHashes(fixture.Input);
        switch (method)
        {
            case 1:
                Assert.Equal(new[] { new string('a', 40), new string('b', 40) }, result["facts"]!.AsArray().Select(row => row!["literal"]!.GetValue<string>()));
                Assert.False(result["compiled_runtime_hash_proven"]!.GetValue<bool>()); Assert.Empty(result["unknown_lines"]!.AsArray()); break;
            case 2: Assert.Equal("not-frozen", result["unknown_lines"]![0]!["raw"]!.GetValue<string>()); break;
            case 3:
                Assert.Equal(2, result["facts"]!.AsArray().Count); Assert.Equal("android.hardware.foo@1.0::IFoo", result["facts"]![0]!["declared_identifier"]!.GetValue<string>());
                Assert.False(result["android_applicability_proven"]!.GetValue<bool>()); break;
            case 4: Assert.Equal("opaque declaration", result["unknown_lines"]![0]!["raw"]!.GetValue<string>()); break;
        }
    }
    public static IEnumerable<JsonObject> WrongOriginalResults(int method)
    {
        var baseline = OriginalFixture(method).Expected;
        var controls = method switch { 1 => 3, 3 => 3, _ => 1 };
        for (var control = 0; control < controls; control++)
        {
            var wrong = (JsonObject)baseline.DeepClone();
            if (method == 1 && control == 0) wrong["facts"]![0]!["literal"] = new string('b', 40);
            else if (method == 1 && control == 1) wrong["compiled_runtime_hash_proven"] = true;
            else if (method == 1) wrong["unknown_lines"] = new JsonArray(new JsonObject());
            else if (method == 3 && control == 0) wrong["facts"]!.AsArray().RemoveAt(1);
            else if (method == 3 && control == 1) wrong["facts"]![0]!["declared_identifier"] = "wrong";
            else if (method == 3) wrong["android_applicability_proven"] = true;
            else wrong["unknown_lines"]![0]!["raw"] = "wrong";
            yield return wrong;
        }
    }
}
