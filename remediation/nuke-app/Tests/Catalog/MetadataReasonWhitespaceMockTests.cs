using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

// Exact existing source_metadata_store.py:_append reason.strip() semantics.
// CPython's whitespace definition includes U+001C..U+001F; .NET's ordinary
// IsNullOrWhiteSpace does not. No Python program is executed for this port.
// https://raw.githubusercontent.com/python/cpython/v3.13.0/Objects/unicodetype_db.h
// https://learn.microsoft.com/en-us/dotnet/api/system.char.iswhitespace
public static class MetadataReasonWhitespaceCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            (string Id,string Reason,bool Accepted)[] inputs=[
                ("REASON-FILE-SEPARATOR","\u001c",false),("REASON-GROUP-SEPARATOR","\u001d",false),
                ("REASON-RECORD-SEPARATOR","\u001e",false),("REASON-UNIT-SEPARATOR","\u001f",false),
                ("REASON-MIXED-CONTROL","\t\u001c\u001d\u001e\u001f \n",false),("REASON-EMPTY","",false),
                ("REASON-NO-BREAK-SPACE","\u00a0",false),("REASON-ASCII-WHITESPACE"," \t\r\n",false),
                ("REASON-ZERO-WIDTH-SPACE","\u200b",true),("REASON-CONTROL-WRAPPED-TEXT","\u001c why \u001f",true),
                ("REASON-TEXT","fixture",true),("REASON-IDEOGRAPHIC-SPACE","\u3000",false)];
            foreach(var input in inputs){
                var expected=input.Accepted?MetadataCrudCases.Row(reason:input.Reason):MetadataCrudCases.Error(422);
                var after=input.Accepted?expected:MetadataCrudCases.Error(404);
                yield return new object[]{new MetadataCrudCase(input.Id,[MetadataCrudCases.Create() with{Reason=input.Reason},new("get")],[expected,after])};
            }
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class MetadataReasonWhitespaceMockTests
{
    [Theory,MemberData(nameof(MetadataReasonWhitespaceCases.All),MemberType=typeof(MetadataReasonWhitespaceCases))]
    public void CorrectOriginalReasonResponsesAndStoredBytesPass(MetadataCrudCase test)=>new MetadataCrudMockTests().CorrectMocksPass(test);
    [Theory,MemberData(nameof(MetadataReasonWhitespaceCases.All),MemberType=typeof(MetadataReasonWhitespaceCases))]
    public void EveryResponseAndSideEffectFieldRejectsWrongValues(MetadataCrudCase test)=>new MetadataCrudMockTests().EveryFieldAtEveryStepIsDiscriminating(test);
}
