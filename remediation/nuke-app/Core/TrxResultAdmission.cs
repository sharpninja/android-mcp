using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Pure normal-test admission. This is not HV acceptance and performs no external effects.</summary>
public sealed class TrxResultAdmission:ITrxResultAdmission
{
    private static readonly XNamespace Ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private static readonly HashSet<string> CounterNames=new(StringComparer.Ordinal)
    {"total","executed","passed","failed","error","timeout","aborted","inconclusive","passedButRunAborted","notRunnable","notExecuted","disconnected","warning","completed","inProgress","pending"};
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request)
    {
        var expected=request.ExpectedClasses.Distinct(StringComparer.Ordinal).ToArray();
        var classes=new Dictionary<string,int>(StringComparer.Ordinal);
        var hash=request.TrxBytes is { } raw ? ContentHash.Sha256(raw.Span) : null;
        TrxAdmissionResult Invalid(string code)=>new(false,code,null,0,classes,expected,hash);
        if(request.TrxBytes is null)return Invalid("MISSING_TRX");
        XDocument document;
        try
        {
            using var stream=new MemoryStream(request.TrxBytes.Value.ToArray(),false);
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
            document=XDocument.Load(reader);
        }
        catch(Exception error)when(error is XmlException or ArgumentException or InvalidOperationException)
        {return Invalid("INVALID_TRX");}
        var root=document.Root;
        if(root is null||root.Name!=Ns+"TestRun")return Invalid("INVALID_TRX");
        var summaries=root.Elements(Ns+"ResultSummary").ToArray();
        if(summaries.Length!=1)return Invalid("INVALID_TRX");
        var counterElements=summaries[0].Elements(Ns+"Counters").ToArray();
        if(counterElements.Length!=1)return Invalid("INVALID_TRX");
        var attributes=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var attribute in counterElements[0].Attributes())
        {
            if(attribute.IsNamespaceDeclaration)continue;
            if(attribute.Name.Namespace!=XNamespace.None||!CounterNames.Contains(attribute.Name.LocalName)||
                !int.TryParse(attribute.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var number)||number<0)
                return Invalid("INVALID_TRX");
            attributes.Add(attribute.Name.LocalName,number);
        }
        if(new[]{"total","executed","passed","failed","notExecuted"}.Any(x=>!attributes.ContainsKey(x)))return Invalid("INVALID_TRX");
        int Count(string name)=>attributes.GetValueOrDefault(name);
        var counters=new TrxRunCounters(Count("total"),Count("executed"),Count("passed"),Count("failed"),Count("notExecuted"));
        var resultContainers=root.Elements(Ns+"Results").ToArray();
        if(resultContainers.Length!=1)return Invalid("INVALID_TRX");
        var results=resultContainers[0].Elements(Ns+"UnitTestResult").ToArray();
        var definitions=root.Elements(Ns+"TestDefinitions").ToArray();
        var definitionMap=new Dictionary<string,string>(StringComparer.Ordinal);
        var definitionError=definitions.Length!=1;
        if(definitions.Length==1)
        foreach(var definition in definitions[0].Elements(Ns+"UnitTest"))
        {
            var id=definition.Attribute("id")?.Value;var method=definition.Elements(Ns+"TestMethod").ToArray();
            var className=method.Length==1 ? method[0].Attribute("className")?.Value : null;
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(className)||!definitionMap.TryAdd(id,className))definitionError=true;
        }
        var executions=new HashSet<string>(StringComparer.Ordinal);var duplicate=false;
        foreach(var result in results)
        {
            var execution=result.Attribute("executionId")?.Value;
            if(string.IsNullOrWhiteSpace(execution))definitionError=true;else if(!executions.Add(execution))duplicate=true;
            var testId=result.Attribute("testId")?.Value;
            if(testId is null||!definitionMap.TryGetValue(testId,out var className)){definitionError=true;continue;}
            classes[className]=classes.GetValueOrDefault(className)+1;
        }
        var missing=expected.Where(x=>!classes.TryGetValue(x,out var count)||count==0).ToArray();
        TrxAdmissionResult Result(string code)=>new(code=="OK",code,counters,results.Length,classes,missing,hash);
        var summaryOutcome=summaries[0].Attribute("outcome")?.Value;
        if(summaryOutcome=="Aborted"||new[]{"aborted","passedButRunAborted","error","timeout"}.Any(x=>Count(x)>0))return Result("RUN_ABORTED");
        if(summaryOutcome!="Completed"||new[]{"pending","inProgress","disconnected","notRunnable","inconclusive","completed","warning"}.Any(x=>Count(x)>0))return Result("RUN_INCOMPLETE");
        if(counters.Total==0&&results.Length==0)return Result("NO_TESTS");
        var passed=results.Count(x=>x.Attribute("outcome")?.Value=="Passed");
        var failed=results.Count(x=>x.Attribute("outcome")?.Value=="Failed");
        var skipped=results.Count(x=>x.Attribute("outcome")?.Value=="NotExecuted");
        if(counters.Total!=results.Length||counters.Passed!=passed||counters.Failed!=failed||counters.NotExecuted!=skipped||
            counters.Executed!=results.Length-skipped||passed+failed+skipped!=results.Length)return Result("AGGREGATE_MISMATCH");
        if(counters.Failed>0)return Result("TESTS_FAILED");
        if(counters.NotExecuted>0)return Result("TESTS_SKIPPED");
        if(duplicate)return Result("DUPLICATE_RESULT");
        if(definitionError)return Result("DEFINITION_MISMATCH");
        if(missing.Length>0)return Result("EXPECTED_CLASS_MISSING");
        return Result("OK");
    }
}
