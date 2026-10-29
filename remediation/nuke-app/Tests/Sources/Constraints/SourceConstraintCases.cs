using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Constraints;

public sealed record ConstraintCase(string Id,string Api,JsonObject Input,JsonNode? Expected,string? Error=null,string? ErrorKey=null,string? OriginalMethod=null);
public static class SourceConstraintCases
{
    public const string ADeclaration="No complete direct parsed AIDL interface declaration",AAnnotation="Descriptor annotation requires explicit compiler descriptor semantics",ADescriptor="No qualified AIDL descriptor",
        AIdentity="Exact declaration is not in reconciled hash input set",ARevision="Frozen hash audit revision differs",AVerified="Frozen dump hash did not verify",ALink="Frozen module version not uniquely linked",
        AStable="Stable AIDL build flag is unresolved or unstable",AModule="Build declaration revision differs",ATail="Backend tail -1 hash selection differs from verified hash";
    public const string HKind="Published package/type hash does not establish an interface descriptor",HName="No direct parsed HIDL interface FQName",HHash="No exact SHA256 source bytes",HMatch="No same-revision published HIDL interface hash equals these exact source bytes";
    public static readonly string[] AidlOriginals=["test_exact_frozen_hash_forms_interface_only_predicate","test_unstable_or_unresolved_build_flag_rejected","test_backend_physical_last_line_must_match","test_other_source_bytes_not_borrowed","test_descriptor_override_remains_unknown","test_current_dump_does_not_borrow_frozen_version","test_stable_absent_default_differs_from_unknown_expression"];
    public static readonly string[] HidlOriginals=["test_exact_published_identifier_and_source_hash_form_interface_clause","test_other_hash_cannot_describe_this_source_even_if_identifier_matches","test_other_repository_cannot_be_borrowed","test_types_file_is_not_invented_as_an_interface","test_another_declared_identifier_not_used"];
    private static JsonObject Identity(string path,char hash)=>new(){["repository"]="r",["revision"]="c",["path"]=path,["sha256"]=new string(hash,64)};
    public static JsonObject AidlInput()
    {
        var source=Identity("aidl_api/foo/1/p/I.aidl",'a');
        return new(){["source"]=source,["declaration"]=new JsonObject{["declaration_kind"]="interface",["descriptor"]="p.I",["parse_status"]="methods-parsed",["annotations"]=new JsonArray()},
            ["audit"]=new JsonObject{["repository"]="r",["revision"]="c",["source_inputs"]=new JsonArray(source.DeepClone()),["hash_source"]=Identity("aidl_api/foo/1/.hash",'b'),["result"]=new JsonObject{["verified"]=true,["computed_hash"]=new string('c',40),["version_literal"]="1"}},
            ["published"]=new string('c',40)+"\n",["module"]=new JsonObject{["kind"]="aidl_interface",["source"]=Identity("Android.bp",'d'),["properties"]=new JsonObject{["kind"]="object",["complete"]=true,["items"]=new JsonArray()}},
            ["linkage"]=new JsonObject{["status"]="frozen-dump-declared-version-linked"}};
    }
    public static JsonObject HidlInput()=>new(){["source"]=Identity("foo/1.0/IFoo.hal",'a'),["record"]=new JsonObject{["kind"]="interface",["canonical"]="android.hardware.foo@1.0::IFoo"},
        ["facts"]=new JsonArray(new JsonObject{["source"]=Identity("current.txt",'b'),["declared_identifier"]="android.hardware.foo@1.0::IFoo",["literal"]=new string('a',64),["format"]="published-hidl-hash-hex64-and-identifier",["line"]=2})};
    private static JsonObject Clause(string descriptor,JsonNode? hash)=>new(){["interface"]=new JsonObject{["descriptor"]=descriptor,["minimum_version"]=null,["accepted_hashes"]=new JsonArray(hash?.DeepClone())}};
    private static void Limits(JsonObject result){result["runtime_observation"]=false;result["android_platform_range"]=null;result["wire_codec_proven"]=false;result["os_or_client_authority_granted"]=false;}
    public static JsonObject AidlExpected(JsonObject e)
    {
        var source=e["source"]!.AsObject();var identity=new JsonObject();foreach(var key in new[]{"repository","revision","path","sha256"})identity[key]=source[key]?.DeepClone();
        var result=new JsonObject{["contract"]="exact-aidl-source-constraint/1",["clause"]=Clause(e["declaration"]!["descriptor"]!.GetValue<string>(),e["audit"]!["result"]!["computed_hash"]),
            ["frozen_version_literal"]=e["audit"]!["result"]!["version_literal"]?.DeepClone(),["source_evidence"]=new JsonArray(identity,e["audit"]!["hash_source"]?.DeepClone(),e["module"]!["source"]?.DeepClone())};
        Limits(result);result["source_hash_verified"]=true;result["backend_selection"]="pinned aidl_gen_rule.go --hash tail -1";
        result["limitations"]=new JsonArray("Future trusted descriptor/hash reachability facts required","Type dependencies, OS/client grants and codecs remain separate");return result;
    }
    public static JsonObject HidlExpected(JsonObject e,int[]? matchIndexes=null,int[]? evidenceIndexes=null)
    {
        var identity=new JsonObject();foreach(var key in new[]{"repository","revision","path","sha256"})identity[key]=e["source"]![key]?.DeepClone();
        var facts=e["facts"]!.AsArray();var evidence=new JsonArray(identity);foreach(var index in evidenceIndexes??[0])evidence.Add(facts[index]!["source"]?.DeepClone());
        var lines=new JsonArray();foreach(var index in matchIndexes??[0])lines.Add(facts[index]!["line"]?.DeepClone());
        var result=new JsonObject{["contract"]="exact-hidl-source-constraint/1",["clause"]=Clause(e["record"]!["canonical"]!.GetValue<string>(),e["source"]!["sha256"]),["source_evidence"]=evidence,["published_lines"]=lines,["source_bytes_hash_equals_published_identifier_hash"]=true};
        Limits(result);result["normative_reference"]="https://source.android.com/docs/core/architecture/hidl/hashing";return result;
    }
    private static JsonObject Unknown(string reason)=>new(){["unknown_reason"]=reason};
    public static IReadOnlyList<ConstraintCase> All()
    {
        var cases=new List<ConstraintCase>();
        void A(string id,Action<JsonObject>? change=null,string? reason=null,int? original=null,string? error=null,string? key=null){var e=AidlInput();change?.Invoke(e);cases.Add(new(id,"aidl",e,error is not null?null:reason is null?AidlExpected(e):Unknown(reason),error,key,original is {} i?AidlOriginals[i]:null));}
        void H(string id,Action<JsonObject>? change=null,string? reason=null,int? original=null,int[]? matches=null,int[]? evidence=null,string? error=null,string? key=null){var e=HidlInput();change?.Invoke(e);cases.Add(new(id,"hidl",e,error is not null?null:reason is null?HidlExpected(e,matches,evidence):Unknown(reason),error,key,original is {} i?HidlOriginals[i]:null));}
        void Stable(string id,bool expected,Action<JsonObject>? change=null,bool original=false){var e=AidlInput();change?.Invoke(e);cases.Add(new(id,"stable",e,JsonValue.Create(expected),OriginalMethod:original?AidlOriginals[6]:null));}
        void Flag(JsonObject e,JsonObject value)=>e["module"]!["properties"]!["items"]=new JsonArray(new JsonObject{["name"]="unstable",["value"]=value});
        A("A-original",original:0);A("A-unstable",e=>Flag(e,new(){["kind"]="literal",["value"]=true}),AStable,1);A("A-unresolved",e=>Flag(e,new(){["kind"]="unresolved-expression"}),AStable,1);
        A("A-blank-tail",e=>e["published"]=new string('c',40)+"\n\n",ATail,2);A("A-other-bytes",e=>e["source"]!["sha256"]=new string('e',64),AIdentity,3);
        A("A-override",e=>e["declaration"]!["annotations"]=new JsonArray(new JsonObject{["name"]="Descriptor"}),AAnnotation,4);A("A-development",e=>e["linkage"]!["status"]="development-dump-linked-version-unknown",ALink,5);
        Stable("S-absent",true,original:true);Stable("S-incomplete",false,e=>e["module"]!["properties"]!["complete"]=false,true);
        H("H-original",original:0);H("H-other-hash",e=>e["facts"]![0]!["literal"]=new string('c',64),HMatch,1);H("H-other-repository",e=>e["facts"]![0]!["source"]!["repository"]="other",HMatch,2);
        H("H-type",e=>e["record"]!["kind"]="type",HKind,3);H("H-other-identifier",e=>e["record"]!["canonical"]="android.hardware.foo@1.0::IBar",HMatch,4);
        A("A-kind",e=>e["declaration"]!["declaration_kind"]="parcelable",ADeclaration);A("A-depth",e=>e["declaration"]!["declaration_depth"]=1,ADeclaration);A("A-status",e=>e["declaration"]!["parse_status"]="partial",ADeclaration);
        A("A-depth-false",e=>e["declaration"]!["declaration_depth"]=false);A("A-depth-zero-real",e=>e["declaration"]!["declaration_depth"]=0.0);
        A("A-qualified-override",e=>e["declaration"]!["annotations"]=new JsonArray(new JsonObject{["name"]="p.Descriptor"}),AAnnotation);
        A("A-annotation-case",e=>e["declaration"]!["annotations"]=new JsonArray(new JsonObject{["name"]="descriptor"}));
        foreach(var descriptor in new[]{"p","p.I²","p.I𐐀"})A("A-name-"+descriptor,e=>e["declaration"]!["descriptor"]=descriptor);
        foreach(var descriptor in new[]{"2p.I","p..I","p.I\n","p.I\u0301"})A("A-bad-name-"+descriptor,e=>e["declaration"]!["descriptor"]=descriptor,ADescriptor);
        A("A-nonstring-name",e=>e["declaration"]!["descriptor"]=1,ADescriptor);A("A-audit-revision",e=>e["audit"]!["revision"]="other",ARevision);A("A-unverified",e=>e["audit"]!["result"]!["verified"]=false,AVerified);
        A("A-verified-truthy",e=>e["audit"]!["result"]!["verified"]="yes");A("A-module-revision",e=>e["module"]!["source"]!["revision"]="other",AModule);
        A("A-source-extra-member",e=>e["audit"]!["source_inputs"]![0]!["extra"]=true,AIdentity);A("A-first-failure",e=>{e["declaration"]!["declaration_depth"]=1;e["source"]!.AsObject().Remove("sha256");},ADeclaration);
        A("A-required-identity",e=>e["source"]!.AsObject().Remove("sha256"),error:nameof(KeyNotFoundException),key:"sha256");A("A-required-computed",e=>e["audit"]!["result"]!.AsObject().Remove("computed_hash"),error:nameof(KeyNotFoundException),key:"computed_hash");
        A("A-required-version",e=>e["audit"]!["result"]!.AsObject().Remove("version_literal"),error:nameof(KeyNotFoundException),key:"version_literal");A("A-required-hash-source",e=>e["audit"]!.AsObject().Remove("hash_source"),error:nameof(KeyNotFoundException),key:"hash_source");
        A("A-equality-bool-number",e=>{e["source"]!["revision"]=true;e["audit"]!["revision"]=1;e["audit"]!["source_inputs"]![0]!["revision"]=1;e["module"]!["source"]!["revision"]=1;});
        A("A-input-key-order",e=>e["audit"]!["source_inputs"]![0]=new JsonObject(e["source"]!.AsObject().Reverse().Select(pair=>new KeyValuePair<string,JsonNode?>(pair.Key,pair.Value?.DeepClone()))));
        A("A-shape",e=>e["declaration"]!["annotations"]=1,error:nameof(ArgumentException));
        foreach(var suffix in new[]{"\r\n","\v","\f","\u001c","\u0085","\u2028","\u2029"})A("A-lines-"+((int)suffix[0]),e=>e["published"]=new string('c',40)+suffix);
        A("A-strip",e=>e["published"]="\u001f "+new string('c',40)+" \u001f\n");
        A("A-no-added-hash-format",e=>{e["published"]="not-a-sha\n";e["audit"]!["result"]!["computed_hash"]="not-a-sha";e["audit"]!["result"]!["version_literal"]=7;});
        A("A-empty-hash-selected",e=>{e["published"]="";e["audit"]!["result"]!["computed_hash"]="";});
        A("A-provenance-copy",e=>{e["audit"]!["hash_source"]!["extra"]=new JsonArray(new JsonObject{["nested"]=true});e["module"]!["source"]!["extra"]=new JsonObject{["nested"]=true};});
        Stable("S-false",true,e=>Flag(e,new(){["kind"]="literal",["value"]=false}));Stable("S-zero",false,e=>Flag(e,new(){["kind"]="literal",["value"]=0}));Stable("S-null",false,e=>Flag(e,new(){["kind"]="literal",["value"]=null}));
        Stable("S-duplicates",false,e=>{Flag(e,new(){["kind"]="literal",["value"]=false});e["module"]!["properties"]!["items"]!.AsArray().Add(e["module"]!["properties"]!["items"]![0]!.DeepClone());});
        Stable("S-kind",false,e=>e["module"]!["kind"]="cc_library");Stable("S-complete-zero",false,e=>e["module"]!["properties"]!["complete"]=0);Stable("S-complete-string",true,e=>e["module"]!["properties"]!["complete"]="yes");
        Stable("S-kind-short-circuit",false,e=>{e["module"]!["kind"]="other";e["module"]!["properties"]=null;});Stable("S-empty-complete",false,e=>e["module"]!["properties"]!["complete"]=new JsonArray());Stable("S-list-complete",true,e=>e["module"]!["properties"]!["complete"]=new JsonArray(1));
        H("H-name",e=>e["record"]!["canonical"]="bad",HName);H("H-hash-upper",e=>e["source"]!["sha256"]=new string('A',64),HHash);H("H-hash-newline",e=>e["source"]!["sha256"]=new string('a',64)+"\n",HHash);
        H("H-revision",e=>e["facts"]![0]!["source"]!["revision"]="other",HMatch);H("H-format",e=>e["facts"]![0]!["format"]="other",HMatch);H("H-no-facts",e=>e["facts"]=new JsonArray(),HMatch);
        H("H-duplicates",e=>{var second=e["facts"]![0]!.DeepClone();second["line"]=3;e["facts"]!.AsArray().Add(second);},matches:[0,1],evidence:[0]);
        H("H-distinct-provenance",e=>{var second=e["facts"]![0]!.DeepClone();second["source"]!["path"]="other.txt";second["line"]=3;e["facts"]!.AsArray().Add(second);},matches:[0,1],evidence:[0,1]);
        H("H-identity-dedup",e=>e["facts"]![0]!["source"]=e["source"]!.DeepClone(),evidence:[]);
        H("H-evidence-extra",e=>e["facts"]![0]!["source"]!["extra"]=new JsonArray(new JsonObject{["nested"]=true}));
        H("H-required-line",e=>e["facts"]![0]!.AsObject().Remove("line"),error:nameof(KeyNotFoundException),key:"line");H("H-required-identity",e=>e["source"]!.AsObject().Remove("path"),error:nameof(KeyNotFoundException),key:"path");
        H("H-required-fact-source",e=>e["facts"]![0]!.AsObject().Remove("source"),error:nameof(KeyNotFoundException),key:"source");
        H("H-equality-bool-number",e=>{e["source"]!["revision"]=true;e["facts"]![0]!["source"]!["revision"]=1;});
        H("H-evidence-numeric-dedup",e=>{e["facts"]![0]!["source"]!["extra"]=true;var second=e["facts"]![0]!.DeepClone();second["source"]!["extra"]=1;e["facts"]!.AsArray().Add(second);},matches:[0,1],evidence:[0]);
        H("H-late-identity",e=>{e["source"]!.AsObject().Remove("path");e["facts"]=new JsonArray();},HMatch);
        H("H-shape",e=>e["facts"]![0]!["source"]=1,error:nameof(ArgumentException));
        H("H-unicode",e=>{e["record"]!["canonical"]="p.I²@١.٢::I𐐀";e["facts"]![0]!["declared_identifier"]="p.I²@١.٢::I𐐀";});
        H("H-name-newline",e=>e["record"]!["canonical"]="p.I@1.2::I\n",HName);
        return cases;
    }
    public static IEnumerable<object[]> Cases=>All().Select(test=>new object[]{test});
    public static async Task AssertCase(ConstraintCase test,Func<ConstraintCase,Task<JsonNode?>> provider)
    {
        var input=(JsonObject)test.Input.DeepClone();var snapshot=input.ToJsonString();var actualCase=test with{Input=input};JsonNode? result=null;
        var error=await Record.ExceptionAsync(async()=>result=await provider(actualCase));
        Assert.Equal(snapshot,input.ToJsonString());
        if(test.Error is not null){Assert.NotNull(error);Assert.Equal(test.Error,error.GetType().Name);if(test.ErrorKey is not null)Assert.Equal(test.ErrorKey,error.Message);Assert.Null(result);return;}
        Assert.Null(error);Assert.True(JsonNode.DeepEquals(test.Expected,result));AssertOrder(test.Expected,result);
        var resultSnapshot=result!.ToJsonString();input["mutation-after-return"]=true;
        if(input["audit"] is JsonObject audit&&audit["hash_source"] is JsonObject hash)hash["mutation-after-return"]=true;
        if(input["facts"] is JsonArray facts)foreach(var fact in facts.OfType<JsonObject>())if(fact["source"] is JsonObject source)source["mutation-after-return"]=true;
        Assert.Equal(resultSnapshot,result.ToJsonString());
    }
    private static void AssertOrder(JsonNode? expected,JsonNode? actual)
    {
        if(expected is JsonObject obj){var other=Assert.IsType<JsonObject>(actual);Assert.Equal(obj.Select(item=>item.Key),other.Select(item=>item.Key));foreach(var item in obj)AssertOrder(item.Value,other[item.Key]);}
        else if(expected is JsonArray list){var other=Assert.IsType<JsonArray>(actual);for(var i=0;i<list.Count;i++)AssertOrder(list[i],other[i]);}
    }
    public static Task<JsonNode?> Mock(ConstraintCase test,string fault="")
    {
        if(fault=="input")test.Input["unexpected-input-mutation"]=true;
        if(fault=="wrong-error")throw test.Error==nameof(KeyNotFoundException)?new ArgumentException("wrong"):new KeyNotFoundException("wrong");
        if(fault=="wrong-key")throw new KeyNotFoundException("wrong-key");
        if(test.Error is not null&&fault!="outcome")throw test.Error==nameof(KeyNotFoundException)?new KeyNotFoundException(test.ErrorKey):new ArgumentException("Admitted container required");
        if(fault=="outcome")return Task.FromResult<JsonNode?>(new JsonObject{["unknown_reason"]="wrong"});
        var result=test.Expected!.DeepClone();
        if(fault.StartsWith("leaf:",StringComparison.Ordinal)){
            var path=fault[5..].Split('/',StringSplitOptions.RemoveEmptyEntries);
            if(path.Length==0)result=JsonValue.Create(!result.GetValue<bool>())!;
            else{JsonNode parent=result;foreach(var segment in path.SkipLast(1))parent=parent is JsonArray a?a[int.Parse(segment)]!:parent[segment]!;var key=path[^1];if(parent is JsonArray array)array[int.Parse(key)]="__wrong__";else parent[key]="__wrong__";}
        }
        if(fault=="extra"&&result is JsonObject extra)extra["invented"]=true;
        if(fault=="order"&&result is JsonObject ordered)result=new JsonObject(ordered.Reverse().Select(item=>new KeyValuePair<string,JsonNode?>(item.Key,item.Value?.DeepClone())));
        return Task.FromResult<JsonNode?>(result);
    }
    private static IEnumerable<string> Leaves(JsonNode? node,string path="")
    {if(node is JsonObject obj){foreach(var item in obj)foreach(var value in Leaves(item.Value,path+"/"+item.Key))yield return value;}else if(node is JsonArray array){for(var i=0;i<array.Count;i++)foreach(var value in Leaves(array[i],path+"/"+i))yield return value;}else yield return path;}
    public static IEnumerable<object[]> Faults
    {get{foreach(var test in All()){yield return new object[]{test,"outcome"};yield return new object[]{test,"input"};if(test.Error is not null){yield return new object[]{test,"wrong-error"};if(test.ErrorKey is not null)yield return new object[]{test,"wrong-key"};}if(test.Error is null){foreach(var leaf in Leaves(test.Expected))yield return new object[]{test,"leaf:"+leaf};if(test.Expected is JsonObject obj){yield return new object[]{test,"extra"};if(obj.Count>1)yield return new object[]{test,"order"};}}}}}
}
