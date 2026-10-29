using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Pure archive/XML evidence. Scoped lookup and annotation attachment remain in Catalog.</summary>
public sealed class SdkAnnotationArchiveSourceFacts(IAnnotationTargetSpellingSourceFacts targets, ISourceXmlFormatter xml) : ISdkAnnotationArchiveSourceFacts
{
    public JsonObject Parse(ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray();
        // Python ZipFile.read(name) resolves the LAST duplicate member. The
        // namelist still repeats that name and therefore processes it twice.
        var selected = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries) selected[entry.FullName] = entry;
        var items = new JsonArray(); var issues = new JsonArray(); var documents = 0; var annotations = 0; var partial = false;
        void Issue(string code, string reason, string member, string raw, string encoding = "utf-8") => issues.Add(new JsonObject
        { ["code"] = code, ["reason"] = reason, ["archive_member"] = member, ["raw"] = raw, ["raw_encoding"] = encoding });
        foreach (var name in names)
        {
            if (!name.EndsWith(".xml", StringComparison.Ordinal))
            { Issue("unsupported", "Unrecognized annotation archive member", name, name); continue; }
            byte[] member;
            try { member = MetadataArchiveReader.ReadMember(selected[name], 8 * 1024 * 1024); }
            catch (TransportException error) when (error.Code is "METADATA_LIMIT" or "ZIP_CRC")
            { throw new InvalidDataException(error.Message, error); }
            string text;
            try { text = SourceText.DecodeUtf8(member); }
            catch (DecoderFallbackException)
            { partial = true; Issue("annotation-xml", "Non-UTF8 external annotation XML retained losslessly", name, Convert.ToBase64String(member), "base64"); continue; }
            XElement root;
            try { root = SourceXml.Load(text); }
            catch (FormatException)
            { partial = true; Issue("annotation-xml", "Malformed external annotation XML", name, text); continue; }
            documents++; var itemOrdinal = 0;
            foreach (var item in root.Elements("item"))
            {
                var facts = targets.NormalizeTarget(item.Attribute("name")?.Value ?? "");
                facts["archive_member"] = name; facts["item_ordinal"] = itemOrdinal++;
                var annotationFacts = new JsonArray(); var annotationOrdinal = 0;
                foreach (var annotation in item.Elements("annotation"))
                {
                    var values = annotation.Elements("val").Select(value => SourceXml.Json(SourceXml.Attributes(value))).ToArray();
                    annotationFacts.Add(new JsonObject
                    {
                        ["annotation_ordinal"] = annotationOrdinal++, ["name"] = annotation.Attribute("name")?.Value ?? "",
                        ["arguments"] = string.Join(',', values.Select(value => (value["name"]?.GetValue<string>() ?? "") + "=" + (value["val"]?.GetValue<string>() ?? ""))),
                        ["raw"] = xml.Serialize(SourceXml.ElementEvidence(annotation)), ["values"] = new JsonArray(values.Cast<JsonNode>().ToArray())
                    });
                    annotations++;
                }
                facts["annotations"] = annotationFacts; items.Add(facts);
            }
        }
        return new JsonObject
        {
            ["status"] = partial ? "partial" : "parsed", ["members"] = new JsonArray(names.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray()),
            ["items"] = items, ["counts"] = new JsonObject { ["xml_documents"] = documents, ["items"] = items.Count, ["annotations"] = annotations }, ["issues"] = issues
        };
    }
}
