namespace AndroidMcp.BuildTool.Catalog;

public sealed record PolicyRule(string Effect, string SchemaHash, IReadOnlySet<string> AllowedTargets, int MaxItems);
public sealed record CanonicalBinding(string BindingId, string SchemaHash, string Target, object Items);

/// <summary>Offline admission algebra. A candidate is never an execution or an OS grant.</summary>
public static class PolicyReference
{
    public static string Evaluate(IReadOnlyList<CanonicalBinding> plan, IReadOnlyDictionary<string,PolicyRule> bindingRules,
        PolicyRule? entryRule,string entrySchema,bool authority,bool emergency=false)
    {
        if(emergency || plan.Count==0 || entryRule is null || entryRule.SchemaHash!=entrySchema) return "deny";
        var ask=false;
        foreach(var binding in plan)
        {
            if(!bindingRules.TryGetValue(binding.BindingId,out var rule) || rule.SchemaHash!=binding.SchemaHash) return "deny";
            foreach(var current in new[]{rule,entryRule})
            {
                if(current.Effect is not ("allow" or "ask") || !current.AllowedTargets.Contains(binding.Target)
                    || !TryInteger(binding.Items,out var items) || items<0 || items>current.MaxItems) return "deny";
                ask |= current.Effect=="ask";
            }
        }
        return !authority ? "os-unavailable" : ask ? "ask" : "admit-candidate";
    }

    private static bool TryInteger(object value,out long result)
    {
        switch(value)
        {
            case int integer: result=integer;return true;
            case long integer: result=integer;return true;
            default: result=0;return false;
        }
    }
}
