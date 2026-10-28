using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources.Java;

public sealed class JavaSourceExtractor(JavaNativeParser parser) : ISourceExtractor
{
    public string Kind=>"java-source";
    public ExtractionResult Extract(SourceDocument source)=>parser.WithTree(source.Bytes,tree=>new Visitor(source,tree,parser).Run());

    private sealed class Visitor(SourceDocument source,JavaNativeParser.SyntaxTree tree,JavaNativeParser parser)
    {
        private static readonly IReadOnlyDictionary<string,string> TypeKinds=new Dictionary<string,string>(StringComparer.Ordinal)
        { ["class_declaration"]="class",["interface_declaration"]="interface",["enum_declaration"]="enum",["annotation_type_declaration"]="annotation",["record_declaration"]="class" };
        private static readonly HashSet<string> JavaLang=new("String Object Class Throwable Exception RuntimeException Error Enum Annotation Deprecated Override SuppressWarnings SafeVarargs FunctionalInterface Boolean Byte Character Short Integer Long Float Double Void Number CharSequence StringBuilder StringBuffer Iterable Comparable Cloneable AutoCloseable".Split(' '),StringComparer.Ordinal);
        private static readonly HashSet<string> AndroidAnnotations=new("RequiresPermission EnforcePermission RequiresNoPermission SystemApi TestApi FlaggedApi Nullable NonNull".Split(' '),StringComparer.Ordinal);
        private readonly List<JsonObject> records=[];private readonly List<SourceIssue> issues=[];private readonly List<JsonObject> issueDetails=[];
        private readonly Dictionary<string,string> imports=new(StringComparer.Ordinal),binaryByOwner=new(StringComparer.Ordinal);
        private readonly List<string> wildcards=[];private readonly Dictionary<string,int> excluded=new(StringComparer.Ordinal);private string package="";
        private string Text(JavaNativeParser.Node node)=>tree.Text(node);
        private string Kind(JavaNativeParser.Node node)=>tree.Kind(node);
        private JsonArray Range(JavaNativeParser.Node node)=>new(tree.Start(node),tree.End(node));
        internal ExtractionResult Run()
        {
            var children=tree.Children(tree.Root).ToArray();
            foreach(var node in children)
            {
                if(Kind(node)=="package_declaration")package=Text(tree.Children(node).LastOrDefault(x=>Kind(x) is "identifier" or "scoped_identifier"));
                else if(Kind(node)=="import_declaration")
                {
                    var value=SourceSyntax.Rx(@"^import\s+(?:static\s+)?|;\s*$").Replace(Text(node),"").Trim();
                    if(value.EndsWith(".*",StringComparison.Ordinal))wildcards.Add(value[..^2]);else imports[value[(value.LastIndexOf('.')+1)..]]=value;
                }
            }
            if(package.Length>0)records.Add(Row(children.First(x=>Kind(x)=="package_declaration"),"package",null,package,canonical:package,extra:new(){["declaration_kind"]="package"}));
            foreach(var node in children)
            {
                if(TypeKinds.ContainsKey(Kind(node)))Type(node,null,0);
                else if(Kind(node) is not ("package_declaration" or "import_declaration" or "line_comment" or "block_comment"))Exclude(Kind(node));
            }
            var pending=new Stack<JavaNativeParser.Node>();pending.Push(tree.Root);
            while(pending.TryPop(out var node))
            {
                if(Kind(node)=="ERROR" || tree.Missing(node))Issue(node,tree.Missing(node)?"Java AST missing token":"Java AST ERROR");
                else if(tree.HasError(node))foreach(var child in tree.Children(node,false).Reverse())pending.Push(child);
            }
            foreach(var issue in issueDetails)
            {
                var range=issue["byte_range"]!.AsArray();var raw=issue["raw_excerpt"]?.GetValue<string>()??"";
                var locator="["+range[0]+", "+range[1]+"]";
                records.Add(SourceSyntax.Row("unsupported","unparsed-java:"+source.RelativePath+":"+locator+":"+ContentHash.Sha256(Encoding.UTF8.GetBytes(raw)),null,package,"unparsed",raw,
                    issue["line"]!.GetValue<int>(),extras:new(){["parse_error"]=issue["reason"]!.DeepClone(),["source_byte_range"]=range.DeepClone(),["source_bytes_retained"]=true}));
            }
            var metadata=new JsonObject{
                ["package"]=package,["imports"]=new JsonObject(imports.Select(x=>KeyValuePair.Create<string,JsonNode?>(x.Key,JsonValue.Create(x.Value)))),
                ["wildcard_imports"]=SourceSyntax.Strings(wildcards),["parse_errors"]=issues.Count,["issues"]=SourceSyntax.Array(issueDetails),
                ["excluded_implementation_nodes"]=new JsonObject(excluded.Select(x=>KeyValuePair.Create<string,JsonNode?>(x.Key,JsonValue.Create(x.Value)))),
                ["parser"]="tree-sitter-java0.23.5 / tree-sitter0.25.2; csharp-native-v1",["native_asset_sha256"]=parser.ArtifactSha256,["language_abi"]=parser.LanguageVersion };
            return new(records,issues,issues.Count==0?StructuralParseStatus.Clean:StructuralParseStatus.Malformed,metadata);
        }
        private void Exclude(string kind)=>excluded[kind]=excluded.GetValueOrDefault(kind)+1;
        private void Issue(JavaNativeParser.Node node,string reason)
        {
            var raw=string.Concat(Text(node).EnumerateRunes().Take(4096).Select(x=>x.ToString()));
            issues.Add(new("java-ast-error",reason,tree.Start(node),tree.End(node)));
            issueDetails.Add(new(){["reason"]=reason,["line"]=tree.Line(node),["byte_range"]=Range(node),["raw_excerpt"]=raw,["source_bytes_retained"]=true});
        }
        private JsonArray Annotations(string value,JavaNativeParser.Node node)
        {
            AnnotatedSource parsed;
            try{parsed=SourceSyntax.ParseAnnotations(SourceSyntax.Mask(value,false));}
            catch(FormatException error){Issue(node,"Java annotation metadata: "+error.Message);return new();}
            var result=new JsonArray();
            foreach(var annotation in parsed.Annotations)
            {
                var item=(JsonObject)annotation.Document.DeepClone();var raw=item["name"]!.GetValue<string>();var dot=raw.IndexOf('.');var first=dot<0?raw:raw[..dot];
                if(imports.TryGetValue(first,out var full)){item["name"]=full+(dot<0?"":raw[dot..]);item["name_resolution"]="explicit-import";}
                else if(dot>=0)item["name_resolution"]="qualified-source-spelling";
                else if(JavaLang.Contains(raw)){item["name"]="java.lang."+raw;item["name_resolution"]="java-lang";}
                else if(wildcards.Contains("android.annotation",StringComparer.Ordinal)&&AndroidAnnotations.Contains(raw)){item["name"]="android.annotation."+raw;item["name_resolution"]="known-annotation-wildcard";}
                else{item["name"]=(package.Length>0?package+".":"")+raw;item["name_resolution"]="package-candidate; unresolved wildcard imports retained";}
                item["source_spelling"]=raw;item["source_raw"]=value[annotation.Start..annotation.End];
                var args=item["arguments"]!.GetValue<string>();item["source_arguments"]=args;
                foreach(var import in imports)args=SourceSyntax.Rx(@"(?<![\w.$])"+Regex.Escape(import.Key)+@"(?=\.)").Replace(args,_=>import.Value);
                item["arguments"]=args;result.Add(item);
            }
            return result;
        }
        private (string[] Modifiers,JsonArray Annotations) Modifiers(JavaNativeParser.Node node)
        {
            var value=Text(tree.Children(node).FirstOrDefault(x=>Kind(x)=="modifiers"));var masked=SourceSyntax.Mask(value,false);string clean;
            try{clean=SourceSyntax.ParseAnnotations(masked).Masked;}catch(FormatException){clean=SourceSyntax.Mask(masked,true);}
            return (SourceSyntax.Rx(@"\b(public|protected|private|static|final|abstract|native|synchronized|strictfp|default|transient|volatile|sealed|non-sealed)\b").Matches(clean).Select(x=>x.Value).ToArray(),Annotations(value,node));
        }
        private string TypeText(JavaNativeParser.Node node,string extra="")
        {
            var clean=SourceSyntax.TypeName(Text(node)+extra);
            foreach(var import in imports)
                if(!import.Value.StartsWith("java.lang.",StringComparison.Ordinal))clean=SourceSyntax.Rx(@"(?<![\w.$])"+Regex.Escape(import.Key)+@"(?![\w$])").Replace(clean,_=>import.Value);
            return clean;
        }
        private JsonObject Parameter(JavaNativeParser.Node node)
        {
            var type=tree.Field(node,"type");var name=tree.Field(node,"name");
            if(Kind(node)=="spread_parameter")
            {
                if(type.IsNull)type=tree.Children(node).FirstOrDefault(x=>Kind(x) is not ("modifiers" or "variable_declarator" or "annotation" or "marker_annotation"));
                var declaration=tree.Children(node).FirstOrDefault(x=>Kind(x)=="variable_declarator");if(name.IsNull)name=tree.Field(declaration,"name");
            }
            var dimensions=string.Concat(tree.Children(node).Where(x=>Kind(x)=="dimensions").Select(Text));
            return new(){["type"]=TypeText(type,(Kind(node)=="spread_parameter"?"...":"")+dimensions),["name"]=name.IsNull?null:Text(name),["direction"]=null,
                ["annotations"]=Annotations(Text(node),node),["source_type"]=Text(type),["source_byte_range"]=Range(node)};
        }
        private JsonObject Row(JavaNativeParser.Node node,string kind,string? owner,string name,string? returnType=null,JsonArray? parameters=null,string? canonical=null,JsonArray? annotations=null,JsonObject? extra=null,string? raw=null)
        {
            var (modifiers,metadata)=Modifiers(node);parameters??=new();var body=tree.Field(node,"body");
            var header=raw??(body.IsNull?Text(node):tree.Slice(tree.Start(node),tree.Start(body))).Trim();extra??=new();
            var visibility=modifiers.FirstOrDefault(x=>x is "public" or "protected" or "private")??
                (extra["owner_kind"]?.GetValue<string>() is "interface" or "annotation" && kind is "method" or "field" ? "public":"package");
            var extras=new JsonObject{["source_path"]=source.RelativePath,["source_byte_range"]=Range(node),["modifiers"]=SourceSyntax.Strings(modifiers),["effective_visibility"]=visibility,
                ["owner_binary_name"]=owner is not null?binaryByOwner.GetValueOrDefault(owner):null,["public_sdk_membership"]="unknown until checked against published SDK metadata"};
            foreach(var field in extra)extras[field.Key]=field.Value?.DeepClone();
            var row=SourceSyntax.Row(kind,canonical??owner+"#"+name,owner,package,name,header,tree.Line(node),returnType,parameters,extras:extras);
            row["annotations"]=(annotations??metadata).DeepClone();row["all_annotations"]=Annotations(header,node);return row;
        }
        private void Type(JavaNativeParser.Node node,string? outer,int depth)
        {
            if(depth>128){Issue(node,"Nested type depth exceeds128");return;}
            var name=Text(tree.Field(node,"name"));var owner=outer is null?(package.Length>0?package+".":"")+name:outer+"."+name;var kind=TypeKinds[Kind(node)];
            binaryByOwner[owner]=outer is null?(package.Length>0?package.Replace('.','/')+"/":"")+name:binaryByOwner[outer]+"$"+name;
            records.Add(Row(node,kind,owner,name,canonical:owner,extra:new(){["declaration_kind"]=Kind(node),["type_parameters"]=Text(tree.Field(node,"type_parameters")),
                ["superclass"]=Text(tree.Field(node,"superclass")),["interfaces"]=Text(tree.Field(node,"interfaces"))}));
            foreach(var child in tree.Children(tree.Field(node,"body")))Child(child,owner,kind,depth);
        }
        private void Child(JavaNativeParser.Node node,string owner,string ownerKind,int depth)
        {
            var nodeKind=Kind(node);
            if(TypeKinds.ContainsKey(nodeKind)){Type(node,owner,depth+1);return;}
            if(nodeKind=="enum_body_declarations"){foreach(var child in tree.Children(node))Child(child,owner,ownerKind,depth);return;}
            if(nodeKind is "method_declaration" or "constructor_declaration" or "compact_constructor_declaration" or "annotation_type_element_declaration")
            {
                var constructor=nodeKind is "constructor_declaration" or "compact_constructor_declaration";var name=Text(tree.Field(node,"name"));var parameters=new JsonArray();var receivers=new List<string>();
                foreach(var parameter in tree.Children(tree.Field(node,"parameters")))
                    if(Kind(parameter) is "formal_parameter" or "spread_parameter")parameters.Add(Parameter(parameter));else if(Kind(parameter)=="receiver_parameter")receivers.Add(Text(parameter));
                var canonical=owner+"#"+(constructor?"<init>":name)+"("+string.Join(',',parameters.Select(x=>x!["type"]!.GetValue<string>()))+")";
                records.Add(Row(node,constructor?"constructor":"method",owner,name,constructor?null:TypeText(tree.Field(node,"type")),parameters,canonical,Modifiers(node).Annotations,
                    new(){["owner_kind"]=ownerKind,["type_parameters"]=Text(tree.Field(node,"type_parameters")),["receiver_parameters"]=SourceSyntax.Strings(receivers),
                        ["default_value_raw"]=Text(tree.Field(node,"value")),["source_declared_constructor"]=constructor}));return;
            }
            if(nodeKind is "field_declaration" or "constant_declaration")
            {
                var type=tree.Field(node,"type");var annotations=Modifiers(node).Annotations;
                foreach(var declaration in tree.Children(node).Where(x=>Kind(x)=="variable_declarator"))
                {
                    var name=Text(tree.Field(declaration,"name"));var value=tree.Field(declaration,"value");var valueRaw=Text(value);
                    var dimensions=string.Concat(tree.Children(declaration).Where(x=>Kind(x)=="dimensions").Select(Text));var raw=Text(node);
                    var extra=new JsonObject{["owner_kind"]=ownerKind,["constant_raw"]=valueRaw.EnumerateRunes().Take(4097).Count()<=4096?valueRaw:null,["initializer_source_range"]=value.IsNull?null:Range(value),["enum_constant"]=false};
                    if(raw.EnumerateRunes().Take(16385).Count()>16384){raw=TypeText(type)+" "+name+" [initializer retained at source byte range]";extra["raw_signature_is_excerpt"]=true;}
                    records.Add(Row(node,"field",owner,name,TypeText(type,dimensions),annotations:annotations,extra:extra,raw:raw));
                }
                return;
            }
            if(nodeKind=="enum_constant"){var name=Text(tree.Field(node,"name"));records.Add(Row(node,"field",owner,name,owner,extra:new(){["owner_kind"]="enum",["enum_constant"]=true,["effective_visibility"]="public"}));}
            else if(nodeKind is not ("line_comment" or "block_comment"))Exclude(nodeKind);
        }
    }
}
