using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CompanyCopilot.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public sealed class FeedbackController : ControllerBase
{
    private readonly IKnowledgeStore _store;

    public FeedbackController(IKnowledgeStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Feedback voluntário do visitante. Somente avaliação e comentário são
    /// persistidos; o transcript integral nunca é salvo.
    /// </summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> Submit(
        [FromBody] FeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Requisição inválida." });
        }

        var kind = request.Kind?.ToLowerInvariant() switch
        {
            "positive" or "positivo" => FeedbackKind.Positive,
            "negative" or "negativo" => FeedbackKind.Negative,
            _ => (FeedbackKind?)null
        };

        if (kind is null)
        {
            return BadRequest(new { error = "Avaliação inválida." });
        }

        if (request.Comment?.Length > 2000)
        {
            return BadRequest(new { error = "Comentário muito longo (máximo 2.000 caracteres)." });
        }

        await _store.AddFeedbackAsync(new UserFeedback
        {
            SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "anonimo" : request.SessionId,
            Kind = kind.Value,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.FeedbackReceived,
            DocumentId = null,
            Actor = "visitante",
            Details = $"Avaliação {kind.Value} recebida"
        }, cancellationToken);

        await _store.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

public sealed record FeedbackRequest(string? SessionId, string? Kind, string? Comment);
