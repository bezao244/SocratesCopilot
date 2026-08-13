namespace CompanyCopilot.Application.Evidence;

/// <summary>
/// Mensagens padronizadas exibidas ao usuário (recusa e indisponibilidade).
/// Não vazam detalhes internos.
/// </summary>
public static class ChatMessages
{
    public const string NoEvidenceRefusal =
        "Não encontrei informações confiáveis nas fontes aprovadas para responder a essa pergunta. " +
        "Para uma orientação personalizada, procure o atendimento humano da Empresa.";

    public const string ConflictRefusal =
        "Encontrei informações conflitantes entre as fontes aprovadas e não posso dar uma resposta confiável. " +
        "Para esclarecer essa dúvida, procure o atendimento humano da Empresa.";

    public const string Unavailable =
        "O assistente está temporariamente indisponível. Tente novamente em instantes.";

    public const string TooManyRequests =
        "Você excedeu o limite de perguntas. Aguarde alguns minutos e tente novamente.";

    public const string MessageTooLong =
        "A pergunta é muito longa. Escreva em até 2.000 caracteres.";
}
