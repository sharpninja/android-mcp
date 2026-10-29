using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources.Constraints;

public interface IAidlSourceConstraints
{
    bool StableModule(JsonObject module);
    JsonObject ExactConstraint(JsonObject source,JsonObject declaration,JsonObject audit,string publishedText,JsonObject module,JsonObject linkage);
}
public interface IHidlSourceConstraints
{
    JsonObject ExactConstraint(JsonObject source,JsonObject record,JsonArray facts);
}
