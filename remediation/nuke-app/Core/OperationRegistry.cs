namespace AndroidMcp.BuildTool.Core;

public enum OperationExecutor { Pending, MockTests, Tests, DocumentationAssets, NativeParser }
public sealed record OperationDefinition(OperationKey Key, string Description,
    IReadOnlyList<string> RequiredParameters, IReadOnlyList<string> OptionalParameters,
    IReadOnlyList<PlannedStep> Steps, OperationExecutor Executor=OperationExecutor.Pending);

/// <summary>Single operation vocabulary for planning, dispatch and generated help.</summary>
public sealed class OperationRegistry
{
    public IReadOnlyList<OperationDefinition> Definitions { get; }
    public OperationRegistry()
    {
        var definitions = new List<OperationDefinition>();
        void Add(Category category, Operation operation, string[] required, string[] optional, OperationExecutor executor, params PlannedStep[] steps) =>
            definitions.Add(new(new(category,operation), $"{category}/{operation}",required,optional,steps,executor));
        Add(Category.Sources,Operation.Plan,[],[],OperationExecutor.Pending,new PlannedStep("source-plan",[]));
        Add(Category.Sources,Operation.NativeParser,["compiler-identity","output-root"],["project-root","cache-root","receipt-root"],OperationExecutor.NativeParser,
            new("verify-compiler-identity",[]),new("acquire-native-sources",["verify-compiler-identity"]),
            new("build-native-parser",["acquire-native-sources"]),new("verify-native-parser",["build-native-parser"]));
        Add(Category.Catalog,Operation.Build,["source-manifest","output-root"],[],OperationExecutor.Pending,
            new("validate-source-manifest",[]),new("build-catalog",["validate-source-manifest"]));
        Add(Category.Design,Operation.Assets,["project-root","output-root"],["generation"],OperationExecutor.DocumentationAssets,
            new("validate-design-inputs",[]),new("project-ui",["validate-design-inputs"]));
        Add(Category.Validate,Operation.Toolchain,[],[],OperationExecutor.Pending,new PlannedStep("verify-toolchain",[]));
        Add(Category.Review,Operation.Prepare,["snapshot"],[],OperationExecutor.Pending,new("verify-review-evidence",[]),new("prepare-review",[]));
        Add(Category.Publish,Operation.Check,["git-checkout"],[],OperationExecutor.Pending,new PlannedStep("check-deliverables",[]));
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
        foreach(var parameter in request.Parameters.Keys.Order(StringComparer.Ordinal))
            if (!definition.RequiredParameters.Contains(parameter,StringComparer.Ordinal) && !definition.OptionalParameters.Contains(parameter,StringComparer.Ordinal))
                errors.Add(new("UNSUPPORTED_PARAMETER",parameter,$"Not accepted by {category}/{operation}."));
        return errors.Count>0 ? new(null,errors) : new(new(definition.Key,definition.Steps),[]);
    }
    private static PlanningResult Failure(string code,string parameter,string message) => new(null,[new(code,parameter,message)]);
}
