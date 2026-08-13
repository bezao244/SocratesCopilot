using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Extrai texto de arquivos suportados (PDF, DOCX, XLSX, TXT, MD, HTML),
/// preservando localização (página, seção, planilha/célula).
/// </summary>
public interface IDocumentExtractor
{
    Task<ExtractedDocument> ExtractAsync(
        string filePath,
        string extension,
        CancellationToken cancellationToken = default);
}
