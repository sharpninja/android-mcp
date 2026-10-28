using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record ProjectionField(string Name,string Type,string? Constant=null);
public sealed record ProjectionVariant(ProjectionField[] Fields,string Header="parcelable Data",JsonObject? Extra=null);
public sealed record ScopedProjectionInput(string Language,string Kind,ProjectionVariant[] Variants,JsonObject? Extra=null);
public sealed record ProjectedShape(string[] Names,string[] TypeTexts,string?[] Constants,int?[] Order,string OrderStatus,
    string? HidlKind=null,string? Storage=null,string? Target=null);
public sealed record ScopedProjectionReply(ProjectedShape[] Shapes,string[] Digests);
public sealed record ScopedProjectionCase(string Id,ScopedProjectionInput Input,ProjectedShape[] Shapes,bool EqualHashes);
public interface IScopedProjectionProvider { ScopedProjectionReply Project(ScopedProjectionInput input); }
public static class ScopedProjectionCases
{
    public static IEnumerable<object[]> All=>Generate().Select(value=>new object[]{value});
    private static ProjectedShape Shape(ProjectionField[] fields,string? hidl=null,string? storage=null,string? target=null)=>
        new(fields.Select(field=>field.Name).ToArray(),fields.Select(field=>field.Type).ToArray(),fields.Select(field=>field.Constant).ToArray(),
            fields.Select((_,index)=>(int?)index).ToArray(),"source-declared",hidl,storage,target);
    public static IEnumerable<ScopedProjectionCase> Generate()
    {
        ProjectionField[] first=[new("x","int")],second=[new("x","long")];
        yield return new("SCOPE-01-versioned-fields-not-combined",new("aidl","aidl-parcelable",[new(first),new(second)]),[Shape(first),Shape(second)],false);
        first=[new("x","int32_t")];second=[new("x","int64_t")];
        yield return new("SCOPE-02-hidl-field-semantic-change",new("hidl","type",[new(first),new(second)],new(){["hidl_declaration_kind"]="struct"}),[Shape(first,"struct"),Shape(second,"struct")],false);
        first=[new("x","int"),new("y","long")];second=[new("y","long"),new("x","int")];
        yield return new("SCOPE-03-field-source-order-semantic-change",new("aidl","aidl-parcelable",[new(first),new(second)]),[Shape(first),Shape(second)],false);
        yield return new("SCOPE-04-hidl-typedef-target-change",new("hidl","type",[new([],Extra:new(){["target_type_raw"]="int32_t"}),new([],Extra:new(){["target_type_raw"]="int64_t"})],new(){["hidl_declaration_kind"]="typedef"}),
            [Shape([],"typedef",target:"int32_t"),Shape([],"typedef",target:"int64_t")],false);
        yield return new("SCOPE-05-hidl-enum-storage-change",new("hidl","enum",[new([],Extra:new(){["storage_type_raw"]=":uint32_t"}),new([],Extra:new(){["storage_type_raw"]=":uint64_t"})],new(){["hidl_declaration_kind"]="enum"}),
            [Shape([],"enum",":uint32_t"),Shape([],"enum",":uint64_t")],false);
        first=[new("A","p@1.0::Data","1")];second=[new("A","p@1.0::Data","2")];
        yield return new("SCOPE-06-hidl-enum-expression-change",new("hidl","enum",[new(first),new(second)],new(){["hidl_declaration_kind"]="enum"}),[Shape(first,"enum"),Shape(second,"enum")],false);
        first=[new("x","int")];
        yield return new("SCOPE-07-raw-header-comment-not-semantic",new("aidl","aidl-parcelable",[new(first),new(first,"/* comment and no layout change */ parcelable Data")]),[Shape(first),Shape(first)],true);
    }
    public static void AssertCase(ScopedProjectionCase test,IScopedProjectionProvider provider)
    {
        var before=JsonSerializer.Serialize(test.Input);var result=provider.Project(test.Input);Assert.Equal(test.Shapes.Length,result.Shapes.Length);Assert.Equal(test.Shapes.Length,result.Digests.Length);
        for(var index=0;index<test.Shapes.Length;index++)
        {
            var expected=test.Shapes[index];var actual=result.Shapes[index];
            Assert.Equal(expected.Names,actual.Names);Assert.Equal(expected.TypeTexts,actual.TypeTexts);Assert.Equal(expected.Constants,actual.Constants);
            Assert.Equal(expected.Order,actual.Order);Assert.Equal(expected.OrderStatus,actual.OrderStatus);Assert.Equal(expected.HidlKind,actual.HidlKind);
            Assert.Equal(expected.Storage,actual.Storage);Assert.Equal(expected.Target,actual.Target);Assert.Matches("^[0-9a-f]{64}$",result.Digests[index]);
        }
        Assert.Equal(test.EqualHashes,result.Digests[0]==result.Digests[1]);
        Assert.Equal(before,JsonSerializer.Serialize(test.Input));
    }
    // Exact legacy-shaped source/occurrence inputs. Source IDs deliberately stay
    // distinct while repository/revision/platform snapshot metadata is shared.
    public static (Dictionary<string,JsonObject> Sources,List<(string SourceId,JsonObject Document)> Rows,List<(string SourceId,JsonObject Document)> Types) Fixture(ScopedProjectionInput input)
    {
        var owner=input.Language=="aidl" ? "p.Data" : "p@1.0::Data";var signature=input.Language+":"+owner;
        var identity=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        var sources=new Dictionary<string,JsonObject>(StringComparer.Ordinal);var rows=new List<(string,JsonObject)>();var types=new List<(string,JsonObject)>();
        for(var index=0;index<input.Variants.Length;index++)
        {
            var variant=input.Variants[index];var source="source-"+index;
            sources.Add(source,new(){["source_id"]=source,["repository"]="fixture",["revision"]="same-pinned-commit",["kind"]=input.Language=="aidl" ? "aidl" : "evidence",["api_level"]=null,["channel"]="unknown",["path"]="aidl_api/module/"+(index+1)+"/p/Data.aidl"});
            var extra=new JsonObject{["language"]=input.Language};
            foreach(var additions in new[]{input.Extra,variant.Extra}) if(additions is not null) foreach(var value in additions) extra[value.Key]=value.Value?.DeepClone();
            var type=new JsonObject{["entry_id"]=identity,["canonical_signature"]=signature,["kind"]=input.Kind,["name"]="Data",["owner"]=owner,["package"]=input.Language=="aidl" ? "p" : "p@1.0",
                ["arguments"]=new JsonArray(),["codec_status"]="unknown",["extensions"]=extra,["provenance"]=new JsonArray(new JsonObject{["raw_signature"]=variant.Header,["line"]=1})};
            rows.Add((source,type));types.Add((source,type));
            for(var order=0;order<variant.Fields.Length;order++)
            {
                var field=variant.Fields[order];var fieldExtra=new JsonObject{["language"]=input.Language,["aidl_declaration_kind"]=input.Language=="aidl" ? "field" : null,["hidl_declaration_kind"]="field",["source_character_span"]=new JsonArray(order*20,order*20+10)};
                if(field.Constant is not null) fieldExtra["constant_expression_raw"]=field.Constant;
                rows.Add((source,new(){["entry_id"]="field-"+field.Name,["canonical_signature"]=signature+"#"+field.Name,["kind"]="field",["name"]=field.Name,["owner"]=owner,["package"]=type["package"]!.DeepClone(),["arguments"]=new JsonArray(),
                    ["return_type"]=new JsonObject{["text"]=field.Type,["status"]="unknown",["codec_status"]="unknown"},["extensions"]=fieldExtra,["provenance"]=new JsonArray(new JsonObject{["raw_signature"]=field.Type+" "+field.Name,["line"]=order+2})}));
            }
        }
        return(sources,rows,types);
    }
}
public sealed class ScriptedScopedProjection(ScopedProjectionCase test,int field=-1):IScopedProjectionProvider
{
    public ScopedProjectionReply Project(ScopedProjectionInput input)
    {
        Assert.Same(test.Input,input);var shapes=test.Shapes.Select(shape=>shape with{}).ToArray();var digests=new[]{new string('a',64),new string(test.EqualHashes ? 'a' : 'b',64)};
        if(field==10) input.Variants[0]=input.Variants[0] with{Header="wrong input mutation"};
        else if(field==8) digests[1]=test.EqualHashes ? new string('b',64) : digests[0];
        else if(field==9) digests[0]="wrong";
        else if(field>=0) shapes[0]=field switch{0=>shapes[0] with{Names=["wrong"]},1=>shapes[0] with{TypeTexts=["wrong"]},2=>shapes[0] with{Constants=["wrong"]},
            3=>shapes[0] with{Order=[99]},4=>shapes[0] with{OrderStatus="wrong"},5=>shapes[0] with{HidlKind="wrong"},6=>shapes[0] with{Storage="wrong"},_=>shapes[0] with{Target="wrong"}};
        return new(shapes,digests);
    }
}
[Trait("Gate","Mock")]
public sealed class ScopedProjectionMockTests
{
    [Theory,MemberData(nameof(ScopedProjectionCases.All),MemberType=typeof(ScopedProjectionCases))]
    public void CorrectRepliesPass(ScopedProjectionCase test)=>ScopedProjectionCases.AssertCase(test,new ScriptedScopedProjection(test));
    [Theory,MemberData(nameof(ScopedProjectionCases.All),MemberType=typeof(ScopedProjectionCases))]
    public void EachShapeAndDigestMutationRejected(ScopedProjectionCase test)
    {for(var field=0;field<11;field++) Assert.NotNull(Record.Exception(()=>ScopedProjectionCases.AssertCase(test,new ScriptedScopedProjection(test,field))));}
}
