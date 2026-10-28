using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

internal sealed class JavaScriptedLinker(Func<string?,JsonObject?,string?> response,IReadOnlyDictionary<string,string>? expectedOwners=null) : IJvmSourceLinker
{
    public int Calls { get; private set; }
    public string? Encode(string? value,string owner,IReadOnlyDictionary<string,string> owners) { Calls++; Assert.Equal("a.C",owner);if(expectedOwners is not null)Assert.Same(expectedOwners,owners); return response(value,null); }
    public string? Key(JsonObject declaration,IReadOnlyDictionary<string,string> owners) { Calls++;if(expectedOwners is not null)Assert.Same(expectedOwners,owners); return response(null,declaration); }
}
internal sealed class JavaScriptedContext(Action<JsonArray,JsonArray> response) : IJavaFileContext
{
    public void Attach(JsonArray sources,JsonArray reports)=>response(sources,reports);
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaMockGateTests
{
    [Theory,MemberData(nameof(JavaContractCases.Cases),MemberType=typeof(JavaContractCases))]
    public void CorrectMockPassesOriginalAssertionsAndInteractions(string id)
    {
        var test=JavaContractCases.Get(id);var provider=new ScriptedSourceExtractor("java-source",source=>
        { Assert.Equal(test.Input().RelativePath,source.RelativePath);Assert.Equal(test.Input().Context,source.Context);return test.Correct(); });
        JavaContractCases.AssertContract(test,provider);Assert.Equal(test.Repetitions,provider.Received.Count);
    }

    [Theory,MemberData(nameof(JavaContractCases.Cases),MemberType=typeof(JavaContractCases))]
    public void EachAssertedRowPropertyRejectsWrongAndOmittedData(string id)
    {
        var test=JavaContractCases.Get(id);if(test.HasErrors)return;
        var adapter=new SourceExtractionCase(id,"java-source",test.Text,StructuralParseStatus.Clean,test.Rows,[],"test_java_source.py");
        foreach(var values in SourceMutationCases.For(adapter))
        {
            var mutation=(SourceMutation)values[1];var wrong=SourceMutationCases.Apply(adapter,mutation);
            Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrong.Records}))));
        }
        foreach(var wrongRows in new[]{test.Correct().Records.Skip(1).ToArray(),test.Correct().Records.Concat([test.Rows[0]]).ToArray(),test.Correct().Records.Reverse().ToArray()})
            Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrongRows}))));
    }

    [Theory,MemberData(nameof(JavaContractCases.Cases),MemberType=typeof(JavaContractCases))]
    public void MetadataStatusInputMutationAndMissingIssuesAreRejected(string id)
    {
        var test=JavaContractCases.Get(id);
        var mutations=new List<ExtractionResult>{test.Correct() with{Metadata=new()},test.Correct() with{StructuralStatus=test.HasErrors?StructuralParseStatus.Clean:StructuralParseStatus.Malformed}};
        if(test.HasErrors)
        {
            mutations.Add(test.Correct() with{Issues=[]});mutations.Add(test.Correct() with{Records=[]});
            mutations.Add(test.Correct() with{Issues=[new("java-ast-error","",0,test.Input().Bytes.Length+1)]});
        }
        else mutations.Add(test.Correct() with{Issues=[new("wrong","Unexpected issue",0,0)]});
        foreach(var wrong in mutations) Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>wrong))));
        Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("wrong",_=>test.Correct()))));
        Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>throw new IOException("mock exception")))));
        Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",source=>
        { System.Runtime.InteropServices.MemoryMarshal.TryGetArray(source.Bytes,out var bytes);bytes.Array![bytes.Offset]^=1;return test.Correct(); }))));
    }

    [Theory,MemberData(nameof(JvmContractCases.Cases),MemberType=typeof(JvmContractCases))]
    public void JvmExpectedAndWrongMocksProveExactDescriptors(string id)
    {
        var test=JvmContractCases.Get(id);var provider=new JavaScriptedLinker((type,row)=>
        { Assert.Equal(test.Type,type);if(test.Declaration is not null)Assert.True(JsonNode.DeepEquals(test.Declaration,row));return test.Expected; },test.KnownOwners);
        JvmContractCases.AssertContract(test,provider);Assert.Equal(1,provider.Calls);
        Assert.NotNull(Record.Exception(()=>JvmContractCases.AssertContract(test,new JavaScriptedLinker((_,_)=>test.Expected is null?"guessed/Identity":null))));
        Assert.NotNull(Record.Exception(()=>JvmContractCases.AssertContract(test,new JavaScriptedLinker((_,_)=>throw new IOException("mock exception")))));
    }

    private static JavaScriptedContext CorrectContext(JavaContextCase test)=>new((sources,reports)=>
    {
        Assert.True(JsonNode.DeepEquals(test.Sources(),sources));Assert.True(JsonNode.DeepEquals(test.Reports(),reports));
        if(test.Failure is not null)throw new InvalidDataException("Java context cache mismatch C.java");
        var context=(JsonObject)reports[0]!.DeepClone();context.Remove("cache_path");context.Remove("cache_sha256");context.Remove("parser_sha256");
        context["excluded_implementation_nodes"]=null;
        context["static_import_resolution"]="Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.";
        sources[0]!["java_file_context"]=context;
    });
    [Theory,MemberData(nameof(JavaContextCases.Cases),MemberType=typeof(JavaContextCases))]
    public void ContextCorrectAndNegativeMocksRetainCacheBinding(string id)
    {
        var test=JavaContextCases.Get(id);JavaContextCases.AssertContract(test,CorrectContext(test));
        Assert.NotNull(Record.Exception(()=>JavaContextCases.AssertContract(test,new JavaScriptedContext((_,_)=>{}))));
        Assert.NotNull(Record.Exception(()=>JavaContextCases.AssertContract(test,new JavaScriptedContext((_,_)=>throw new IOException("wrong failure")))));
        if(test.Failure is not null)return;
        foreach(var property in new[]{"package","imports","wildcard_imports","parser","parse_errors","excluded_implementation_nodes","static_import_resolution"})
            foreach(var omit in new[]{true,false})
                Assert.NotNull(Record.Exception(()=>JavaContextCases.AssertContract(test,new JavaScriptedContext((sources,reports)=>
                {CorrectContext(test).Attach(sources,reports);if(omit)sources[0]!["java_file_context"]!.AsObject().Remove(property);else sources[0]!["java_file_context"]![property]="wrong";}))));
        if(test.Id=="CTX-nonjava")Assert.NotNull(Record.Exception(()=>JavaContextCases.AssertContract(test,new JavaScriptedContext((sources,reports)=>
        {CorrectContext(test).Attach(sources,reports);sources[1]!["java_file_context"]=new JsonObject();}))));
        Assert.NotNull(Record.Exception(()=>JavaContextCases.AssertContract(test,new JavaScriptedContext((sources,reports)=>
        {CorrectContext(test).Attach(sources,reports);reports[0]!["imports"]!["List"]="wrong";}))));
    }
}
