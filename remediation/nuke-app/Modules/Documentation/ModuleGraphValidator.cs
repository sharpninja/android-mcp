using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Validates the proposed design/import graph, not an Android runtime.</summary>
public sealed class ModuleGraphValidator : IModuleGraphValidator
{
    private static readonly Regex Declaration=new("^(?:class|interface|enum) (\\w+)",RegexOptions.Multiline|RegexOptions.CultureInvariant);
    private static readonly Regex Interface=new("^interface (\\w+)",RegexOptions.Multiline|RegexOptions.CultureInvariant);
    private static readonly Regex Relationship=new("^(\\w+)\\s+(?:\"[^\"\\r\\n]+\"\\s+)?(?:\\.\\.[|]?>|-->|\\*--|o--|--)(?:\\s+\"[^\"\\r\\n]+\")?\\s+(\\w+)",RegexOptions.CultureInvariant);
    private static readonly Regex View=new("## ([a-z-]+)[^\\r\\n]*\\r?\\n[\\s\\S]*?```plantuml\\r?\\n([\\s\\S]*?)\\r?\\n```",RegexOptions.CultureInvariant);

    public ModuleValidationResult Validate(ModuleValidationInput input)
    {
        var owners=input.ModuleContract["classes"]?.AsObject()??throw new InvalidDataException("Module class ownership map missing");
        var dependencies=input.ModuleContract["dependencies"]?.AsObject()??throw new InvalidDataException("Module dependency map missing");
        var names=Declaration.Matches(input.CodeMarkdown).Select(m=>m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        var issues=new List<DesignIssue>();var edges=new HashSet<string>(StringComparer.Ordinal);
        void Issue(string code,string subject)=>issues.Add(new(code,code=="MODULE_CYCLE"?"architecture/module-contract.json":"architecture/code-diagrams.md",subject));
        string? Owner(string name)=>owners[name]?.GetValue<string>();
        string[] Targets(string module)=>dependencies[module] is JsonArray values?values.Select(n=>n!.GetValue<string>()).ToArray():[];

        foreach(var name in names)
            if(string.IsNullOrEmpty(Owner(name)))Issue("CLASS_NO_OWNER",name);
        if(input.ModuleContract["interfaces_are_domain_ports"]?.GetValue<bool>()==true)
            foreach(Match declaration in Interface.Matches(input.CodeMarkdown))
                if(Owner(declaration.Groups[1].Value)!="bridge.domain")Issue("PORT_OUTSIDE_DOMAIN",declaration.Groups[1].Value);

        foreach(Match view in View.Matches(input.CodeMarkdown))
        {
            var declared=Declaration.Matches(view.Groups[2].Value).Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            foreach(var line in view.Groups[2].Value.Split('\n'))
            {
                var relation=Relationship.Match(line);if(!relation.Success)continue;
                foreach(var endpoint in new[]{relation.Groups[1].Value,relation.Groups[2].Value})
                    if(!declared.Contains(endpoint))Issue("IMPLICIT_ENDPOINT",view.Groups[1].Value+":"+endpoint);
            }
        }
        foreach(var line in input.CodeMarkdown.Split('\n'))
        {
            var relation=Relationship.Match(line);if(!relation.Success)continue;
            var from=relation.Groups[1].Value;var to=relation.Groups[2].Value;var a=Owner(from);var b=Owner(to);
            if(string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b)){Issue("DEPENDENCY_OWNER_MISSING",from+"->"+to);continue;}
            if(a==b||a=="external"||b=="external")continue;
            edges.Add(a+" -> "+b);
            if(!Targets(a).Contains(b,StringComparer.Ordinal))Issue("FORBIDDEN_DEPENDENCY",from+"->"+to);
        }
        var active=new HashSet<string>(StringComparer.Ordinal);var complete=new HashSet<string>(StringComparer.Ordinal);
        void Visit(string module)
        {
            if(active.Contains(module)){Issue("MODULE_CYCLE",module);return;}
            if(complete.Contains(module))return;
            active.Add(module);foreach(var target in Targets(module))Visit(target);active.Remove(module);complete.Add(module);
        }
        foreach(var module in dependencies.Select(p=>p.Key))Visit(module);
        return new(names.Length,edges.Order(StringComparer.Ordinal).ToArray(),issues,names);
    }
}
