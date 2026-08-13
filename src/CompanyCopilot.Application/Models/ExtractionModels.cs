namespace CompanyCopilot.Application.Models;

/// <summary>
/// Trecho extraído de um arquivo, com localização preservada
/// (página, seção, planilha ou célula) para gerar fontes úteis.
/// </summary>
public sealed record ExtractedSection(string Title, string Text, string Location);

/// <summary>
/// Resultado da extração de um arquivo.
/// Se a extração falhar (ex.: PDF digitalizado), FailureMessage é preenchido
/// e Sections fica vazio — o documento deve ser marcado como rejeitado.
/// </summary>
public sealed record ExtractedDocument(string? FailureMessage, IReadOnlyList<ExtractedSection> Sections);

public sealed record TextChunk(string Content, string Location);
