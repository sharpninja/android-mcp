using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Sources.Constraints.SourceConstraintValues;

namespace AndroidMcp.BuildTool.Sources.Constraints;

public sealed class AidlSourceConstraints(ILegacyJsonValueEquality equality):IAidlSourceConstraints
{
    private readonly SourceConstraintValues values=new(equality);
    public bool StableModule(JsonObject module)
    {
        var properties=Get(module,"properties");
        if(!values.Is(Get(module,"kind"),"aidl_interface"))return false;
        var admitted=module.ContainsKey("properties")?Object(properties):new JsonObject();
        if(!values.Truth(Get(admitted,"complete")))return false;
        var flags=new List<JsonNode?>();
        foreach(var item in ArrayOrEmpty(admitted,"items")){
            var entry=Object(item);if(values.Is(Required(entry,"name"),"unstable"))flags.Add(Required(entry,"value"));
        }
        if(flags.Count==0)return true;
        if(flags.Count!=1)return false;
        var flag=Object(flags[0]);return values.Is(Get(flag,"kind"),"literal")&&Get(flag,"value") is JsonValue scalar&&scalar.TryGetValue<bool>(out var boolean)&&!boolean;
    }
    public JsonObject ExactConstraint(JsonObject source,JsonObject declaration,JsonObject audit,string publishedText,JsonObject module,JsonObject linkage)
    {
        var depth=declaration.TryGetPropertyValue("declaration_depth",out var supplied)?supplied:JsonValue.Create(0);
        if(!values.Is(Get(declaration,"declaration_kind"),"interface")||!values.Same(depth,JsonValue.Create(0))||!values.Is(Get(declaration,"parse_status"),"methods-parsed"))return Unknown("No complete direct parsed AIDL interface declaration");
        foreach(var annotation in ArrayOrEmpty(declaration,"annotations")){
            var item=Object(annotation);var name=item.TryGetPropertyValue("name",out var value)?Text(value):"";
            if(name.Split('.').Last()=="Descriptor")return Unknown("Descriptor annotation requires explicit compiler descriptor semantics");
        }
        var descriptor=String(Get(declaration,"descriptor"));
        if(descriptor is null||!Qualified(descriptor))return Unknown("No qualified AIDL descriptor");
        var identity=Identity(source);
        if(!ArrayOrEmpty(audit,"source_inputs").Any(item=>values.Same(identity,item)))return Unknown("Exact declaration is not in reconciled hash input set");
        if(new[]{"repository","revision"}.Any(key=>!values.Same(Required(source,key),Get(audit,key))))return Unknown("Frozen hash audit revision differs");
        var result=ObjectOrEmpty(audit,"result");
        if(!values.Truth(Get(result,"verified")))return Unknown("Frozen dump hash did not verify");
        if(!values.Is(Get(linkage,"status"),"frozen-dump-declared-version-linked"))return Unknown("Frozen module version not uniquely linked");
        if(!StableModule(module))return Unknown("Stable AIDL build flag is unresolved or unstable");
        var moduleSource=ObjectOrEmpty(module,"source");
        if(new[]{"repository","revision"}.Any(key=>!values.Same(Required(source,key),Get(moduleSource,key))))return Unknown("Build declaration revision differs");
        var lines=SourceText.Lines(publishedText).ToArray();var selected=lines.Length==0?"":SourceText.Strip(lines[^1].Raw);
        var expected=Required(result,"computed_hash");
        if(!values.Same(JsonValue.Create(selected),expected))return Unknown("Backend tail -1 hash selection differs from verified hash");
        var output=new JsonObject{["contract"]="exact-aidl-source-constraint/1",["clause"]=Clause(descriptor,expected),["frozen_version_literal"]=Required(result,"version_literal")?.DeepClone(),
            ["source_evidence"]=new JsonArray(identity,Required(audit,"hash_source")?.DeepClone(),Required(module,"source")?.DeepClone())};
        Limits(output);output["source_hash_verified"]=true;output["backend_selection"]="pinned aidl_gen_rule.go --hash tail -1";
        output["limitations"]=new JsonArray("Future trusted descriptor/hash reachability facts required","Type dependencies, OS/client grants and codecs remain separate");return output;
    }
}
