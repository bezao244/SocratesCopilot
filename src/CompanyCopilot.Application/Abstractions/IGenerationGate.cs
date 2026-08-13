namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Evita disputa de VRAM: o worker de ingestão pausa novos embeddings
/// enquanto existir uma geração de chat ativa.
/// </summary>
public interface IGenerationGate
{
    /// <summary>
    /// Marca uma geração de chat como ativa. Deve ser disposto ao final.
    /// </summary>
    IDisposable EnterGeneration();

    /// <summary>
    /// Espera até não existir geração de chat ativa antes de gerar embedding.
    /// </summary>
    Task WaitForEmbeddingSlotAsync(CancellationToken cancellationToken = default);
}
