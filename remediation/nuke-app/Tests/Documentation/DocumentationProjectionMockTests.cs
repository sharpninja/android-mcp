using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class DocumentationContractException(string message) : Exception(message);

public sealed record ProjectionAssertionCase(
    string Id,
    Action<DocumentationProjection> Assert,
    Func<DocumentationProjection, DocumentationProjection> IncorrectResponse)
{
    public override string ToString() => Id;
}

// Independent oracle: expected values come from source-pinned original
// artifacts and the canonical blueprint, never from a production projector.
public sealed class DocumentationFixture
{
    public static DocumentationFixture Current { get; } = new();
    public string Workspace { get; }
    public JsonObject Blueprint { get; }
    public JsonObject Inventory { get; }
    public DocumentationInput Input { get; }
    public DocumentationProjection Correct { get; }
    public IReadOnlyList<JsonObject> Screens { get; }
    public IReadOnlyList<JsonObject> Steps { get; }
    public IReadOnlyDictionary<string, string> ExpectedHashes { get; }
    public JsonObject HealthMigration { get; }
    public const string NavigationBegin = "<!-- GENERATED UI NAVIGATION BEGIN -->";
    public const string NavigationEnd = "<!-- GENERATED UI NAVIGATION END -->";

    private DocumentationFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "remediation", "design-c4", "docs", "ui-blueprint.md")))
            directory = directory.Parent;
        Workspace = directory?.FullName ?? throw new InvalidOperationException("Canonical design fixture workspace unavailable");
        Inventory = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Workspace,"remediation/nuke-planning/docs/saved-fixture-inventory.json")))!.AsObject();
        ExpectedHashes = Inventory["baselineFiles"]!.AsArray().Concat(Inventory["docsVectorFixtures"]!.AsArray())
            .Select(n => n!.AsObject()).ToDictionary(n => Text(n,"path"),n => CurrentSourceHash(Text(n,"path"),Text(n,"sha256")),StringComparer.Ordinal);
        HealthMigration = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Workspace,"remediation/nuke-planning/docs/health-migration-and-gate-chronology.json")))!.AsObject();
        if (HealthMigration["beforeSha256"]!.GetValue<string>() != ExpectedHashes["remediation/design-c4/docs/ui-blueprint.md"])
            throw new InvalidOperationException("Health migration does not reference the pinned original input");
        var root = Path.Combine(Workspace,"remediation/design-c4");
        Artifact Read(string relative)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root,relative));
            var inventoryPath = "remediation/design-c4/" + relative;
            var hashKey = relative.StartsWith("ui-svg/",StringComparison.Ordinal)
                ? "remediation/design-c4/docs/screens/" + Path.GetFileName(relative) : inventoryPath;
            var expected = relative == "docs/ui-blueprint.md" ? HealthMigration["afterSha256"]!.GetValue<string>() : ExpectedHashes.GetValueOrDefault(hashKey);
            if (expected is null || Hash(bytes) != expected)
                throw new InvalidOperationException("Pinned fixture changed or unassigned: " + relative);
            return new(relative,bytes,relative.EndsWith(".svg",StringComparison.Ordinal) ? "image/svg+xml" : relative.EndsWith(".html",StringComparison.Ordinal) ? "text/html" : "text/plain");
        }
        var blueprint = Read("docs/ui-blueprint.md");
        var design = Read("docs/Android MCP Bridge Design.md");
        Input = new(blueprint,design);
        var block = Regex.Match(Utf8(blueprint),"```json ui-blueprint-v2\\n([\\s\\S]*?)\\n```");
        if (!block.Success) throw new InvalidOperationException("Canonical blueprint fixture block missing");
        Blueprint = JsonNode.Parse(block.Groups[1].Value)!.AsObject();
        Screens = Blueprint["screens"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        Steps = Blueprint["storyboards"]!.AsArray().SelectMany(n => n!["steps"]!.AsArray()).Select(n => n!.AsObject()).ToArray();
        var paths = Directory.GetFiles(Path.Combine(root,"docs/screens"),"*.svg",SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root,"ui-svg"),"*.svg"))
            .Select(p => Path.GetRelativePath(root,p).Replace('\\','/'))
            .Concat(["docs/ui-asset-manifest.json","docs/ui-screens-and-storyboards.md","docs/Android MCP Bridge Design.md","storyboards/storyboard.md","docs/storyboards.html"])
            .Order(StringComparer.Ordinal).ToArray();
        // This fixture was copied from the frozen original and edited explicitly
        // before the real test binding. It is never generated by production code.
        // Every saved SVG, deck and other expectation remains the pinned bytes.
        var fixtureRoot=Path.Combine(Workspace,"remediation/nuke-planning/docs");
        var fixtureReceipt=JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot,"migrated-manifest-fixture-receipt.json")))!;
        var originalFixture=File.ReadAllBytes(Path.Combine(fixtureRoot,fixtureReceipt["originalFixture"]!.GetValue<string>()));
        var migratedFixture=File.ReadAllBytes(Path.Combine(fixtureRoot,fixtureReceipt["migratedFixture"]!.GetValue<string>()));
        if(Hash(originalFixture)!=ExpectedHashes["remediation/design-c4/docs/ui-asset-manifest.json"]||Hash(originalFixture)!=fixtureReceipt["originalSha256"]!.GetValue<string>()||Hash(migratedFixture)!=fixtureReceipt["migratedSha256"]!.GetValue<string>())
            throw new InvalidOperationException("Independent manifest fixture hash mismatch");
        Correct = new(paths.Select(Read).Select(artifact =>
        {
            if (artifact.RelativePath != "docs/ui-asset-manifest.json") return artifact;
            return artifact with { Bytes=migratedFixture };
        }).ToArray());
    }

    public static string CurrentSourceHash(string inventoryPath,string originalHash)
    {
        var migration=ToolingDocumentationMigrationCases.Files.SingleOrDefault(file=>"remediation/design-c4/"+file.CanonicalPath==inventoryPath);
        if(migration is null)return originalHash;
        Assert.Equal(migration.BeforeSha256,originalHash);
        return migration.AfterSha256;
    }
    public static string Text(JsonObject node,string key) => node[key]?.GetValue<string>() ?? "";
    public static string Utf8(Artifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.Span);
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static Artifact Get(DocumentationProjection projection,string path) => projection.Artifacts.Single(a => a.RelativePath == path);
    public static string Content(DocumentationProjection projection,string path) => Utf8(Get(projection,path));
    public static XElement Svg(DocumentationProjection projection,string path) => XElement.Parse(Content(projection,path));
    public static string Normalize(string value) => Regex.Replace(value,"\\s+"," ").Trim();
    public static string SvgText(XElement root) => Normalize(string.Join(" ",root.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value)));
    public static string VisibleHtml(string html) => Normalize(WebUtility.HtmlDecode(Regex.Replace(html,"<[^>]+>"," ")));
    public static void Require(bool condition,string id,string detail)
    { if (!condition) throw new DocumentationContractException(id + ": " + detail); }
    public static DocumentationProjection Change(DocumentationProjection projection,string path,Func<string,string> mutation) =>
        new(projection.Artifacts.Select(a => a.RelativePath != path ? a : a with { Bytes = Encoding.UTF8.GetBytes(mutation(Utf8(a))) }).ToArray());
    public static DocumentationProjection ChangeSvg(DocumentationProjection projection,string path,Action<XElement> mutation) =>
        Change(projection,path,text => { var root = XElement.Parse(text);mutation(root);return root.ToString(SaveOptions.DisableFormatting); });
    public static string[] Values(JsonNode? node) => node!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
    public static string[] HtmlSections(DocumentationProjection projection) => Regex.Matches(Content(projection,"docs/storyboards.html"),"<section\\b[^>]*>[\\s\\S]*?</section>",RegexOptions.IgnoreCase).Select(m => m.Value).ToArray();
}

public sealed class ScriptedDocumentationProjector(DocumentationProjection response) : IDocumentationProjector
{
    public DocumentationInput? Received { get; private set; }
    public int Calls { get; private set; }
    public DocumentationProjection Project(DocumentationInput input) { Calls++;Received = input;return response; }
}

public static class DocumentationProjectionAssertions
{
    public static IEnumerable<object[]> Cases => Create().Select(c => new object[] { c });

    public static IReadOnlyList<ProjectionAssertionCase> Create()
    {
        var f = DocumentationFixture.Current;
        var cases = new List<ProjectionAssertionCase>();
        void Add(string id,Action<DocumentationProjection> check,Func<DocumentationProjection,DocumentationProjection> bad) => cases.Add(new(id,check,bad));
        static void R(bool pass,string id,string reason) => DocumentationFixture.Require(pass,id,reason);
        static string C(DocumentationProjection p,string path) => DocumentationFixture.Content(p,path);
        static DocumentationProjection M(DocumentationProjection p,string path,Func<string,string> mutate) => DocumentationFixture.Change(p,path,mutate);
        const string html = "docs/storyboards.html", manifest = "docs/ui-asset-manifest.json", reference = "docs/ui-screens-and-storyboards.md", deck = "storyboards/storyboard.md", design = "docs/Android MCP Bridge Design.md";
        var expectedPaths = f.Correct.Artifacts.Select(a => a.RelativePath).Order(StringComparer.Ordinal).ToArray();
        Add("UI-PATHS",p => R(p.Artifacts.Select(a=>a.RelativePath).Order(StringComparer.Ordinal).SequenceEqual(expectedPaths),"UI-PATHS","Exact projection artifact set"),p=>new(p.Artifacts.Skip(1).ToArray()));
        Add("UI-UNIQUE",p=>R(p.Artifacts.Select(a=>a.RelativePath).Distinct(StringComparer.Ordinal).Count()==p.Artifacts.Count,"UI-UNIQUE","Duplicate output path"),p=>new(p.Artifacts.Concat([p.Artifacts[0]]).ToArray()));
        Add("UI-MANIFEST",p=>{var projected=JsonNode.Parse(C(p,manifest))!.AsObject();projected.Remove("state_svg_files");R(JsonNode.DeepEquals(f.Blueprint,projected),"UI-MANIFEST","Projection differs from canonical blueprint");},p=>M(p,manifest,s=>s.Replace("\"schema_version\": \"2.0.0\"","\"schema_version\": \"9.0.0\"",StringComparison.Ordinal)));
        var expectedVariantPaths = f.Screens.SelectMany(s=>s["state_specs"]!.AsObject().Select(v=>"screens/states/"+v.Key+".svg")).Concat(f.Steps.Select(s=>"screens/steps/"+DocumentationFixture.Text(s,"step_id")+".svg")).Order(StringComparer.Ordinal).ToArray();
        Add("UI-VARIANT-MANIFEST",p=>R(DocumentationFixture.Values(JsonNode.Parse(C(p,manifest))!["state_svg_files"]).Order(StringComparer.Ordinal).SequenceEqual(expectedVariantPaths),"UI-VARIANT-MANIFEST","Exact state/step manifest"),p=>M(p,manifest,s=>{var j=JsonNode.Parse(s)!;j["state_svg_files"]!.AsArray().RemoveAt(0);return j.ToJsonString();}));
        Add("UI-DESIGN-BOUNDARY",p=>{static string Outside(string s)=>Regex.Replace(s,Regex.Escape(DocumentationFixture.NavigationBegin)+"[\\s\\S]*?"+Regex.Escape(DocumentationFixture.NavigationEnd),"<generated>");R(Outside(C(p,design))==Outside(DocumentationFixture.Utf8(f.Input.DesignMarkdown)),"UI-DESIGN-BOUNDARY","Unmarked design bytes changed");},p=>M(p,design,s=>"changed\n"+s));
        foreach(var screen in f.Screens)
        {
            var sid=DocumentationFixture.Text(screen,"id");var overview="docs/screens/"+sid+".svg";
            Add("UI-COPY-"+sid,p=>R(DocumentationFixture.Get(p,overview).Bytes.Span.SequenceEqual(DocumentationFixture.Get(p,"ui-svg/"+sid+".svg").Bytes.Span),"UI-COPY-"+sid,"Compatibility copy differs"),p=>M(p,"ui-svg/"+sid+".svg",s=>s+" "));
            Add("UI-STATUS-"+sid,p=>R(DocumentationFixture.Svg(p,overview).Descendants().Count(e=>(string?)e.Attribute("data-status-id")==sid+".STATUS")==1,"UI-STATUS-"+sid,"Overview status chip missing/duplicated"),p=>DocumentationFixture.ChangeSvg(p,overview,r=>r.Descendants().Single(e=>(string?)e.Attribute("data-status-id")==sid+".STATUS").Remove()));
            foreach(var control in screen["controls"]!.AsObject())
            {
                var cid=control.Key;var label=control.Value!.GetValue<string>();var spec=screen["control_specs"]![cid]!.AsObject();
                Add("UI-CONTROL-"+cid,p=>R(DocumentationFixture.Svg(p,overview).Descendants().Count(e=>(string?)e.Attribute("data-control-id")==cid)==1,"UI-CONTROL-"+cid,"Overview control identity missing/duplicated"),p=>DocumentationFixture.ChangeSvg(p,overview,r=>r.Descendants().Single(e=>(string?)e.Attribute("data-control-id")==cid).SetAttributeValue("data-control-id","wrong")));
                foreach(var field in new[]{"behavior","fields","error","cancel"})
                {
                    var value=DocumentationFixture.Text(spec,field);var id="UI-REFERENCE-"+cid+"-"+field;
                    Add(id,p=>R(DocumentationFixture.Normalize(C(p,reference)).Contains(DocumentationFixture.Normalize(value),StringComparison.Ordinal),id,"Control contract text omitted"),p=>M(p,reference,s=>s.Replace(value,"[omitted]",StringComparison.Ordinal)));
                }
                var navigationId="UI-NAVIGATION-"+cid;
                Add(navigationId,p=>{var region=Regex.Match(C(p,design),Regex.Escape(DocumentationFixture.NavigationBegin)+"([\\s\\S]*?)"+Regex.Escape(DocumentationFixture.NavigationEnd));R(region.Success&&region.Groups[1].Value.Contains(cid+" "+label,StringComparison.Ordinal),navigationId,"Navigation does not contain current control and label");},p=>M(p,design,s=>s.Replace(cid+" "+label,"[omitted]",StringComparison.Ordinal)));
            }
            foreach(var state in screen["state_specs"]!.AsObject())
            {
                var stateId=state.Key;var spec=state.Value!.AsObject();var path="docs/screens/states/"+stateId+".svg";
                Add("UI-STATE-ID-"+stateId,p=>R((string?)DocumentationFixture.Svg(p,path).Attribute("data-screen-state")==stateId,"UI-STATE-ID-"+stateId,"Wrong state identity"),p=>DocumentationFixture.ChangeSvg(p,path,r=>r.SetAttributeValue("data-screen-state","wrong")));
                foreach(var enabled in new[]{true,false})
                {
                    var flag=enabled?"true":"false";var key=enabled?"enabled_controls":"disabled_controls";var expected=DocumentationFixture.Values(spec[key]).Order(StringComparer.Ordinal).ToArray();var id="UI-ENABLEMENT-"+stateId+"-"+flag;
                    Add(id,p=>R(DocumentationFixture.Svg(p,path).Descendants().Where(e=>(string?)e.Attribute("data-enabled")==flag).Select(e=>(string?)e.Attribute("data-control-id")??"").Order(StringComparer.Ordinal).SequenceEqual(expected),id,"Exact enabled/disabled set differs"),p=>DocumentationFixture.ChangeSvg(p,path,r=>{var ns=r.Name.Namespace;r.Add(new XElement(ns+"g",new XAttribute("data-control-id","unexpected"),new XAttribute("data-enabled",flag)));}));
                }
                var expectedHealth=JsonNode.Parse((string)DocumentationFixture.Svg(f.Correct,path).Descendants().Single(e=>e.Attribute("data-health-snapshot") is not null).Attribute("data-health-snapshot")!)!;
                Add("UI-HEALTH-"+stateId,p=>{var health=JsonNode.Parse((string)DocumentationFixture.Svg(p,path).Descendants().Single(e=>e.Attribute("data-health-snapshot") is not null).Attribute("data-health-snapshot")!)!;R(JsonNode.DeepEquals(expectedHealth,health),"UI-HEALTH-"+stateId,"State health or integer revision drift");},p=>DocumentationFixture.ChangeSvg(p,path,r=>{var e=r.Descendants().Single(e=>e.Attribute("data-health-snapshot") is not null);var j=JsonNode.Parse((string)e.Attribute("data-health-snapshot")!)!;j["revision"]= -1;e.SetAttributeValue("data-health-snapshot",j.ToJsonString());}));
                foreach(var field in new[]{"effect","recovery"})
                {
                    var expected=DocumentationFixture.Normalize(DocumentationFixture.Text(spec,field));var id="UI-STATE-TEXT-"+stateId+"-"+field;
                    Add(id,p=>R(DocumentationFixture.SvgText(DocumentationFixture.Svg(p,path)).Contains(expected,StringComparison.Ordinal),id,"State contract text absent"),p=>DocumentationFixture.ChangeSvg(p,path,r=>{foreach(var e in r.Descendants().Where(e=>e.Name.LocalName=="text").ToArray())e.Value="[omitted]";}));
                }
            }
        }
        foreach(var story in f.Blueprint["storyboards"]!.AsArray())
        {
            var storyId=story!["id"]!.GetValue<string>();var path="docs/screens/"+storyId+".svg";var expected=story["steps"]!.AsArray().Select(s=>s!["step_id"]!.GetValue<string>()).ToArray();
            Add("UI-STORY-ORDER-"+storyId,p=>R(DocumentationFixture.Svg(p,path).Descendants().Where(e=>e.Attribute("data-step-id")is not null).Select(e=>(string)e.Attribute("data-step-id")!).SequenceEqual(expected),"UI-STORY-ORDER-"+storyId,"Composite step order differs"),p=>DocumentationFixture.ChangeSvg(p,path,r=>r.Descendants().First(e=>e.Attribute("data-step-id")is not null).SetAttributeValue("data-step-id","wrong")));
            Add("UI-COPY-"+storyId,p=>R(DocumentationFixture.Get(p,path).Bytes.Span.SequenceEqual(DocumentationFixture.Get(p,"ui-svg/"+storyId+".svg").Bytes.Span),"UI-COPY-"+storyId,"Storyboard copy differs"),p=>M(p,"ui-svg/"+storyId+".svg",s=>s+" "));
        }
        Add("DECK-SLIDE-COUNT",p=>R(DocumentationFixture.HtmlSections(p).Length==108,"DECK-SLIDE-COUNT","Cover plus107steps required"),p=>M(p,html,s=>Regex.Replace(s,"<section\\b[^>]*>[\\s\\S]*?</section>","",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(5))));
        Add("DECK-IMAGE-COUNT",p=>R(Regex.Matches(C(p,html),"<img\\b[^>]*alt=\"Screen\"",RegexOptions.IgnoreCase).Count==107,"DECK-IMAGE-COUNT","Every step requires an actual image"),p=>M(p,html,s=>s.Replace("alt=\"Screen\"","alt=\"missing\"",StringComparison.Ordinal)));
        Add("DECK-LOCAL",p=>R(!Regex.IsMatch(C(p,html),"(?:src|href)=[\"'](?:https?:)?//|url\\(\\s*[\"']?(?:https?:)?//",RegexOptions.IgnoreCase),"DECK-LOCAL","External asset dependency"),p=>M(p,html,s=>s+"<img src=\"https://example.invalid/pixel\">"));
        Add("DECK-NO-RAW-MARKDOWN",p=>R(!C(p,html).Contains("![Screen]",StringComparison.Ordinal),"DECK-NO-RAW-MARKDOWN","Raw image Markdown"),p=>M(p,html,s=>s+"![Screen](missing.svg)"));
        Add("DECK-STANDARD-IMAGES",p=>R(!C(p,deck).Contains("![bg ",StringComparison.Ordinal)&&Regex.Matches(C(p,deck),"!\\[Screen]\\(").Count==107,"DECK-STANDARD-IMAGES","Standard editable SVG image references"),p=>M(p,deck,s=>s.Replace("![Screen]","![bg Screen]",StringComparison.Ordinal)));
        for(var index=0;index<f.Steps.Count;index++)
        {
            var i=index;var step=f.Steps[i];var stepId=DocumentationFixture.Text(step,"step_id");var path="docs/screens/steps/"+stepId+".svg";
            foreach(var attribute in new[]{("data-screen-state","state_id"),("data-focus-control","control_id")})
            {
                var (name,key)=attribute;var expected=DocumentationFixture.Text(step,key);var id="UI-STEP-"+stepId+"-"+key;
                Add(id,p=>R((string?)DocumentationFixture.Svg(p,path).Attribute(name)==expected,id,"Step anchor drift"),p=>DocumentationFixture.ChangeSvg(p,path,r=>r.SetAttributeValue(name,"wrong")));
            }
            var embeddedId="DECK-EMBEDDED-"+stepId;
            Add(embeddedId,p=>{var sections=DocumentationFixture.HtmlSections(p);var image=Regex.Match(sections[i+1],"data:image/svg\\+xml;base64,([A-Za-z0-9+/=]+)");R(image.Success&&Convert.FromBase64String(image.Groups[1].Value).AsSpan().SequenceEqual(DocumentationFixture.Get(p,path).Bytes.Span),embeddedId,"Exact corresponding SVG bytes not embedded");},p=>M(p,html,s=>s.Replace(Convert.ToBase64String(DocumentationFixture.Get(p,path).Bytes.Span),Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg/>")),StringComparison.Ordinal)));
            foreach(var field in new[]{"actor","state_id","branch","control_id","action","result","error","cancel","next_success","next_error"})
            {
                var expected=DocumentationFixture.Normalize(DocumentationFixture.Text(step,field));var id="DECK-TEXT-"+stepId+"-"+field;
                Add(id,p=>R(DocumentationFixture.VisibleHtml(DocumentationFixture.HtmlSections(p)[i+1]).Contains(expected,StringComparison.Ordinal),id,"Ordered storyboard field omitted"),p=>M(p,html,s=>{var sections=DocumentationFixture.HtmlSections(p);return s.Replace(sections[i+1],"<section>[omitted]</section>",StringComparison.Ordinal);}));
            }
        }
        foreach(var artifact in f.Correct.Artifacts.Where(a=>a.RelativePath.EndsWith(".svg",StringComparison.Ordinal)))
        {
            var path=artifact.RelativePath;var id="UI-VECTOR-"+path;
            Add(id,p=>{var root=DocumentationFixture.Svg(p,path);R(root.Name.LocalName=="svg"&&root.Descendants().Any(e=>e.Name.LocalName=="text")&&!root.Descendants().Any(e=>e.Name.LocalName is "image" or "script")&&!C(p,path).Contains("<!--SRC=[",StringComparison.Ordinal),id,"Editable vector text only");},p=>DocumentationFixture.ChangeSvg(p,path,r=>r.Add(new XElement(r.Name.Namespace+"image"))));
        }
        return cases;
    }

    public static void AssertAll(IDocumentationProjector provider,DocumentationInput input)
    {
        var result=provider.Project(input);
        foreach(var contract in Create()) contract.Assert(result);
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationProjectionMockTests
{
    [Theory,MemberData(nameof(DocumentationProjectionAssertions.Cases),MemberType=typeof(DocumentationProjectionAssertions))]
    public void CorrectOriginalFixtureResponsesPassEveryAssertion(ProjectionAssertionCase test)
    {
        var fixture=DocumentationFixture.Current;
        var mock=new ScriptedDocumentationProjector(fixture.Correct);
        test.Assert(mock.Project(fixture.Input));
        Assert.Same(fixture.Input,mock.Received);
        Assert.Equal(1,mock.Calls);
    }

    [Theory,MemberData(nameof(DocumentationProjectionAssertions.Cases),MemberType=typeof(DocumentationProjectionAssertions))]
    public void IndividualIncorrectResponsesAreRejectedByTheirOwnAssertion(ProjectionAssertionCase test)
    {
        var fixture=DocumentationFixture.Current;
        var mock=new ScriptedDocumentationProjector(test.IncorrectResponse(fixture.Correct));
        var failure=Assert.Throws<DocumentationContractException>(()=>test.Assert(mock.Project(fixture.Input)));
        Assert.StartsWith(test.Id+":",failure.Message,StringComparison.Ordinal);
        Assert.Same(fixture.Input,mock.Received);
        Assert.Equal(1,mock.Calls);
    }

    [Fact]
    public void FixtureHasFullOriginalEntityDenominatorAndLegacyInventory()
    {
        var f=DocumentationFixture.Current;
        Assert.Equal(11,f.Screens.Count);
        Assert.Equal(88,f.Screens.Sum(s=>s["controls"]!.AsObject().Count));
        Assert.Equal(76,f.Screens.Sum(s=>s["state_specs"]!.AsObject().Count));
        Assert.Equal(6,f.Blueprint["storyboards"]!.AsArray().Count);
        Assert.Equal(107,f.Steps.Count);
        Assert.Equal(221,f.Inventory["docsVectorFixtures"]!.AsArray().Count);
        Assert.Equal(58,f.Inventory["legacyTestInventory"]!.AsArray().Sum(n=>n!["cases"]!.AsArray().Count));
        Assert.Equal(222,f.Correct.Artifacts.Count);
        Assert.Equal(217,f.Correct.Artifacts.Count(a=>a.RelativePath.EndsWith(".svg",StringComparison.Ordinal)));
        Assert.Equal(DocumentationProjectionAssertions.Create().Count,DocumentationProjectionAssertions.Create().Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ApprovedHealthMigrationChangesOnlyTwelveExistingHealthFields()
    {
        var f=DocumentationFixture.Current;
        var originalBytes=File.ReadAllBytes(Path.Combine(f.Workspace,"remediation/nuke-planning/docs",new DocumentationFixtureCarrierMap().Resolve("ui-blueprint.pre-health-migration.md")));
        Assert.Equal(f.HealthMigration["beforeSha256"]!.GetValue<string>(),DocumentationFixture.Hash(originalBytes));
        var originalBlock=Regex.Match(Encoding.UTF8.GetString(originalBytes),"```json ui-blueprint-v2\\n([\\s\\S]*?)\\n```").Groups[1].Value;
        var current=f.Blueprint.DeepClone();
        Assert.Equal(12,f.HealthMigration["states"]!.AsArray().Count);
        foreach(var delta in f.HealthMigration["states"]!.AsArray())
        {
            var id=delta!["stateId"]!.GetValue<string>();
            var state=current["screens"]!.AsArray().SelectMany(s=>s!["state_specs"]!.AsObject()).Single(p=>p.Key==id).Value!.AsObject();
            Assert.True(JsonNode.DeepEquals(delta["health"],state["health"]));
            var saved=JsonNode.Parse((string)DocumentationFixture.Svg(f.Correct,"docs/screens/states/"+id+".svg").Descendants().Single(e=>e.Attribute("data-health-snapshot")is not null).Attribute("data-health-snapshot")!)!;
            foreach(var pair in delta["health"]!.AsObject()) Assert.True(JsonNode.DeepEquals(pair.Value,saved[pair.Key]),id+" "+pair.Key);
            Assert.True(state.Remove("health"));
        }
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(originalBlock),current));
    }
}
