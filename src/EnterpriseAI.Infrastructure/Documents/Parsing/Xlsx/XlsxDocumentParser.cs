using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EnterpriseAI.Application.Abstractions.Parsing;

namespace EnterpriseAI.Infrastructure.Documents.Parsing.Xlsx;

public sealed class XlsxDocumentParser : IDocumentParser
{
    private const string XlsxExtension = ".xlsx";

    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int MinimumUsefulCharacterCount = 20;
    private const int MaximumRowsPerFallbackSection = 25;

    private static readonly string[] MetadataLabels =
    [
        "Rendszer",
        "Végrehajtó",
        "Készítette",
        "Frissítés dátuma"
    ];

    private static readonly Regex WhitespaceRegex =
        new(
            @"[^\S\r\n]+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex ExcessiveLineBreakRegex =
        new(
            @"\n{3,}",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex StepNumberRegex =
        new(
            @"^\s*(?<number>\d{1,4})(?:[.)])?\s*$",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    public bool CanParse(string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);

        if (string.Equals(extension, XlsxExtension, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            mimeType?.Trim(),
            XlsxMimeType,
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentParseResult> ParseAsync(
        DocumentParseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceDocument);
        ArgumentNullException.ThrowIfNull(request.Content);

        cancellationToken.ThrowIfCancellationRequested();

        if (!CanParse(request.SourceDocument.FileName, request.SourceDocument.MimeType))
        {
            return DocumentParseResult.Unsupported(
                $"The file '{request.SourceDocument.FileName}' is not a supported XLSX workbook.");
        }

        if (!request.Content.CanRead)
        {
            throw new ArgumentException("XLSX content stream must be readable.", nameof(request));
        }

        await using var workbookContent = new MemoryStream();

        await request.Content.CopyToAsync(workbookContent, cancellationToken);

        if (workbookContent.Length == 0)
        {
            throw new InvalidOperationException("XLSX content stream is empty.");
        }

        workbookContent.Position = 0;

        try
        {
            using var spreadsheet = SpreadsheetDocument.Open(workbookContent, isEditable: false);

            var workbookPart = spreadsheet.WorkbookPart
                ?? throw new InvalidOperationException(
                    "The XLSX workbook does not contain a workbook part.");

            var sharedStrings = ReadSharedStrings(workbookPart);

            var sections = new List<ParsedSection>();
            var documentTitle =
                request.SourceDocument.Title;

            var workbook = workbookPart.Workbook
                ?? throw new InvalidOperationException("The XLSX workbook does not contain a workbook definition.");

            var sheets = workbook
                .Sheets?
                .Elements<Sheet>()
                .ToArray()
                ?? [];

            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sheet.Id?.Value is null)
                {
                    continue;
                }

                if (workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
                {
                    continue;
                }

                var worksheetName = NormalizeText(sheet.Name?.Value);

                if (string.IsNullOrWhiteSpace(worksheetName))
                {
                    worksheetName = "Munkalap";
                }

                var rows = ReadRows(
                    worksheetPart,
                    workbookPart,
                    sharedStrings,
                    cancellationToken);

                if (rows.Count == 0)
                {
                    continue;
                }

                var structuredResult = TryParseInstructionTemplate(
                        rows,
                        worksheetName,
                        sections.Count,
                        cancellationToken);

                if (structuredResult is not null)
                {
                    foreach (var section in structuredResult.Sections)
                    {
                        sections.Add(
                            section with
                            {
                                Order = sections.Count
                            });
                    }

                    if (!string.IsNullOrWhiteSpace(structuredResult.DocumentTitle))
                    {
                        documentTitle = structuredResult.DocumentTitle;
                    }

                    continue;
                }

                AddFallbackSections(
                    worksheetName,
                    rows,
                    sections,
                    cancellationToken);
            }

            var usefulCharacterCount = sections.Sum(
                section =>
                    CountUsefulCharacters(section.Content)
                    + CountUsefulCharacters(section.Heading ?? string.Empty));

            if (usefulCharacterCount < MinimumUsefulCharacterCount)
            {
                return DocumentParseResult.Unsupported(
                    "The XLSX workbook contains no usable text or too little extractable content.");
            }

            var parsedDocument = new ParsedDocument(
                Title: documentTitle,
                Sections: sections);

            return DocumentParseResult.Success(parsedDocument);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidOperationException(
                $"The XLSX workbook '{request.SourceDocument.FileName}' could not be parsed.", exception);
        }
    }

    private static IReadOnlyList<string> ReadSharedStrings(WorkbookPart workbookPart)
    {
        var sharedStringTable = workbookPart
            .SharedStringTablePart?
            .SharedStringTable;

        if (sharedStringTable is null)
        {
            return [];
        }

        return sharedStringTable
            .Elements<SharedStringItem>()
            .Select(item => NormalizeText(item.InnerText))
            .ToArray();
    }

    private static IReadOnlyList<WorksheetRow> ReadRows(
        WorksheetPart worksheetPart,
        WorkbookPart workbookPart,
        IReadOnlyList<string> sharedStrings,
        CancellationToken cancellationToken)
    {
        var worksheet = worksheetPart.Worksheet;

        if (worksheet is null)
        {
            return [];
        }

        var sheetData = worksheet.GetFirstChild<SheetData>();

        if (sheetData is null)
        {
            return [];
        }

        var rows = new List<WorksheetRow>();

        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cellValues = new SortedDictionary<int, string>();

            foreach (var cell in row.Elements<Cell>())
            {
                var columnIndex = GetColumnIndex(cell.CellReference?.Value);

                if (columnIndex < 0)
                {
                    continue;
                }

                var value = ReadCellValue(cell, workbookPart, sharedStrings);


                if (!string.IsNullOrWhiteSpace(value))
                {
                    cellValues[columnIndex] = value;
                }
            }

            if (cellValues.Count == 0)
            {
                continue;
            }

            rows.Add(
                new WorksheetRow(
                    RowNumber: checked((int)(row.RowIndex?.Value ?? 0)),
                    Cells: cellValues));
        }

        return rows;
    }

    private static string ReadCellValue(
        Cell cell,
        WorkbookPart workbookPart,
        IReadOnlyList<string> sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return NormalizeText(cell.InlineString?.InnerText);
        }

        var rawValue = cell.CellValue?.Text;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return NormalizeText(cell.InnerText);
        }

        if (cell.DataType?.Value ==
            CellValues.SharedString
            && int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return rawValue == "1"
                ? "Igen"
                : "Nem";
        }

        if (IsDateCell(
                cell,
                workbookPart)
            && double.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var excelDateValue))
        {
            try
            {
                var date = DateTime.FromOADate(excelDateValue);

                return date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                // Érvénytelen Excel-dátum esetén
                // megtartjuk az eredeti cellaértéket.
            }
        }

        return NormalizeText(rawValue);
    }

    private static StructuredParseResult? TryParseInstructionTemplate(
            IReadOnlyList<WorksheetRow> rows,
            string worksheetName,
            int initialOrder,
            CancellationToken cancellationToken)
    {
        var metadata = ExtractMetadata(rows);
        var generalDescription = ExtractGeneralDescription(rows);
        var steps = ExtractSteps(rows, cancellationToken);
        var recognizedTemplate =
            metadata.Count >= 2
            || steps.Count >= 2
            || ContainsText(rows, "Általános leírás");

        if (!recognizedTemplate)
        {
            return null;
        }

        var title = GetMetadataValue(metadata, "Rendszer");

        if (string.IsNullOrWhiteSpace(title))
        {
            title = worksheetName;
        }

        var commonContext = BuildCommonContext(metadata, worksheetName);

        var sections = new List<ParsedSection>();

        if (!string.IsNullOrWhiteSpace(generalDescription) || metadata.Count > 0)
        {
            var content = JoinNonEmptyParts(commonContext, generalDescription);

            if (!string.IsNullOrWhiteSpace(content))
            {
                sections.Add(
                    new ParsedSection(
                        Heading: "Általános leírás",
                        Content: content,
                        PageNumber: null,
                        Order: initialOrder  + sections.Count));
            }
        }

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var heading = string.IsNullOrWhiteSpace(
                step.Title)
                ? $"{step.Number}. lépés"
                : $"{step.Number}. {step.Title}";

            var content = JoinNonEmptyParts(commonContext, step.Description);

            if (string.IsNullOrWhiteSpace(content))
            {
                content = "A lépéshez nem található külön szöveges leírás.";
            }

            sections.Add(
                new ParsedSection(
                    Heading: heading,
                    Content: content,
                    PageNumber: null,
                    Order: initialOrder + sections.Count));
        }

        return sections.Count == 0
            ? null
            : new StructuredParseResult(
                DocumentTitle: title,
                Sections: sections);
    }

    private static IReadOnlyDictionary<string, string>ExtractMetadata(IReadOnlyList<WorksheetRow> rows)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var label in MetadataLabels)
        {
            var value = FindValueForLabel(rows, label);

            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[label] = value;
            }
        }

        return metadata;
    }

    private static string? FindValueForLabel(IReadOnlyList<WorksheetRow> rows, string label)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            foreach (var cell in row.Cells)
            {
                if (!TextEqualsLabel(cell.Value, label))
                {
                    continue;
                }

                var sameRowValue = row.Cells
                    .Where(candidate => candidate.Key > cell.Key)
                    .Select(candidate => candidate.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !IsKnownLabel(value));

                if (!string.IsNullOrWhiteSpace(sameRowValue))
                {
                    return sameRowValue;
                }

                for (var nextRowIndex = rowIndex + 1; nextRowIndex < Math.Min(rows.Count, rowIndex + 4); nextRowIndex++)
                {
                    var nextRow = rows[nextRowIndex];

                    var value = nextRow.Cells
                        .Where(candidate => candidate.Key >= cell.Key)
                        .OrderBy(candidate => Math.Abs(candidate.Key - cell.Key))
                        .Select(candidate => candidate.Value)
                        .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)&& !IsKnownLabel(candidate));

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractGeneralDescription(IReadOnlyList<WorksheetRow> rows)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            var labelCell = row.Cells
                .FirstOrDefault(cell =>
                    TextEqualsLabel(
                        cell.Value,
                        "Általános leírás"));

            if (string.IsNullOrWhiteSpace(labelCell.Value))
            {
                continue;
            }

            var sameRowValues = row.Cells
                .Where(cell => cell.Key > labelCell.Key)
                .Select(cell => cell.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (sameRowValues.Length > 0)
            {
                return NormalizeText(string.Join(Environment.NewLine, sameRowValues));
            }

            for (var nextRowIndex = rowIndex + 1; nextRowIndex < rows.Count; nextRowIndex++)
            {
                if (TryGetStepNumber(rows[nextRowIndex], out _))
                {
                    break;
                }

                var text = GetRowText(rows[nextRowIndex]);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<InstructionStep>ExtractSteps(
            IReadOnlyList<WorksheetRow> rows,
            CancellationToken cancellationToken)
    {
        var steps = new List<InstructionStep>();

        StepBuilder? currentStep = null;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetStepNumber(row, out var stepNumber))
            {
                if (currentStep is not null)
                {
                    steps.Add(currentStep.Build());
                }

                currentStep = new StepBuilder(stepNumber);

                var values = row.Cells
                    .OrderBy(cell => cell.Key)
                    .Where(cell => !StepNumberRegex.IsMatch(cell.Value))
                    .Select(cell => cell.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

                currentStep.AddRow(values);
                continue;
            }

            if (currentStep is null)
            {
                continue;
            }

            currentStep.AddRow(
                row.Cells
                    .OrderBy(cell => cell.Key)
                    .Select(cell => cell.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray());
        }

        if (currentStep is not null)
        {
            steps.Add(currentStep.Build());
        }

        return steps;
    }

    private static bool TryGetStepNumber(WorksheetRow row, out int stepNumber)
    {
        stepNumber = 0;

        foreach (var cell in row.Cells.OrderBy(cell => cell.Key).Take(3))
        {
            var match = StepNumberRegex.Match(cell.Value);

            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(
                    match.Groups["number"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out stepNumber)
                && stepNumber > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddFallbackSections(
        string worksheetName,
        IReadOnlyList<WorksheetRow> rows,
        ICollection<ParsedSection> sections,
        CancellationToken cancellationToken)
    {
        var currentRows = new List<string>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowText = GetRowText(row);

            if (string.IsNullOrWhiteSpace(rowText))
            {
                continue;
            }

            currentRows.Add(rowText);

            if (currentRows.Count < MaximumRowsPerFallbackSection)
            {
                continue;
            }

            AddFallbackSection(worksheetName, currentRows, sections);

            currentRows.Clear();
        }

        AddFallbackSection(worksheetName, currentRows, sections);
    }

    private static void AddFallbackSection(
        string worksheetName,
        IReadOnlyCollection<string> rows,
        ICollection<ParsedSection> sections)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var content = NormalizeText(string.Join(Environment.NewLine, rows));

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        sections.Add(
            new ParsedSection(
                Heading: worksheetName,
                Content: content,
                PageNumber: null,
                Order: sections.Count));
    }

    private static string BuildCommonContext(
        IReadOnlyDictionary<string, string> metadata,
        string worksheetName)
    {
        var builder = new StringBuilder();

        builder.Append("Munkalap: ");
        builder.AppendLine(worksheetName);

        foreach (var label in MetadataLabels)
        {
            if (!metadata.TryGetValue(label, out var value))
            {
                continue;
            }

            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value);
        }

        return builder.ToString().Trim();
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(
            key,
            out var value)
            ? value
            : null;
    }

    private static bool ContainsText(IReadOnlyList<WorksheetRow> rows, string expectedText)
    {
        return rows.Any(row =>
            row.Cells.Values.Any(value =>
                TextEqualsLabel(value, expectedText)));
    }

    private static string GetRowText(WorksheetRow row)
    {
        return NormalizeText(
            string.Join(
                " | ",
                row.Cells
                    .OrderBy(cell => cell.Key)
                    .Select(cell => cell.Value)
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(
                            value))));
    }

    private static bool IsKnownLabel(string value)
    {
        return MetadataLabels.Any(label =>
                   TextEqualsLabel(value, label))
               || TextEqualsLabel(
                   value,
                   "Általános leírás")
               || TextEqualsLabel(
                   value,
                   "Lépés");
    }

    private static bool TextEqualsLabel(string value, string expectedLabel)
    {
        var normalizedValue = NormalizeLabel(value);
        var normalizedExpected = NormalizeLabel(expectedLabel);

        return string.Equals(
            normalizedValue,
            normalizedExpected,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLabel(string value)
    {
        return NormalizeText(value)
            .Trim()
            .TrimEnd(':')
            .Trim();
    }

    private static string JoinNonEmptyParts(params string?[] parts)
    {
        return string.Join(
            Environment.NewLine
            + Environment.NewLine,
            parts
                .Select(NormalizeText)
                .Where(part =>
                    !string.IsNullOrWhiteSpace(part)));
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\u00A0', ' ');

        normalized = WhitespaceRegex.Replace(
            normalized,
            " ");

        normalized = ExcessiveLineBreakRegex.Replace(
            normalized,
            "\n\n");

        return normalized.Trim();
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        var columnIndex = 0;
        var hasColumnLetter = false;

        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            hasColumnLetter = true;

            columnIndex =
                columnIndex * 26
                + char.ToUpperInvariant(character)
                - 'A'
                + 1;
        }

        return hasColumnLetter
            ? columnIndex - 1
            : -1;
    }

    private static int CountUsefulCharacters(string text)
    {
        return text.Count(char.IsLetterOrDigit);
    }

    private sealed record WorksheetRow(int RowNumber, IReadOnlyDictionary<int, string> Cells);

    private sealed record StructuredParseResult(string DocumentTitle, IReadOnlyList<ParsedSection> Sections);

    private sealed record InstructionStep(
        int Number,
        string Title,
        string Description);

    private sealed class StepBuilder
    {
        private readonly List<string> _rows = [];

        public StepBuilder(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public void AddRow(IReadOnlyCollection<string> values)
        {
            foreach (var value in values)
            {
                var normalized = NormalizeText(value);

                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _rows.Add(normalized);
                }
            }
        }

        public InstructionStep Build()
        {
            if (_rows.Count == 0)
            {
                return new InstructionStep(
                    Number: Number,
                    Title: string.Empty,
                    Description: string.Empty);
            }

            var title = _rows[0];

            var description = _rows.Count > 1
                ? NormalizeText(
                    string.Join(
                        Environment.NewLine,
                        _rows.Skip(1)))
                : string.Empty;

            return new InstructionStep(
                Number: Number,
                Title: title,
                Description: description);
        }
    }

    private static bool IsDateCell(Cell cell, WorkbookPart workbookPart)
    {
        var styleIndex = cell.StyleIndex?.Value;

        if (!styleIndex.HasValue)
        {
            return false;
        }

        var stylesheet = workbookPart
            .WorkbookStylesPart?
            .Stylesheet;

        var cellFormats = stylesheet?
            .CellFormats;

        if (cellFormats is null || styleIndex.Value >= cellFormats.Count?.Value)
        {
            return false;
        }

        var cellFormat = cellFormats
            .Elements<CellFormat>()
            .ElementAtOrDefault(checked((int)styleIndex.Value));

        var numberFormatId = cellFormat?.NumberFormatId?.Value;

        if (!numberFormatId.HasValue)
        {
            return false;
        }

        /*
        * Beépített Excel dátumformátumok.
        * A 14–22 tartomány a szokásos dátum- és
        * dátum/idő formátumokat fedi le.
        */
        if (numberFormatId.Value is >= 14 and <= 22)
        {
            return true;
        }

        var customFormat = stylesheet?
            .NumberingFormats?
            .Elements<NumberingFormat>()
            .FirstOrDefault(format =>
                format.NumberFormatId?.Value ==
                numberFormatId.Value)?
            .FormatCode?
            .Value;

        if (string.IsNullOrWhiteSpace(customFormat))
        {
            return false;
        }

        return IsDateFormatCode(customFormat);
    }

    private static bool IsDateFormatCode(string formatCode)
    {
        var normalized = formatCode.ToLowerInvariant();

        /*
        * Az idézőjeles literálokat és néhány formázási
        * karaktert eltávolítunk, hogy azok ne okozzanak
        * téves dátumfelismerést.
        */
        normalized = Regex.Replace(
            normalized,
            "\"[^\"]*\"",
            string.Empty);

        normalized = Regex.Replace(
            normalized,
            @"\\.",
            string.Empty);

        normalized = Regex.Replace(
            normalized,
            @"\[[^\]]*\]",
            string.Empty);

        return normalized.Contains('y')
            || normalized.Contains('d')
            || (
                normalized.Contains('m')
                && (
                    normalized.Contains('/')
                    || normalized.Contains('.')
                    || normalized.Contains('-')
                )
            );
    }
}