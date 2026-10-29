using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Checks the already captured wrapper source. Acquisition, filesystem access,
// executable effects and the public caller remain existing shared authorities.
public interface IDocumentationWrapperPropertiesValidator
{
    void Validate(DocumentationRendererProfile profile,Artifact properties);
}
