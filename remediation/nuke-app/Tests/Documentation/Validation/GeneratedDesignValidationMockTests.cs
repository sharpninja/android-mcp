using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed class ScriptedGeneratedDesignValidator(GeneratedDesignValidationCase test,GeneratedDesignValidationResult response,string? inputMutation=null):IGeneratedDesignValidator
{
    public GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input)
    {
        Assert.Same(test.Input,input);
        if(inputMutation=="model")input.Model.Blueprint["tampered"]=true;
        if(inputMutation=="bytes")
        {
            Assert.True(MemoryMarshal.TryGetArray(input.Artifacts[0].Bytes,out ArraySegment<byte> segment));
            Assert.NotNull(segment.Array);segment.Array[segment.Offset]^=1;
        }
        if(inputMutation=="order")
        {
            var entries=Assert.IsType<Artifact[]>(input.Artifacts);(entries[0],entries[1])=(entries[1],entries[0]);
        }
        if(inputMutation is "path" or "media")
        {
            var entries=Assert.IsType<Artifact[]>(input.Artifacts);entries[0]=inputMutation=="path"?entries[0] with{RelativePath="wrong"}:entries[0] with{MediaType="wrong"};
        }
        return response;
    }
}

public static class GeneratedDesignValidationResponseControls
{
    public static IEnumerable<(string Name,GeneratedDesignValidationResult Response)> Wrong(GeneratedDesignValidationResult value)
    {
        yield return("checksum-counter",value with{Counters=value.Counters with{ChecksumsChecked=value.Counters.ChecksumsChecked+1}});
        yield return("link-counter",value with{Counters=value.Counters with{MarkdownLinksChecked=value.Counters.MarkdownLinksChecked+1}});
        yield return("svg-counter",value with{Counters=value.Counters with{RenderedSvgCount=value.Counters.RenderedSvgCount+1}});
        yield return("copy-counter",value with{Counters=value.Counters with{EditableUiCopies=value.Counters.EditableUiCopies+1}});
        yield return("phase",value with{Phase=value.Phase==DocumentationValidationPhase.BeforeManifest?DocumentationValidationPhase.AfterManifest:DocumentationValidationPhase.BeforeManifest});
        yield return("expected-extra",value with{ExpectedSvgPaths=value.ExpectedSvgPaths.Append("docs/wrong.svg").ToArray()});
        yield return("actual-extra",value with{ActualSvgPaths=value.ActualSvgPaths.Append("docs/wrong.svg").ToArray()});
        if(value.ExpectedSvgPaths.Count>0)yield return("expected-missing",value with{ExpectedSvgPaths=value.ExpectedSvgPaths.Skip(1).ToArray()});
        if(value.ActualSvgPaths.Count>0)yield return("actual-missing",value with{ActualSvgPaths=value.ActualSvgPaths.Skip(1).ToArray()});
        if(value.ExpectedSvgPaths.Count>1)yield return("expected-order",value with{ExpectedSvgPaths=value.ExpectedSvgPaths.Reverse().ToArray()});
        if(value.ActualSvgPaths.Count>1)yield return("actual-order",value with{ActualSvgPaths=value.ActualSvgPaths.Reverse().ToArray()});
        yield return("issue-extra",value with{Issues=value.Issues.Append(new DesignIssue("WRONG","wrong","wrong")).ToArray()});
        if(value.Issues.Count>0)
        {
            yield return("issue-missing",value with{Issues=value.Issues.Skip(1).ToArray()});
            foreach(var field in new[]{"code","path","subject"})
            {
                var issues=value.Issues.ToArray();issues[0]=field switch{"code"=>issues[0] with{Code="WRONG"},"path"=>issues[0] with{ArtifactPath="wrong"},_=>issues[0] with{Subject="wrong"}};
                yield return("issue-"+field,value with{Issues=issues});
            }
        }
        if(value.Issues.Count>1)yield return("issue-order",value with{Issues=value.Issues.Reverse().ToArray()});
        yield return("parse-extra",value with{ParseDiagnostics=value.ParseDiagnostics.Append(new("wrong",1,1,"wrong")).ToArray()});
        if(value.ParseDiagnostics.Count>0)
        {
            yield return("parse-missing",value with{ParseDiagnostics=value.ParseDiagnostics.Skip(1).ToArray()});
            foreach(var field in new[]{"path","line","column","message"})
            {
                var diagnostics=value.ParseDiagnostics.ToArray();diagnostics[0]=field switch{"path"=>diagnostics[0] with{ArtifactPath="wrong"},"line"=>diagnostics[0] with{Line=0},"column"=>diagnostics[0] with{Column=0},_=>diagnostics[0] with{Message=""}};
                yield return("parse-"+field,value with{ParseDiagnostics=diagnostics});
            }
        }
        if(value.ParseDiagnostics.Count>1)yield return("parse-order",value with{ParseDiagnostics=value.ParseDiagnostics.Reverse().ToArray()});
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class GeneratedDesignValidationMockTests
{
    public static IEnumerable<object[]> Cases=>GeneratedDesignValidationCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void AuthoredGeneratedDesignFixturesPassExactScriptedResults(string id)
    {
        var test=GeneratedDesignValidationCases.Get(id);GeneratedDesignValidationCases.AssertContract(test,new ScriptedGeneratedDesignValidator(test,test.Expected));
    }
    [Theory,MemberData(nameof(Cases))]
    public void EveryCounterSetIssueDiagnosticPhaseAndOrderControlIsRejected(string id)
    {
        var test=GeneratedDesignValidationCases.Get(id);
        foreach(var wrong in GeneratedDesignValidationResponseControls.Wrong(test.Expected))
            Assert.NotNull(Record.Exception(()=>GeneratedDesignValidationCases.AssertContract(test,new ScriptedGeneratedDesignValidator(test,wrong.Response))));
    }
    [Theory,MemberData(nameof(Cases))]
    public void EveryInputByteModelAndOrderMutationIsRejected(string id)
    {
        foreach(var mutation in new[]{"bytes","model","order","path","media"})
        {
            var test=GeneratedDesignValidationCases.Get(id);Assert.NotNull(Record.Exception(()=>GeneratedDesignValidationCases.AssertContract(test,new ScriptedGeneratedDesignValidator(test,test.Expected,mutation))));
        }
    }
}
