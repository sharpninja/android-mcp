namespace AndroidMcp.BuildTool.Core;

public enum OperationExecutor { Pending, MockTests, Tests, DocumentationAssets, NativeParser, SourcesFetch, PublicationCheck, SourcesPlan, Architecture, CatalogBuild, BrowserQa, SourcesInventory, SdkReferenceCopy, SourcesExtract, CatalogBuildFromSources, SourcesSeed, GradleCompatibility, GradleProjection, CatalogValidation, CatalogPermissionExport, ReleaseSplit, ReleaseReassemble, RepositoryDocumentationCheck, SourcesSnapshot, ReviewReceiptAudit }
public sealed record OperationDefinition(OperationKey Key, string Description,
    IReadOnlyList<string> RequiredParameters, IReadOnlyList<string> OptionalParameters,
    IReadOnlyList<PlannedStep> Steps, OperationExecutor Executor=OperationExecutor.Pending,
    IReadOnlyList<IReadOnlyList<string>>? AllOrNoneParameterGroups=null)
{
    public IReadOnlyList<IReadOnlyList<string>> DeclaredParameterGroups=>AllOrNoneParameterGroups??Array.Empty<IReadOnlyList<string>>();
}

/// <summary>Single operation vocabulary for planning, dispatch and generated help.</summary>
public sealed class OperationRegistry
{
    public IReadOnlyList<OperationDefinition> Definitions { get; }
    public OperationRegistry()
    {
        var definitions = new List<OperationDefinition>();
        void Add(Category category, Operation operation, string[] required, string[] optional, OperationExecutor executor, params PlannedStep[] steps) =>
            definitions.Add(new(new(category,operation), $"{category}/{operation}",required,optional,steps,executor));
        Add(Category.Sources,Operation.Plan,["project-root","source-manifest","recipe-lock","output-root"],
            ["release","limit","acquisition-profile"],OperationExecutor.SourcesPlan,new PlannedStep("source-plan",[]));
        definitions.Add(new(new(Category.Sources,Operation.Snapshot),"Sources/Snapshot",
            ["project-root","source-manifest","recipe-lock","output-root"],
            ["generation","release","snapshot-mode","download","bundle-root","acquisition-profile","pipeline-request","pipeline-request-sha256","pipeline-request-length"],
            [new("source-snapshot",[])],OperationExecutor.SourcesSnapshot,
            [new[]{"pipeline-request","pipeline-request-sha256","pipeline-request-length"}]));
        Add(Category.Sources,Operation.Seed,["project-root","source-manifest","recipe-lock","output-root","bundle-root"],
            ["generation","release","acquisition-profile"],OperationExecutor.SourcesSeed,
            new("validate-locked-recipe",[]),new("project-seed-candidates",["validate-locked-recipe"]),
            new("seed-available-sources",["project-seed-candidates"]),new("seal-seed-result",["seed-available-sources"]));
        Add(Category.Sources,Operation.Fetch,["project-root","source-manifest","recipe-lock","output-root","bundle-root"],
            ["generation","release","limit","acquisition-profile","cache-policy","fetch-delay-seconds"],OperationExecutor.SourcesFetch,
            new("validate-locked-recipe",[]),new("preflight-selected-sources",["validate-locked-recipe"]),
            new("acquire-selected-sources",["preflight-selected-sources"]),new("seal-acquisition",["acquire-selected-sources"]));
        Add(Category.Sources,Operation.Inventory,["project-root","source-manifest","recipe-lock","acquisition-root","acquisition-manifest","acquisition-manifest-sha256","acquisition-manifest-length","output-root"],
            ["release","limit","acquisition-profile"],OperationExecutor.SourcesInventory,
            new("validate-source-plan",[]),new("admit-acquisition-receipt",["validate-source-plan"]),
            new("verify-acquisition-readonly",["admit-acquisition-receipt"]),new("seal-source-inventory",["verify-acquisition-readonly"]));
        Add(Category.Sources,Operation.Extract,["project-root","pipeline-request","pipeline-request-sha256","pipeline-request-length"],["extract-task"],OperationExecutor.SourcesExtract,
            new("admit-pinned-workflow",[]),new("admit-task-inputs",["admit-pinned-workflow"]),
            new("verify-task-source-pins",["admit-task-inputs"]),new("capture-runtime-identity",["verify-task-source-pins"]),
            new("extract-source-generation",["capture-runtime-identity"]),new("seal-extraction",["extract-source-generation"]));
        Add(Category.Sources,Operation.NativeParser,["compiler-identity","output-root"],["project-root","cache-root","receipt-root"],OperationExecutor.NativeParser,
            new("verify-compiler-identity",[]),new("acquire-native-sources",["verify-compiler-identity"]),
            new("build-native-parser",["acquire-native-sources"]),new("verify-native-parser",["build-native-parser"]));
        Add(Category.Catalog,Operation.Build,["project-root","source-manifest","output-root","stage-root"],
            ["java-native-manifest","extraction-profile"],OperationExecutor.CatalogBuild,
            new("validate-source-manifest",[]),new("build-catalog",["validate-source-manifest"]));
        Add(Category.Catalog,Operation.BuildFromSources,["project-root","pipeline-request","pipeline-request-sha256","pipeline-request-length"],[],OperationExecutor.CatalogBuildFromSources,
            new("admit-pinned-workflow",[]),new("admit-locked-recipe",["admit-pinned-workflow"]),
            new("verify-source-inventory",["admit-locked-recipe"]),new("capture-runtime-identity",["verify-source-inventory"]),
            new("project-effective-inventory",["capture-runtime-identity"]),new("build-catalog",["project-effective-inventory"]),
            new("seal-catalog",["build-catalog"]));
        Add(Category.Catalog,Operation.Export,["project-root","source-catalog","output-root"],[],OperationExecutor.CatalogPermissionExport,
            new("admit-readonly-export",[]),new("export-permissions",["admit-readonly-export"]),new("emit-coverage",["export-permissions"]));
        Add(Category.Design,Operation.Assets,["project-root","output-root"],["generation"],OperationExecutor.DocumentationAssets,
            new("validate-design-inputs",[]),new("project-ui",["validate-design-inputs"]));
        Add(Category.Design,Operation.GradleProjection,["output-root"],["generation"],OperationExecutor.GradleProjection,
            new PlannedStep("emit-gradle-projection",[]));
        Add(Category.Design,Operation.Architecture,["project-root","output-root","cache-root","work-root","receipt-root","docker-identity","renderer-user","tool-access"],
            ["generation","offline-gradle-checksum-proof"],OperationExecutor.Architecture,
            new("validate-architecture-inputs",[]),new("preflight-native-context",["validate-architecture-inputs"]),
            new("verify-pinned-tools",["preflight-native-context"]),new("generate-architecture",["verify-pinned-tools"]),new("seal-generation",["generate-architecture"]));
        Add(Category.Design,Operation.GradleCompatibility,["project-root","output-root","cache-root","work-root","receipt-root","docker-identity","renderer-user","tool-access","design-aliases","design-registry-sha256"],
            ["generation","offline-gradle-checksum-proof","catalog-root","policy-relative-path","pipeline-request","pipeline-request-sha256","pipeline-request-length"],OperationExecutor.GradleCompatibility,
            new PlannedStep("design-stage-union",[]));
        Add(Category.Design,Operation.BrowserQa,["project-root","output-root","browser-identity","ui-manifest-sha256","expected-architecture-diagrams"],
            ["architecture-root","architecture-manifest-sha256","viewport-width","viewport-height"],OperationExecutor.BrowserQa,
            new("validate-browser-generations",[]),new("run-native-browser-qa",["validate-browser-generations"]),
            new("seal-browser-evidence",["run-native-browser-qa"]));
        Add(Category.Validate,Operation.Catalog,["project-root","pipeline-request","pipeline-request-sha256","pipeline-request-length"],[],OperationExecutor.CatalogValidation,
            new("admit-pinned-workflow",[]),new("validate-catalog-readonly",["admit-pinned-workflow"]),
            new("emit-validation-receipt",["validate-catalog-readonly"]));
        Add(Category.Validate,Operation.Toolchain,[],["toolchain-check","project-root","pipeline-request","pipeline-request-sha256","pipeline-request-length"],OperationExecutor.SdkReferenceCopy,new PlannedStep("verify-toolchain",[]));
        Add(Category.Validate,Operation.Design,["project-root"],[],OperationExecutor.RepositoryDocumentationCheck,new PlannedStep("check-repository-documentation",[]));
        Add(Category.Review,Operation.Assess,["project-root","output-root"],[],OperationExecutor.ReviewReceiptAudit,
            new("discover-local-review-receipts",[]),new("verify-local-review-receipts",["discover-local-review-receipts"]));
        Add(Category.Review,Operation.Prepare,["snapshot"],[],OperationExecutor.Pending,new("verify-review-evidence",[]),new("prepare-review",[]));
        Add(Category.Publish,Operation.Check,["git-checkout","git-identity","receipt-root"],["policy-relative-path"],OperationExecutor.PublicationCheck,new PlannedStep("check-deliverables",[]));
        Add(Category.Publish,Operation.Split,["project-root","source-artifact","generation","artifact-sha256"],["part-bytes"],OperationExecutor.ReleaseSplit,
            new("admit-release-input",[]),new("split-parts",["admit-release-input"]),new("seal-parts-manifest",["split-parts"]));
        Add(Category.Publish,Operation.Reassemble,["project-root","parts-manifest","generation","destination-artifact","manifest-sha256"],[],OperationExecutor.ReleaseReassemble,
            new("admit-parts-manifest",[]),new("verify-parts",["admit-parts-manifest"]),new("reassemble-artifact",["verify-parts"]),new("seal-reassembly-receipt",["reassemble-artifact"]));
        Add(Category.Validate,Operation.MockGate,[],["test-filter"],OperationExecutor.MockTests,new PlannedStep("mock-tests",[]));
        Add(Category.Validate,Operation.Tests,[],["test-filter","java-native-manifest"],OperationExecutor.Tests,new PlannedStep("all-tests",[]));
        Definitions = definitions.AsReadOnly();
    }
    public OperationDefinition? Find(OperationKey key) => Definitions.SingleOrDefault(x=>x.Key==key);
}

/// <summary>Pure preflight: no file, process, network or environment access.</summary>
public sealed class OperationPlanner(OperationRegistry registry) : IOperationPlanner
{
    public IReadOnlyList<string> ObservedEffects => Array.Empty<string>();
    public PlanningResult Plan(OperationRequest request)
    {
        if (!Enum.TryParse<Category>(request.Category,true,out var category) || !Enum.IsDefined(category))
            return Failure("UNKNOWN_CATEGORY","",request.Category);
        if (!Enum.TryParse<Operation>(request.Operation,true,out var operation) || !Enum.IsDefined(operation) || operation==Operation.None)
            return Failure("UNKNOWN_OPERATION","",request.Operation);
        var definition=registry.Find(new(category,operation));
        if (definition is null) return Failure("ILLEGAL_CATEGORY_OPERATION","",$"{category}/{operation}");
        var errors=new List<PlanError>();
        foreach(var parameter in definition.RequiredParameters)
            if (!request.Parameters.TryGetValue(parameter,out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add(new("MISSING_PARAMETER",parameter,$"Supply --{parameter}."));
        foreach(var group in definition.DeclaredParameterGroups.Where(group=>group.Any(request.Parameters.ContainsKey)))
            foreach(var parameter in group)
                if((!request.Parameters.TryGetValue(parameter,out var value)||string.IsNullOrWhiteSpace(value))&&
                    !errors.Any(error=>error.Code=="MISSING_PARAMETER"&&error.Parameter==parameter))
                    errors.Add(new("MISSING_PARAMETER",parameter,$"Supply --{parameter} together with its declared parameter group."));
        foreach(var parameter in request.Parameters.Keys.Order(StringComparer.Ordinal))
            if (!definition.RequiredParameters.Contains(parameter,StringComparer.Ordinal) && !definition.OptionalParameters.Contains(parameter,StringComparer.Ordinal))
                errors.Add(new("UNSUPPORTED_PARAMETER",parameter,$"Not accepted by {category}/{operation}."));
        return errors.Count>0 ? new(null,errors) : new(new(definition.Key,definition.Steps),[]);
    }
    private static PlanningResult Failure(string code,string parameter,string message) => new(null,[new(code,parameter,message)]);
}
