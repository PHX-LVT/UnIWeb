using FullProject.Models;
using System.IO.Compression;
using System.Xml;

namespace FullProject.Services.FormServices;

public sealed class FormSubmissionExportService
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public byte[] BuildXlsx(
        IReadOnlyCollection<FormSubmission> submissions,
        IReadOnlyCollection<FormDefinition>? definitions = null,
        string language = "en")
    {
        var exportLanguage = NormalizeLanguage(language);
        var rows = submissions
            .OrderByDescending(submission => submission.SubmittedAt)
            .ToList();
        var fieldColumns = BuildFieldColumns(rows, definitions ?? Array.Empty<FormDefinition>(), exportLanguage);
        var table = BuildTable(rows, fieldColumns);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", ContentTypesXml());
            WriteTextEntry(archive, "_rels/.rels", RootRelationshipsXml());
            WriteTextEntry(archive, "docProps/app.xml", AppXml());
            WriteTextEntry(archive, "docProps/core.xml", CoreXml());
            WriteTextEntry(archive, "xl/workbook.xml", WorkbookXml());
            WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml());
            WriteWorksheet(archive, "xl/worksheets/sheet1.xml", table);
        }

        return stream.ToArray();
    }

    private static List<ExportFieldColumn> BuildFieldColumns(
        IEnumerable<FormSubmission> submissions,
        IReadOnlyCollection<FormDefinition> definitions,
        string language)
    {
        var columns = new Dictionary<string, ExportFieldColumn>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in submissions
                     .SelectMany(submission => submission.Fields.Select(field => new { Submission = submission, Field = field }))
                     .OrderBy(entry => entry.Field.Order))
        {
            var field = entry.Field;
            var key = Clean(field.Key);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var isDeletedField = IsDeletedField(entry.Submission, field, definitions);
            var label = ResolveFieldLabel(entry.Submission, field, definitions, language, isDeletedField);
            if (!columns.TryGetValue(key, out var column))
            {
                column = new ExportFieldColumn
                {
                    Key = key,
                    Header = string.IsNullOrWhiteSpace(label) || string.Equals(label, key, StringComparison.OrdinalIgnoreCase)
                        ? key
                        : $"{label} ({key})"
                };
                columns[key] = column;
            }

            if (isDeletedField)
                column.HasDeletedSnapshot = true;
        }

        return columns.Values.ToList();
    }

    private static List<List<string>> BuildTable(
        IEnumerable<FormSubmission> submissions,
        IReadOnlyList<ExportFieldColumn> fieldColumns)
    {
        var table = new List<List<string>>
        {
            new()
            {
                "Submitted Time",
                "Form Name",
                "Form Key",
                "Status",
                "Source Page",
                "Language",
                "Internal Notes"
            }
        };
        table[0].AddRange(fieldColumns.Select(column => column.DisplayHeader));

        foreach (var submission in submissions)
        {
            var fieldsByKey = submission.Fields
                .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(" | ", group
                        .Select(field => Clean(field.Value))
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                    StringComparer.OrdinalIgnoreCase);

            var row = new List<string>
            {
                submission.SubmittedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Clean(submission.FormName),
                Clean(submission.FormKey),
                submission.Status.ToString(),
                Clean(submission.SourcePage),
                Clean(submission.Language),
                Clean(submission.InternalNotes)
            };

            row.AddRange(fieldColumns.Select(column =>
                fieldsByKey.TryGetValue(column.Key, out var value) ? value : string.Empty));
            table.Add(row);
        }

        return table;
    }

    private static void WriteWorksheet(ZipArchive archive, string path, IReadOnlyList<List<string>> table)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = System.Text.Encoding.UTF8,
            Indent = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetData");

        for (var rowIndex = 0; rowIndex < table.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString());

            var row = table[rowIndex];
            for (var colIndex = 0; colIndex < row.Count; colIndex++)
            {
                WriteInlineCell(writer, colIndex, rowNumber, row[colIndex]);
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteInlineCell(XmlWriter writer, int colIndex, int rowNumber, string value)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", $"{ColumnName(colIndex)}{rowNumber}");
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteAttributeString("xml", "space", null, "preserve");
        writer.WriteString(TrimExcelCell(value));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string ColumnName(int index)
    {
        var dividend = index + 1;
        var name = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            dividend = (dividend - modulo) / 26;
        }

        return name;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeLanguage(string? language)
    {
        var clean = language?.Trim().ToLowerInvariant() ?? "en";
        return clean.Length is > 0 and <= 12 ? clean : "en";
    }

    private static string TrimExcelCell(string? value)
    {
        var clean = Clean(value);
        return clean.Length <= 32767 ? clean : clean[..32767];
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
        writer.Write(content);
    }

    private static string ContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private static string RootRelationshipsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
        </Relationships>
        """;

    private static string AppXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
          <Application>UIWEB AdminSite</Application>
        </Properties>
        """;

    private static string CoreXml() =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:creator>UIWEB AdminSite</dc:creator>
          <cp:lastModifiedBy>UIWEB AdminSite</cp:lastModifiedBy>
          <dcterms:created xsi:type="dcterms:W3CDTF">{DateTime.UtcNow:O}</dcterms:created>
          <dcterms:modified xsi:type="dcterms:W3CDTF">{DateTime.UtcNow:O}</dcterms:modified>
        </cp:coreProperties>
        """;

    private static string WorkbookXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Form Submissions" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string WorkbookRelationshipsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    private static bool IsDeletedField(
        FormSubmission submission,
        FormSubmissionFieldSnapshot field,
        IReadOnlyCollection<FormDefinition> definitions)
    {
        if (definitions.Count == 0)
            return false;

        var definition = ResolveDefinitionForSubmission(submission, definitions);
        if (definition is null)
            return false;

        return !definition.Fields.Any(activeField =>
            string.Equals(activeField.Key, field.Key, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFieldLabel(
        FormSubmission submission,
        FormSubmissionFieldSnapshot field,
        IReadOnlyCollection<FormDefinition> definitions,
        string language,
        bool isDeletedField)
    {
        if (!isDeletedField && definitions.Count > 0)
        {
            var definition = ResolveDefinitionForSubmission(submission, definitions);
            var activeField = definition?.Fields.FirstOrDefault(item =>
                string.Equals(item.Key, field.Key, StringComparison.OrdinalIgnoreCase));
            if (activeField is not null)
                return Clean(FormValidationService.ResolveText(activeField.Label, language, field.Label));
        }

        return Clean(field.Label);
    }

    private static FormDefinition? ResolveDefinitionForSubmission(
        FormSubmission submission,
        IReadOnlyCollection<FormDefinition> definitions)
    {
        if (!string.IsNullOrWhiteSpace(submission.FormId))
        {
            var byId = definitions.FirstOrDefault(item =>
                string.Equals(item.Id, submission.FormId, StringComparison.Ordinal));
            if (byId is not null)
                return byId;
        }

        return definitions.FirstOrDefault(item =>
            string.Equals(item.Key, submission.FormKey, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ExportFieldColumn
    {
        public string Key { get; init; } = string.Empty;
        public string Header { get; init; } = string.Empty;
        public bool HasDeletedSnapshot { get; set; }

        public string DisplayHeader =>
            HasDeletedSnapshot ? $"{Header} [old/deleted]" : Header;
    }
}
