using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace CompanyCopilot.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IDocumentAdminService _admin;
    private readonly IDocumentIngestionService _ingestion;
    private readonly IKnowledgeStore _store;
    private readonly ICompanyKnowledgeAgent _agent;
    private readonly IAntiforgery _antiforgery;
    private const string Actor = "admin-local";

    public AdminController(
        IDocumentAdminService admin,
        IDocumentIngestionService ingestion,
        IKnowledgeStore store,
        ICompanyKnowledgeAgent agent,
        IAntiforgery antiforgery)
    {
        _admin = admin;
        _ingestion = ingestion;
        _store = store;
        _agent = agent;
        _antiforgery = antiforgery;
    }

    [HttpGet("antiforgery")]
    public IActionResult Antiforgery()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("documents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Arquivo obrigatório." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _ingestion.ImportAsync(stream, file.FileName, title ?? string.Empty, cancellationToken);

        return result.Outcome == ImportOutcome.Imported
            ? Ok(new { documentId = result.DocumentId, message = "Documento enfileirado para processamento." })
            : Conflict(new { error = result.Error ?? "Não foi possível importar o documento." });
    }

    [HttpGet("documents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListDocuments(CancellationToken cancellationToken) =>
        Ok(await _admin.ListDocumentsAsync(cancellationToken));

    [HttpGet("documents/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await _admin.GetDocumentAsync(id, cancellationToken);
        return document is null ? NotFound(new { error = "Documento não encontrado." }) : Ok(document);
    }

    [HttpPatch("documents/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var error = await _admin.UpdateMetadataAsync(id, request, Actor, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpPost("documents/{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var error = await _admin.ApproveAsync(id, Actor, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpPost("documents/{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var error = await _admin.ArchiveAsync(id, Actor, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpPost("documents/{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest? body, CancellationToken cancellationToken)
    {
        var error = await _admin.RejectAsync(id, Actor, body?.Reason, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpPost("documents/{id:guid}/unpublish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        var error = await _admin.UnpublishAsync(id, Actor, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpPost("documents/{id:guid}/reindex")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reindex(Guid id, CancellationToken cancellationToken)
    {
        var error = await _admin.ReindexAsync(id, Actor, cancellationToken);
        return error is null ? Ok() : BadRequest(new { error });
    }

    [HttpGet("jobs")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListJobs([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await _admin.ListJobsAsync(take, cancellationToken));

    [HttpGet("audit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListAudit([FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await _admin.ListAuditEventsAsync(take, cancellationToken));

    [HttpGet("evaluation-cases")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListEvaluationCases(CancellationToken cancellationToken) =>
        Ok(await _admin.ListEvaluationCasesAsync(cancellationToken));

    [HttpPost("test-question")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestQuestion(
        [FromBody] TestQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "A pergunta não pode ser vazia." });
        }

        var sources = new List<ChatSourceDto>();
        var answer = new System.Text.StringBuilder();
        var outcome = "sem resposta";
        var errorMessage = (string?)null;

        await foreach (var chatEvent in _agent.AnswerAsync(
                     new ChatRequest(null, request.Question), cancellationToken))
        {
            switch (chatEvent.Type)
            {
                case ChatEventType.Delta:
                    answer.Append(chatEvent.Text);
                    break;
                case ChatEventType.Sources:
                    sources.AddRange(chatEvent.Sources ?? Array.Empty<ChatSourceDto>());
                    break;
                case ChatEventType.Refusal:
                    outcome = "recusa";
                    answer.Clear();
                    answer.Append(chatEvent.Text);
                    break;
                case ChatEventType.Error:
                    errorMessage = chatEvent.Text;
                    outcome = "erro";
                    break;
            }
        }

        return Ok(new
        {
            outcome,
            errorMessage,
            sources,
            answer = answer.ToString()
        });
    }

    [HttpPost("evaluation-cases/{id:guid}/run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunEvaluationCase(Guid id, CancellationToken cancellationToken)
    {
        var cases = await _store.GetAllEvaluationCasesAsync(cancellationToken);
        var evaluationCase = cases.FirstOrDefault(c => c.Id == id);
        if (evaluationCase is null)
        {
            return NotFound(new { error = "Caso de avaliação não encontrado." });
        }

        var sources = new List<ChatSourceDto>();
        var answer = new System.Text.StringBuilder();
        var outcome = "sem resposta";
        var errorMessage = (string?)null;

        await foreach (var chatEvent in _agent.AnswerAsync(
                     new ChatRequest(null, evaluationCase.Question), cancellationToken))
        {
            switch (chatEvent.Type)
            {
                case ChatEventType.Delta:
                    answer.Append(chatEvent.Text);
                    break;
                case ChatEventType.Sources:
                    sources.AddRange(chatEvent.Sources ?? Array.Empty<ChatSourceDto>());
                    break;
                case ChatEventType.Refusal:
                    outcome = "recusa";
                    answer.Clear();
                    answer.Append(chatEvent.Text);
                    break;
                case ChatEventType.Error:
                    errorMessage = chatEvent.Text;
                    outcome = "erro";
                    break;
            }
        }

        var hasSources = sources.Count > 0;
        evaluationCase.Result = hasSources && outcome != "erro"
            ? EvaluationResult.Passed
            : EvaluationResult.Failed;
        evaluationCase.LastRunAtUtc = DateTimeOffset.UtcNow;
        evaluationCase.ExecutionNotes =
            $"fontes={sources.Count}; desfecho={outcome}; {(errorMessage is null ? string.Empty : $"erro={errorMessage}")}";

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.TestQuestionExecuted,
            DocumentId = null,
            Actor = Actor,
            Details = $"Caso '{evaluationCase.Question[..Math.Min(80, evaluationCase.Question.Length)]}' → {evaluationCase.Result}"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            evaluationCase.Id,
            evaluationCase.Result,
            evaluationCase.ExecutionNotes,
            sources,
            answer = answer.ToString()
        });
    }
}

public sealed record RejectRequest(string? Reason);

public sealed record TestQuestionRequest(string? Question);
