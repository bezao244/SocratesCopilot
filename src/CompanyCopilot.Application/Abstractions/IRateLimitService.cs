namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Limite de perguntas por IP em janela fixa e controle de concorrência
/// de geração (uma geração ativa e fila máxima de cinco).
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Registra uma pergunta do IP e informa se ela está dentro do limite.
    /// </summary>
    bool TryConsume(string clientIp);

    /// <summary>
    /// Tenta enfileirar uma geração. Retorna false se a fila estiver cheia.
    /// </summary>
    bool TryEnqueue();

    /// <summary>
    /// Libera uma posição da fila quando a geração termina.
    /// </summary>
    void Release();
}
