using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public interface IDesignFullArchitectureRecipeFactory:IDesignCompositionRecipeFactory
{
    ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection,
        DocumentationModelSourceSnapshot capturedSources,IDocumentationSourceCollection sourceCollection);
}
