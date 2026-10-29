using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationBuildPayloadCase(string Id,DocumentationBuildPayloadInput Input,
    DocumentationBuildPayloadPlan Expected,string? Error=null)
{public override string ToString()=>Id;}

public static class DocumentationBuildPayloadCases
{
    public static IReadOnlyList<DocumentationBuildPayloadCase> Create()
    {
        var root=Path.Combine(Path.GetTempPath(),"nuke-payload-pure-fixture");
        var app=Path.Combine(root,"app");var host=Path.Combine(app,"AndroidMcp.Build.csproj");
        var tests=Path.Combine(app,"Tests/Tests.csproj");var sdk=Path.Combine(root,"sdk");var nuget=Path.Combine(root,"packages");
        string Local(string path)=>Path.Combine(app,path.Replace('/',Path.DirectorySeparatorChar));
        string Sha(string value)=>ContentHash.Sha256(Encoding.UTF8.GetBytes(value));
        var sdkFiles=new[]{new StoredArtifact("Microsoft.Build.dll",Sha("sdk assembly"),12,"application/octet-stream")};
        var configuration=new TestGateNativeFactsConfiguration(new(sdk,sdkFiles),[app,sdk,nuget],"Debug","net10.0");
        var localPaths=new[]{"AndroidMcp.Build.csproj","global.json","packages.lock.json","Core/Program.cs","Modules/Data/entities.json","Directory.Build.props"};
        var kinds=new[]{"Project","None","NuGetLockFilePath","Compile","EmbeddedResource","Import"};
        var localInputs=localPaths.Where((_,index)=>index!=5).Select((path,index)=>new TestGateEvaluatedInput(kinds[index],Local(path),host)).ToArray();
        var generated=Local("obj/project.assets.json");var generatedImport=Local("obj/AndroidMcp.Build.csproj.nuget.g.props");
        var sdkImport=Path.Combine(sdk,"Microsoft.Common.CurrentVersion.targets");
        var analyzer=Path.Combine(nuget,"analyzer/1.0/analyzers/Analyzer.dll");
        var hostInputs=localInputs.Concat(new[]{new TestGateEvaluatedInput("ProjectAssetsFile",generated,host),new("Analyzer",analyzer,host)}).ToArray();
        var hostProject=new TestGateEvaluatedProject(host,Local("bin/Debug/AndroidMcp.Build.dll"),hostInputs,
            [Local("Directory.Build.props"),generatedImport,sdkImport],[],[]);
        var testProject=new TestGateEvaluatedProject(tests,Local("Tests/bin/Debug/Tests.dll"),[new("Compile",Local("Tests/Test.cs"),tests)],[],[host],[]);
        var evaluation=new TestGateSdkEvaluation([hostProject,testProject],sdkFiles);
        TestGateHashedInput Pin(TestGateEvaluatedInput row)=>new(row.Kind,row.AbsolutePath,row.DefiningProject,Sha("source:"+row.AbsolutePath),17);
        TestGateSourceManifest Manifest(TestGateSdkEvaluation value)=>new(host,tests,"Debug","net10.0",value.Projects
            .SelectMany(project=>project.Inputs.Concat(project.Imports.Select(path=>new TestGateEvaluatedInput("Import",path,project.ProjectPath)))).Select(Pin).ToArray());
        var original=Manifest(evaluation);
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        DocumentationBuildPayloadInput Input(TestGateSdkEvaluation eval,TestGateSourceManifest manifest)
        {
            Artifact Json(string path,object value)=>new(path,new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value)),"application/json");
            var source=Json("receipts/source-BeforeBuild.json",manifest);var native=Json("receipts/sdk-BeforeBuild.json",eval);
            var identity=verifier.Describe(source);
            return new(app,host,new(identity,identity.Sha256,verifier.Describe(native)),source,native,configuration,[new("sdk",sdk),new("nuget",nuget)]);
        }
        var input=Input(evaluation,original);
        StoredArtifact LocalPin(string relative)=>new(relative,Sha("source:"+Local(relative)),17,"application/octet-stream");
        var expected=new DocumentationBuildPayloadPlan(Array.AsReadOnly(localPaths.Order(StringComparer.Ordinal).Select(path=>
            new DocumentationBuildPayloadRoute(new(path,"application/octet-stream",LocalPin(path)),"build-tool/"+path)).ToArray()),
            Array.AsReadOnly(new[]{
                new DocumentationBuildPrerequisite("restore-generated","obj/project.assets.json","ProjectAssetsFile",Sha("source:"+generated),17),
                new DocumentationBuildPrerequisite("restore-generated","obj/AndroidMcp.Build.csproj.nuget.g.props","Import",Sha("source:"+generatedImport),17),
                new DocumentationBuildPrerequisite("sdk","Microsoft.Common.CurrentVersion.targets","Import",Sha("source:"+sdkImport),17),
                new DocumentationBuildPrerequisite("nuget","analyzer/1.0/analyzers/Analyzer.dll","Analyzer",Sha("source:"+analyzer),17)
            }.OrderBy(row=>row.ScopeId,StringComparer.Ordinal).ThenBy(row=>row.RelativePath,StringComparer.Ordinal).ThenBy(row=>row.Kind,StringComparer.Ordinal).ToArray()));
        var rows=new List<DocumentationBuildPayloadCase>{new("FULL-HOST-NO-TEST-EXPORT",input,expected)};
        // The installed SDK's workload metadata and each restored package/version
        // have independently admitted roots. Logical labels never grant roots.
        var metadataRoot=Path.Combine(root,"sdk-metadata");var metadataImport=Path.Combine(metadataRoot,"Workload.props");
        var metadataProject=hostProject with{Imports=hostProject.Imports.Append(metadataImport).ToArray()};
        var metadataEvaluation=evaluation with{Projects=[metadataProject,testProject]};
        var metadataInput=Input(metadataEvaluation,Manifest(metadataEvaluation));
        metadataInput=metadataInput with{NativeConfiguration=configuration with{ApprovedReadRoots=configuration.ApprovedReadRoots.Append(metadataRoot).ToArray()},
            PrerequisiteScopes=[new("sdk",sdk),new("nuget-analyzer-1",nuget),new("sdk-metadata",metadataRoot)]};
        var metadataExpected=expected with{Prerequisites=Array.AsReadOnly(expected.Prerequisites.Select(row=>row.ScopeId=="nuget"?row with{ScopeId="nuget-analyzer-1"}:row)
            .Append(new("sdk-metadata","Workload.props","Import",Sha("source:"+metadataImport),17)).OrderBy(row=>row.ScopeId,StringComparer.Ordinal).ThenBy(row=>row.RelativePath,StringComparer.Ordinal).ThenBy(row=>row.Kind,StringComparer.Ordinal).ToArray())};
        rows.Add(new("EXPLICIT-VERSIONED-PREREQUISITES",metadataInput,metadataExpected));
        void Bad(string id,DocumentationBuildPayloadInput changed,string error)=>rows.Add(new(id,changed,expected,error));
        void Changed(string id,TestGateSdkEvaluation changed,string error)=>Bad(id,Input(changed,Manifest(changed)),error);
        var aliases=hostProject with{Inputs=hostProject.Inputs.Append(new("None",Local("Core/Program.cs"),host)).ToArray()};
        var aliasEval=evaluation with{Projects=new[]{aliases,testProject}};
        rows.Add(new("DUPLICATE-PHYSICAL-INPUT-ONE-ROUTE",Input(aliasEval,Manifest(aliasEval)),expected));
        Bad("SNAPSHOT-CONTENT-FINGERPRINT",input with{Snapshot=input.Snapshot with{ContentFingerprint=new string('a',64)}},"BUILD_SOURCE_PIN");
        Bad("MANIFEST-BYTES-CHANGED",input with{SourceManifest=input.SourceManifest with{Bytes=Encoding.UTF8.GetBytes("{}")}},"BUILD_SOURCE_PIN");
        Bad("NATIVE-BYTES-CHANGED",input with{NativeEvaluation=input.NativeEvaluation with{Bytes=Encoding.UTF8.GetBytes("{}")}},"BUILD_SOURCE_PIN");
        Bad("HOST-IDENTITY",input with{HostProject=Local("Other.csproj")},"BUILD_SOURCE_HOST");
        Bad("ROOT-IDENTITY",input with{SourceRoot=Path.Combine(root,"other")},"BUILD_SOURCE_HOST");
        foreach(var kind in new[]{"Compile","EmbeddedResource","Import"})
            Bad("MISSING-HASH-"+kind,Input(evaluation,original with{Inputs=original.Inputs.Where(row=>row.Kind!=kind).ToArray()}),"BUILD_SOURCE_COVERAGE");
        Bad("UNEXPECTED-HASH-ROW",Input(evaluation,original with{Inputs=original.Inputs.Append(new("Compile",Local("Unknown.cs"),host,Sha("unknown"),3)).ToArray()}),"BUILD_SOURCE_COVERAGE");
        Bad("CONFLICTING-PHYSICAL-PIN",Input(evaluation,original with{Inputs=original.Inputs.Append(original.Inputs[0] with{Sha256=Sha("conflict")}).ToArray()}),"BUILD_SOURCE_PIN_CONFLICT");
        Changed("HOST-PROJECT-REFERENCE",evaluation with{Projects=new[]{hostProject with{ProjectReferences=[tests]},testProject}},"BUILD_SOURCE_PROJECT_REFERENCE");
        Changed("EXTERNAL-COMPILE",evaluation with{Projects=new[]{hostProject with{Inputs=hostInputs.Append(new("Compile",Path.Combine(nuget,"source.cs"),host)).ToArray()},testProject}},"BUILD_SOURCE_EXTERNAL_SOURCE");
        Changed("GENERATED-COMPILE",evaluation with{Projects=new[]{hostProject with{Inputs=hostInputs.Append(new("Compile",Local("obj/Generated.cs"),host)).ToArray()},testProject}},"BUILD_SOURCE_GENERATED_SOURCE");
        Bad("UNADMITTED-EXTERNAL",input with{PrerequisiteScopes=[new("sdk",sdk)]},"BUILD_SOURCE_PREREQUISITE");
        Bad("AMBIGUOUS-EXTERNAL-ROOT",input with{PrerequisiteScopes=input.PrerequisiteScopes.Append(new("overlap",root)).ToArray()},"BUILD_SOURCE_PREREQUISITE");
        Bad("SDK-IDENTITY",Input(evaluation with{SdkFiles=[sdkFiles[0] with{Sha256=Sha("other SDK")}]},original),"BUILD_SOURCE_SDK");
        Bad("CONFIGURATION",Input(evaluation,original with{Configuration="Release"}),"BUILD_SOURCE_CONFIGURATION");
        DocumentationBuildPayloadInput Remint(Artifact source,Artifact native)=>input with
        {SourceManifest=source,NativeEvaluation=native,Snapshot=new(verifier.Describe(source),verifier.Describe(source).Sha256,verifier.Describe(native))};
        Bad("INVALID-SOURCE-JSON",Remint(input.SourceManifest with{Bytes=new byte[]{0xff}},input.NativeEvaluation),"BUILD_SOURCE_JSON");
        Bad("INVALID-NATIVE-JSON",Remint(input.SourceManifest,input.NativeEvaluation with{Bytes=Encoding.UTF8.GetBytes("{\"Projects\":[],\"Projects\":[]}")}),"BUILD_SOURCE_JSON");
        Changed("MISSING-PROJECT-INPUT",evaluation with{Projects=new[]{hostProject with{Inputs=hostInputs.Where(row=>row.Kind!="Project").ToArray()},testProject}},"BUILD_SOURCE_REQUIRED");
        Changed("MISSING-NATIVE-HOST",evaluation with{Projects=new[]{testProject}},"BUILD_SOURCE_HOST");
        Changed("DUPLICATE-NATIVE-HOST",evaluation with{Projects=new[]{hostProject,hostProject,testProject}},"BUILD_SOURCE_HOST");
        Changed("NATIVE-HOST-ISSUES",evaluation with{Projects=new[]{hostProject with{Issues=["unresolved input"]},testProject}},"BUILD_SOURCE_HOST");
        foreach(var required in new[]{"global.json","packages.lock.json"})
        {
            var absent=hostProject with{Inputs=hostInputs.Where(row=>row.AbsolutePath!=Local(required)).ToArray()};
            Changed("MISSING-BUILD-PREREQUISITE-"+required,evaluation with{Projects=new[]{absent,testProject}},"BUILD_SOURCE_REQUIRED");
        }
        var caseCollision=hostProject with{Inputs=hostInputs.Append(new("Compile",Local("core/program.cs"),host)).ToArray()};
        Changed("CASE-COLLIDING-ROUTES",evaluation with{Projects=new[]{caseCollision,testProject}},"BUILD_SOURCE_COLLISION");
        return rows;
    }
    public static IEnumerable<object[]> Cases=>Create().Select(test=>new object[]{test});
    public static void AssertContract(DocumentationBuildPayloadCase test,IDocumentationBuildPayloadPlanner provider)
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(test.Input);
        if(test.Error is not null)Assert.Equal(test.Error,Assert.Throws<DocumentationBuildPayloadException>(()=>provider.Plan(test.Input)).Code);
        else
        {
            var plan=provider.Plan(test.Input);Assert.Equal(test.Expected.Sources,plan.Sources);Assert.Equal(test.Expected.Prerequisites,plan.Prerequisites);
            var source=Assert.IsAssignableFrom<IList<DocumentationBuildPayloadRoute>>(plan.Sources);
            Assert.Throws<NotSupportedException>(()=>source[0]=source[0]);
            var prerequisites=Assert.IsAssignableFrom<IList<DocumentationBuildPrerequisite>>(plan.Prerequisites);
            Assert.Throws<NotSupportedException>(()=>prerequisites[0]=prerequisites[0]);
        }
        Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(test.Input));
    }
}
public sealed class ScriptedDocumentationBuildPayloadPlanner(DocumentationBuildPayloadCase test,string mutation=""):IDocumentationBuildPayloadPlanner
{
    public DocumentationBuildPayloadPlan Plan(DocumentationBuildPayloadInput input)
    {
        Assert.Same(test.Input,input);
        if(test.Error is not null){if(mutation=="error")return test.Expected;throw new DocumentationBuildPayloadException(test.Error);}
        var sources=test.Expected.Sources.ToArray();var prerequisites=test.Expected.Prerequisites.ToArray();
        var first=sources[0];
        if(mutation=="omit")sources=sources.Skip(1).ToArray();
        if(mutation=="route")sources[0]=first with{PublicationPath="wrong"};
        if(mutation=="input")sources[0]=first with{Source=first.Source with{RelativePath="wrong"}};
        if(mutation=="media")sources[0]=first with{Source=first.Source with{MediaType="wrong"}};
        if(mutation=="pin")sources[0]=first with{Source=first.Source with{ExpectedIdentity=null}};
        if(mutation=="hash")sources[0]=first with{Source=first.Source with{ExpectedIdentity=first.Source.ExpectedIdentity! with{Sha256=new string('a',64)}}};
        if(mutation=="size")sources[0]=first with{Source=first.Source with{ExpectedIdentity=first.Source.ExpectedIdentity! with{Length=123}}};
        if(mutation=="pin-path")sources[0]=first with{Source=first.Source with{ExpectedIdentity=first.Source.ExpectedIdentity! with{RelativePath="wrong"}}};
        if(mutation=="pin-media")sources[0]=first with{Source=first.Source with{ExpectedIdentity=first.Source.ExpectedIdentity! with{MediaType="wrong"}}};
        if(mutation=="prerequisite")prerequisites=prerequisites.Skip(1).ToArray();
        if(mutation=="scope")prerequisites[0]=prerequisites[0] with{ScopeId="wrong"};
        if(mutation=="kind")prerequisites[0]=prerequisites[0] with{Kind="wrong"};
        if(mutation=="prerequisite-hash")prerequisites[0]=prerequisites[0] with{Sha256="wrong"};
        if(mutation=="prerequisite-size")prerequisites[0]=prerequisites[0] with{Length=123};
        if(mutation=="prerequisite-path")prerequisites[0]=prerequisites[0] with{RelativePath="wrong"};
        return new(mutation=="mutable"?sources:Array.AsReadOnly(sources),mutation=="mutable-prerequisite"?prerequisites:Array.AsReadOnly(prerequisites));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadMockTests
{
    [Theory,MemberData(nameof(DocumentationBuildPayloadCases.Cases),MemberType=typeof(DocumentationBuildPayloadCases))]
    public void CorrectNativePairProjectionAndFailuresPass(DocumentationBuildPayloadCase test)=>DocumentationBuildPayloadCases.AssertContract(test,new ScriptedDocumentationBuildPayloadPlanner(test));
    [Theory,MemberData(nameof(DocumentationBuildPayloadCases.Cases),MemberType=typeof(DocumentationBuildPayloadCases))]
    public void EachIncorrectOutcomeIsRejected(DocumentationBuildPayloadCase test)
    {var mutation=test.Error is null?"omit":"error";Assert.NotNull(Record.Exception(()=>DocumentationBuildPayloadCases.AssertContract(test,new ScriptedDocumentationBuildPayloadPlanner(test,mutation))));}
    [Theory]
    [InlineData("route")][InlineData("input")][InlineData("media")][InlineData("pin")][InlineData("hash")][InlineData("size")][InlineData("pin-path")][InlineData("pin-media")]
    [InlineData("prerequisite")][InlineData("scope")][InlineData("kind")][InlineData("prerequisite-hash")][InlineData("prerequisite-size")][InlineData("prerequisite-path")][InlineData("mutable")][InlineData("mutable-prerequisite")]
    public void EachRoutePinPrerequisiteAndMutationBoundaryIsDiscriminating(string mutation)
    {var test=DocumentationBuildPayloadCases.Create()[0];Assert.NotNull(Record.Exception(()=>DocumentationBuildPayloadCases.AssertContract(test,new ScriptedDocumentationBuildPayloadPlanner(test,mutation))));}
}
