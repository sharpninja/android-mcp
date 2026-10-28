using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>All literal lifecycle nodes, without target filtering or availability inference.</summary>
public sealed class SdkAvailabilitySourceFacts : ISdkAvailabilitySourceFacts
{
    public SdkAvailabilityEvidence Enumerate(ReadOnlyMemory<byte> xml)
    {
        var root = SourceXml.Load(xml);
        if (root.Name != "api" || (string?)root.Attribute("version") != "4") throw new FormatException("Unsupported lifecycle format");
        var locations = SourceXml.StartElementLocations(xml, ["class", "method", "field"]);
        var rows = new List<JsonObject>(); var position = 0;
        foreach (var owner in root.Elements("class"))
        {
            var ordinal = 0;
            foreach (var node in new[] { owner }.Concat(owner.Elements().Where(node => node.Name == "method" || node.Name == "field")))
            {
                if (position >= locations.Count || locations[position].Name != node.Name.ToString()) throw new FormatException("Lifecycle locator mismatch");
                var line = locations[position++].Line;
                JsonArray? supertypes = null;
                if (ReferenceEquals(node, owner))
                {
                    supertypes = new JsonArray();
                    foreach (var parent in owner.Elements().Where(parent => parent.Name == "extends" || parent.Name == "implements"))
                    {
                        var attributes = SourceXml.Json(SourceXml.Attributes(parent)); attributes["relation"] = parent.Name.ToString(); supertypes.Add(attributes);
                    }
                }
                rows.Add(new()
                {
                    ["owner_binary_name"] = (string?)owner.Attribute("name"), ["tag"] = node.Name.ToString(), ["member_ordinal"] = ordinal++, ["line"] = line,
                    ["attributes"] = SourceXml.Json(SourceXml.Attributes(node)), ["owner_attributes"] = SourceXml.Json(SourceXml.Attributes(owner)), ["supertypes"] = supertypes,
                    ["semantics"] = "Exact source facts; absent constraints unknown/not declared, never assumed unrestricted"
                });
            }
        }
        if (position != locations.Count) throw new FormatException("Uninventoried lifecycle declaration");
        return new(rows, root.Elements("sdk").Select(node => SourceXml.Json(SourceXml.Attributes(node))).ToArray());
    }
}
