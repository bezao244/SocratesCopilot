using System.Text;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Ingest;
using CompanyCopilot.Application.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace CompanyCopilot.Infrastructure.Extraction;

/// <summary>
/// Extrai texto de PDF, DOCX, XLSX, TXT, Markdown e HTML, preservando
/// localização (página, seção, planilha/célula) para as fontes.
/// PDF sem camada de texto é marcado como rejeitado (OCR fora do escopo).
/// </summary>
public sealed class FileExtractor : IDocumentExtractor
{
    private readonly ILogger<FileExtractor> _logger;

    public FileExtractor(ILogger<FileExtractor> logger)
    {
        _logger = logger;
    }

    public Task<ExtractedDocument> ExtractAsync(
        string filePath,
        string extension,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sections = extension switch
            {
                ".pdf" => ExtractPdf(filePath),
                ".docx" => ExtractDocx(filePath),
                ".xlsx" => ExtractXlsx(filePath),
                ".txt" => ExtractPlainText(filePath, markdown: false),
                ".md" => ExtractPlainText(filePath, markdown: true),
                ".html" or ".htm" => ExtractHtml(filePath),
                _ => Array.Empty<ExtractedSection>()
            };

            return Task.FromResult(
                sections.Count == 0
                    ? new ExtractedDocument(
                        "Nenhum texto extraível foi encontrado. Para PDFs digitalizados, OCR não é suportado nesta versão.",
                        Array.Empty<ExtractedSection>())
                    : new ExtractedDocument(null, sections));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao extrair {File}", filePath);
            return Task.FromResult(new ExtractedDocument(
                "Falha ao extrair o conteúdo do arquivo. Verifique se o formato é suportado e se o arquivo não está corrompido.",
                Array.Empty<ExtractedSection>()));
        }
    }

    private static IReadOnlyList<ExtractedSection> ExtractPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var sections = new List<ExtractedSection>();
        var totalTextLength = 0;

        foreach (var page in document.GetPages())
        {
            var text = page.Text?.ToString() ?? string.Empty;
            totalTextLength += text.Length;

            var normalized = TextChunker.Normalize(text);
            if (normalized.Length > 0)
            {
                sections.Add(new ExtractedSection($"Página {page.Number}", normalized, $"página {page.Number}"));
            }
        }

        if (totalTextLength == 0)
        {
            return Array.Empty<ExtractedSection>();
        }

        return sections;
    }

    private static IReadOnlyList<ExtractedSection> ExtractDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return Array.Empty<ExtractedSection>();
        }

        var sections = new List<ExtractedSection>();
        var currentTitle = "Documento";
        var currentText = new StringBuilder();

        void Flush()
        {
            var text = TextChunker.Normalize(currentText.ToString());
            if (text.Length > 0)
            {
                sections.Add(new ExtractedSection(currentTitle, text, $"seção {currentTitle}"));
            }

            currentText.Clear();
        }

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var text = paragraph.InnerText.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                if (style is not null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                {
                    Flush();
                    currentTitle = text;
                }
                else
                {
                    currentText.Append(' ').Append(text);
                }
            }
        }

        Flush();
        return sections;
    }

    private static IReadOnlyList<ExtractedSection> ExtractXlsx(string filePath)
    {
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = document.WorkbookPart;
        if (workbookPart is null)
        {
            return Array.Empty<ExtractedSection>();
        }

        var workbook = workbookPart.Workbook;
        if (workbook?.Sheets is null)
        {
            return Array.Empty<ExtractedSection>();
        }

        var sections = new List<ExtractedSection>();

        foreach (var sheet in workbook.Sheets.OfType<Sheet>())
        {
            var worksheetPart = workbookPart.GetPartById(sheet.Id!) as WorksheetPart;
            var sheetData = worksheetPart?.Worksheet?.GetFirstChild<SheetData>();
            if (worksheetPart is null || sheetData is null)
            {
                continue;
            }

            var lines = new List<string>();

            foreach (var row in sheetData.OfType<Row>())
            {
                var cells = row.OfType<Cell>()
                    .Select(cell =>
                    {
                        var reference = cell.CellReference?.Value ?? "?";
                        var value = cell.CellValue?.Text;

                        if (value is null)
                        {
                            if (cell.InnerText.Length > 0)
                            {
                                value = cell.InnerText;
                            }
                        }
                        else if (cell.DataType?.Value == CellValues.SharedString
                                 && workbookPart.SharedStringTablePart is not null
                                 && workbookPart.SharedStringTablePart.SharedStringTable is not null
                                 && int.TryParse(value, out var index)
                                 && index >= 0
                                 && index < workbookPart.SharedStringTablePart.SharedStringTable.ChildElements.Count)
                        {
                            value = workbookPart.SharedStringTablePart.SharedStringTable
                                .ChildElements.ElementAtOrDefault(index)?.InnerText;
                        }

                        return $"{reference}: {value ?? string.Empty}";
                    })
                    .Where(c => !c.EndsWith(": "))
                    .ToList();

                if (cells.Count > 0)
                {
                    lines.Add(string.Join(" | ", cells));
                }
            }

            if (lines.Count > 0)
            {
                var title = $"Planilha {sheet.Name}";
                sections.Add(new ExtractedSection(
                    title,
                    TextChunker.Normalize(string.Join("\n", lines)),
                    $"planilha {sheet.Name}"));
            }
        }

        return sections;
    }

    private static IReadOnlyList<ExtractedSection> ExtractPlainText(string filePath, bool markdown)
    {
        var raw = File.ReadAllText(filePath, Encoding.UTF8);
        var sections = new List<ExtractedSection>();

        if (markdown)
        {
            var currentTitle = "Documento";
            var currentText = new StringBuilder();

            void Flush()
            {
                var text = TextChunker.Normalize(currentText.ToString());
                if (text.Length > 0)
                {
                    sections.Add(new ExtractedSection(currentTitle, text, $"seção {currentTitle}"));
                }

                currentText.Clear();
            }

            foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#'))
                {
                    Flush();
                    currentTitle = trimmed.TrimStart('#').Trim();
                }
                else
                {
                    currentText.Append(' ').Append(trimmed);
                }
            }

            Flush();
        }
        else
        {
            foreach (var block in raw.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var text = TextChunker.Normalize(block);
                if (text.Length == 0)
                {
                    continue;
                }

                var title = text.Length <= 60 ? text : text[..60] + "…";
                sections.Add(new ExtractedSection(title, text, $"seção {title}"));
            }
        }

        return sections;
    }

    private static IReadOnlyList<ExtractedSection> ExtractHtml(string filePath)
    {
        var document = new HtmlDocument();
        document.Load(filePath, Encoding.UTF8);

        foreach (var node in document.DocumentNode.DescendantsAndSelf().ToList())
        {
            if (node.Name is "script" or "style" or "head" or "nav" or "footer" or "iframe")
            {
                node.Remove();
            }
        }

        var sections = new List<ExtractedSection>();
        var currentTitle = "Documento";
        var currentText = new StringBuilder();

        void Flush()
        {
            var text = TextChunker.Normalize(currentText.ToString());
            if (text.Length > 0)
            {
                sections.Add(new ExtractedSection(currentTitle, text, $"seção {currentTitle}"));
            }

            currentText.Clear();
        }

        foreach (var node in document.DocumentNode.Descendants())
        {
            if (node.Name.Length == 2 && node.Name[0] == 'h' && node.Name[1] is >= '1' and <= '6')
            {
                Flush();
                currentTitle = HtmlEntity.DeEntitize(node.InnerText).Trim();
            }
            else if (node.Name is "p" or "li" or "td" or "blockquote")
            {
                var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                if (text.Length > 0)
                {
                    currentText.Append(' ').Append(text);
                }
            }
        }

        Flush();
        return sections;
    }
}
