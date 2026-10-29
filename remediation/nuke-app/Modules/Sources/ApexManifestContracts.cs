using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public interface IApexManifestSourceFacts { JsonObject Manifest(string text); }
