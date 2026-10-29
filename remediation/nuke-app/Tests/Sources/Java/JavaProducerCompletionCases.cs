using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// Exact original row/errors responsibilities. Raw node ranges/field spellings
// were independently inspected through the pinned parser, never the visitor.
public static class JavaProducerCompletionCases
{
    private const string NativeParser="tree-sitter-java0.23.5 / tree-sitter0.25.2; csharp-native-v1";
    private const string NativeSha="058665f4d41f23e407e2920930ee92888404d726d450fbfd0b33730e484b7e0f";
    private static JsonObject Metadata(string package="",JsonObject? imports=null,JsonObject? excluded=null,JsonArray? issues=null)=>
        Object(("package",package),("imports",imports??new()),("wildcard_imports",new JsonArray()),("parse_errors",issues?.Count??0),
            ("issues",issues??new()),("excluded_implementation_nodes",excluded??new()),("parser",NativeParser),("native_asset_sha256",NativeSha),("language_abi",14));
    private static JsonObject Annotation(string spelling,string name,string resolution,string args="",string? resolved=null,int start=0)=>
        Object(("name",name),("arguments",resolved??args),("raw","@"+spelling+(args.Length==0?"":"("+args+")")),
            ("start",start),("end",start+1+spelling.Length+(args.Length==0?0:args.Length+2)),("name_resolution",resolution),
            ("source_spelling",spelling),("source_raw","@"+spelling+(args.Length==0?"":"("+args+")")),("source_arguments",args));
    private static JsonObject Row(string id,string kind,string canonical,string? owner,string package,string name,string raw,int start,int end,
        string? binary,string visibility="package",string[]? modifiers=null,string? returned=null,JsonArray? parameters=null,JsonArray? annotations=null,JsonArray? all=null,JsonObject? extra=null)
    {
        var extras=Object(("source_path",id+".java"),("source_byte_range",new JsonArray(start,end)),("modifiers",modifiers??[]),
            ("effective_visibility",visibility),("owner_binary_name",binary),("public_sdk_membership","unknown until checked against published SDK metadata"));
        if(extra is not null)foreach(var field in extra)extras[field.Key]=field.Value?.DeepClone();
        return Object(("kind",kind),("canonical",canonical),("owner",owner),("package",package),("name",name),("return_type",returned),
            ("parameters",parameters??new()),("annotations",annotations??new()),("all_annotations",all??annotations??new JsonArray()),("raw",raw),("line",1),("extras",extras));
    }
    private static JsonObject TypeExtra(string declaration="class_declaration",string? ownerKind=null,string parameters="",string superclass="",string interfaces="")=>
        Object(("owner_kind",ownerKind),("declaration_kind",declaration),("type_parameters",parameters),("superclass",superclass),("interfaces",interfaces));
    private static JsonObject MethodExtra(string ownerKind="class",string parameters="",bool constructor=false,string value="",JsonArray? receivers=null)=>
        Object(("owner_kind",ownerKind),("type_parameters",parameters),("receiver_parameters",receivers??new()),("default_value_raw",value),("source_declared_constructor",constructor));
    private static JsonObject Parameter(string type,string name,int start,int end,JsonArray? annotations=null)=>
        Object(("type",type),("name",name),("direction",null),("annotations",annotations??new()),("source_type",type),("source_byte_range",new JsonArray(start,end)));
    private static JsonObject Unsupported(string id,string reason,int start,int end,string excerpt)=>
        Object(("kind","unsupported"),("canonical","unparsed-java:"+id+".java:["+start+", "+end+"]:"+ContentHash.Sha256(Encoding.UTF8.GetBytes(excerpt))),
            ("owner",null),("package",""),("name","unparsed"),("return_type",null),("parameters",new JsonArray()),("annotations",new JsonArray()),
            ("all_annotations",new JsonArray()),("raw",excerpt),("line",1),("extras",Object(("parse_error",reason),("source_byte_range",new JsonArray(start,end)))));
    private static JsonObject Issue(string reason,int start,int end,string excerpt)=>Object(("reason",reason),("line",1),("byte_range",new JsonArray(start,end)),("raw_excerpt",excerpt),("source_bytes_retained",true));
    private static JavaProducerBoundaryCase Error(string id,string text,string excerpt,int bytes)=>new(id,text,[Unsupported(id,"Java AST ERROR",0,bytes,excerpt)],
        Metadata(excluded:Object(("ERROR",1)),issues:Array(Issue("Java AST ERROR",0,bytes,excerpt))),[new("java-ast-error","Java AST ERROR",0,bytes)]);
    private static JavaProducerBoundaryCase ErrorLimit(int count)
    {
        var scalar=char.ConvertFromUtf32(0x1f600);var text=string.Concat(Enumerable.Repeat(scalar,count));var excerpt=string.Concat(Enumerable.Repeat(scalar,4096));
        Assert.Equal(count,text.EnumerateRunes().Count());Assert.Equal(4096,excerpt.EnumerateRunes().Count());
        return Error("JAVA-COMPLETE-error"+count,text,excerpt,count*4);
    }
    private static JavaProducerBoundaryCase Missing()
    {
        const string id="JAVA-COMPLETE-missing";const string reason="Java AST missing token";
        return new(id,"class C { int x }",[
            Row(id,"class","C","C","","C","class C",0,17,"C",extra:TypeExtra()),
            Row(id,"field","C#x","C","","x","int x",10,15,"C",returned:"int",extra:Object(("owner_kind","class"),("constant_raw",""),("initializer_source_range",null),("enum_constant",false))),
            Unsupported(id,reason,15,15,"")],Metadata(issues:Array(Issue(reason,15,15,""))),[new("java-ast-error",reason,15,15)]);
    }
    private static JavaProducerBoundaryCase OrderedErrors()
    {
        const string id="JAVA-COMPLETE-ordered-errors";const string reason="Java AST ERROR";
        return new(id,"? class C {} ?",[Row(id,"class","C","C","","C","class C",2,12,"C",extra:TypeExtra()),Unsupported(id,reason,0,1,"?"),Unsupported(id,reason,13,14,"?")],
            Metadata(excluded:Object(("ERROR",2)),issues:Array(Issue(reason,0,1,"?"),Issue(reason,13,14,"?"))),[new("java-ast-error",reason,0,1),new("java-ast-error",reason,13,14)]);
    }
    private static JavaProducerBoundaryCase ClassHeader()
    {
        const string id="JAVA-COMPLETE-class-header";const string header="@Deprecated public abstract class C<T extends Number> extends Base implements I, J";
        var deprecated=Array(Annotation("Deprecated","java.lang.Deprecated","java-lang"));
        return new(id,"package p; "+header+" {}",[
            Row(id,"package","p",null,"p","p","package p;",0,10,null,extra:Object(("declaration_kind","package"))),
            Row(id,"class","p.C","p.C","p","C",header,11,96,"p/C","public",["public","abstract"],annotations:deprecated,
                extra:TypeExtra(parameters:"<T extends Number>",superclass:"extends Base",interfaces:"implements I, J"))],Metadata("p"),[]);
    }
    private static JavaProducerBoundaryCase MemberHeader()
    {
        const string id="JAVA-COMPLETE-member-header";const string header="@Override public <U> U f(@Deprecated final T value)";
        var methodAnnotation=Array(Annotation("Override","java.lang.Override","java-lang"));
        var parameterAnnotation=Array(Annotation("Deprecated","java.lang.Deprecated","java-lang"));
        return new(id,"class C { "+header+" { return null; } protected <V> C(V v) {} }",[
            Row(id,"class","C","C","","C","class C",0,104,"C",extra:TypeExtra()),
            Row(id,"method","C#f(T)","C","","f",header,10,78,"C","public",["public"],"U",Array(Parameter("T","value",35,60,parameterAnnotation)),methodAnnotation,
                Array(Annotation("Override","java.lang.Override","java-lang"),Annotation("Deprecated","java.lang.Deprecated","java-lang",start:25)),MethodExtra(parameters:"<U>")),
            Row(id,"constructor","C#<init>(V)","C","","C","protected <V> C(V v)",79,102,"C","protected",["protected"],parameters:Array(Parameter("V","v",95,98)),extra:MethodExtra(parameters:"<V>",constructor:true))],Metadata(),[]);
    }
    private static JavaProducerBoundaryCase AnnotationHeader()
    {
        const string id="JAVA-COMPLETE-annotation-header";
        return new(id,"public @interface Mark { String value() default \"x\"; }",[
            Row(id,"annotation","Mark","Mark","","Mark","public @interface Mark",0,54,"Mark","public",["public"],extra:TypeExtra("annotation_type_declaration")),
            Row(id,"method","Mark#value()","Mark","","value","String value() default \"x\";",25,52,"Mark","public",returned:"String",extra:MethodExtra("annotation",value:"\"x\""))],Metadata(),[]);
    }
    private static JavaProducerBoundaryCase InterfaceHeader()
    {
        const string id="JAVA-COMPLETE-interface-header";
        return new(id,"public interface I<T> extends A, B { void f(I<T> this); int X=1; }",[
            Row(id,"interface","I","I","","I","public interface I<T> extends A, B",0,66,"I","public",["public"],extra:TypeExtra("interface_declaration",parameters:"<T>")),
            Row(id,"method","I#f()","I","","f","void f(I<T> this);",37,55,"I","public",returned:"void",extra:MethodExtra("interface",receivers:Array(JsonValue.Create("I<T> this")))),
            Row(id,"field","I#X","I","","X","int X=1;",56,64,"I","public",returned:"int",extra:Object(("owner_kind","interface"),("constant_raw","1"),("initializer_source_range",new JsonArray(62,63)),("enum_constant",false)))],Metadata(),[]);
    }
    public static readonly IReadOnlyList<JavaProducerBoundaryCase> All=[Error("JAVA-COMPLETE-error-short","?","?",1),ErrorLimit(4096),ErrorLimit(4097),Missing(),OrderedErrors(),ClassHeader(),MemberHeader(),AnnotationHeader(),InterfaceHeader()];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaProducerBoundaryCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaProducerBoundaryCase test,ISourceExtractor provider)
    {
        ExtractionResult? observed=null;
        JavaProducerBoundaryCases.AssertContract(test,new ScriptedSourceExtractor(provider.Kind,source=>{var result=provider.Extract(source);observed=result;return result;}));
        Assert.NotNull(observed);Assert.True(JsonNode.DeepEquals(test.Metadata,observed.Metadata),"Complete parser report metadata");
        for(var index=0;index<test.Rows.Length;index++)Assert.True(JsonNode.DeepEquals(test.Rows[index],observed.Records[index]),"Complete original row "+index);
    }
}
