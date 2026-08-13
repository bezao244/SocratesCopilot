using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Rules;

/// <summary>
/// Regras de domínio para o ciclo de vida e recuperação de documentos.
/// </summary>
public static class DocumentRules
{
    /// <summary>
    /// Categorias com autoridade factual sobre regras de negócio,
    /// pagamento, horários e políticas têm prioridade sobre FAQ e material institucional.
    /// </summary>
    public static bool IsAuthoritativeCategory(DocumentCategory category) =>
        category is DocumentCategory.Rules
            or DocumentCategory.PaymentConditions
            or DocumentCategory.Hours
            or DocumentCategory.Policy;

    /// <summary>
    /// Documentos Authoritative de pagamentos, horários e políticas exigem período inicial
    /// de vigência (ValidFrom) antes de serem aprovados.
    /// </summary>
    public static bool RequiresValidityPeriod(KnowledgeDocument document) =>
        document.Priority == DocumentPriority.Authoritative
        && IsAuthoritativeCategory(document.Category);

    public static bool CanApprove(KnowledgeDocument document) =>
        document.Status is DocumentStatus.Draft or DocumentStatus.Processing or DocumentStatus.Rejected
        && (!RequiresValidityPeriod(document) || document.ValidFrom.HasValue);

    /// <summary>
    /// Somente documentos aprovados, públicos e vigentes na data atual são recuperáveis.
    /// </summary>
    public static bool IsRetrievable(KnowledgeDocument document, DateOnly today)
    {
        if (document.Status != DocumentStatus.Approved)
        {
            return false;
        }

        if (document.Visibility != Visibility.Public)
        {
            return false;
        }

        if (document.ValidFrom.HasValue && document.ValidFrom.Value > today)
        {
            return false;
        }

        if (document.ValidUntil.HasValue && document.ValidUntil.Value < today)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Transições de status permitidas para o ciclo de vida de um documento.
    /// </summary>
    public static bool CanTransition(DocumentStatus current, DocumentStatus target)
    {
        if (current == target)
        {
            return true;
        }

        return target switch
        {
            DocumentStatus.Processing => current is DocumentStatus.Draft or DocumentStatus.Failed,
            DocumentStatus.Approved => current is DocumentStatus.Draft or DocumentStatus.Processing or DocumentStatus.Rejected,
            DocumentStatus.Archived => current is DocumentStatus.Approved,
            DocumentStatus.Rejected => current is DocumentStatus.Draft or DocumentStatus.Processing or DocumentStatus.Archived,
            DocumentStatus.Failed => current is DocumentStatus.Processing,
            DocumentStatus.Draft => current is DocumentStatus.Processing or DocumentStatus.Archived or DocumentStatus.Rejected or DocumentStatus.Failed,
            _ => false
        };
    }
}
