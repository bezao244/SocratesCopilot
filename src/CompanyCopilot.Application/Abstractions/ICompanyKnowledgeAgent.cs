using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Orquestrador RAG determinístico: validação, histórico, embedding, busca
/// híbrida, verificação de evidência/conflito, prompt limitado e streaming.
/// Não é um agente autônomo: nenhuma ferramenta externa é chamada.
/// </summary>
public interface ICompanyKnowledgeAgent
{
    IAsyncEnumerable<ChatEvent> AnswerAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
