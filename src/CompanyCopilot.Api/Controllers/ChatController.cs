using System.Text.Json;
using CompanyCopilot.Api.Infrastructure;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly ICompanyKnowledgeAgent _agent;
    private readonly IChatStore _chatStore;
    private readonly IRateLimitService _rateLimit;
    private readonly ChatOptions _options;
    private readonly SecurityOptions _security;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ICompanyKnowledgeAgent agent,
        IChatStore chatStore,
        IRateLimitService rateLimit,
        IOptions<ChatOptions> options,
        IOptions<SecurityOptions> security,
        ILogger<ChatController> logger)
    {
        _agent = agent;
        _chatStore = chatStore;
        _rateLimit = rateLimit;
        _options = options.Value;
        _security = security.Value;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _chatStore.GetRecentSessionsAsync(
            GetVisitorId(), Math.Clamp(take, 1, 100), cancellationToken);

        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetSessionMessages(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "Identificador de sessão inválido." });
        }

        if (!await _chatStore.ExistsForOwnerAsync(sessionId, GetVisitorId(), cancellationToken))
        {
            return NotFound();
        }

        var messages = await _chatStore.GetMessagesAsync(sessionId, cancellationToken);
        return Ok(messages);
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "Identificador de sessão inválido." });
        }

        await _chatStore.DeleteAsync(sessionId, GetVisitorId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("messages")]
    public async Task Messages([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            await WriteErrorAsync(400, "A pergunta não pode ser vazia.");
            return;
        }

        if (request.Message.Length > _options.MaxQuestionCharacters)
        {
            await WriteErrorAsync(400, ChatMessages.MessageTooLong);
            return;
        }

        var clientIp = GetClientIp();
        if (!_rateLimit.TryConsume(clientIp))
        {
            await WriteErrorAsync(429, ChatMessages.TooManyRequests);
            return;
        }

        if (!_rateLimit.TryEnqueue())
        {
            await WriteErrorAsync(429, "Muitas perguntas em andamento. Aguarde a conclusão da atual.");
            return;
        }

        var cancellationToken = HttpContext.RequestAborted;

        try
        {
            var requestWithOwner = request with { OwnerId = GetVisitorId() };
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "application/x-ndjson";
            Response.Headers.CacheControl = "no-store";

            await foreach (var chatEvent in _agent.AnswerAsync(requestWithOwner, cancellationToken))
            {
                var payload = new
                {
                    type = chatEvent.Type.ToString().ToLowerInvariant(),
                    sessionId = chatEvent.SessionId,
                    text = chatEvent.Text,
                    sources = chatEvent.Sources,
                    errorMessage = chatEvent.ErrorMessage
                };

                await Response.WriteAsync(
                    JsonSerializer.Serialize(payload, JsonOptions) + "\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cliente encerrou o stream de chat");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao transmitir resposta de chat");
        }
        finally
        {
            _rateLimit.Release();
        }
    }

    private async Task WriteErrorAsync(int statusCode, string message)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";
        await Response.WriteAsync(
            JsonSerializer.Serialize(new { error = message, type = "error" }, JsonOptions));
    }

    /// <summary>
    /// Identificador do dono da conversa: o visitante anônimo (cookie persistido
    /// pela Web) chega pelo header X-Copilot-Visitor-Id. Ausente, usa o IP do
    /// cliente como fallback (mesmo mecanismo de GetClientIp).
    /// </summary>
    private string GetVisitorId()
    {
        var visitorId = HttpContext.Request.Headers["X-Copilot-Visitor-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(visitorId))
        {
            return visitorId;
        }

        return GetClientIp();
    }

    /// <summary>
    /// IP real do visitante: usa CF-Connecting-IP somente quando a requisição
    /// vier do proxy Cloudflare confiado; caso contrário, o IP remoto.
    /// Na API, um cliente (Web local) pode repassar o IP via X-Copilot-Client-IP
    /// somente se vier de origem em loopback confiada.
    /// </summary>
    private string GetClientIp()
    {
        var remote = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var forwarded = HttpContext.Request.Headers["X-Copilot-Client-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded)
            && _security.TrustedClientIpProxies.Contains(remote))
        {
            return forwarded;
        }

        if (_security.TrustCloudflareProxy)
        {
            var cf = HttpContext.Request.Headers["CF-Connecting-IP"].ToString();
            if (!string.IsNullOrWhiteSpace(cf))
            {
                return cf;
            }
        }

        return remote;
    }
}
