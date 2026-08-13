using Microsoft.AspNetCore.Http;

namespace CompanyCopilot.Web.Services;

/// <summary>
/// Identificador anônimo e persistente do visitante, usado como dono dos chats.
/// Um cookie HttpOnly é gravado na primeira visita; o valor persiste entre
/// recargas, novas abas e sessões do navegador, isolando o histórico por pessoa.
/// Não há autenticação: este ID é a melhor identificação disponível.
/// </summary>
public sealed class VisitorIdProvider
{
    public const string CookieName = "copilot_visitor";
    public const string HeaderName = "X-Copilot-Visitor-Id";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _visitorId;

    public VisitorIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// ID do visitante atual; cria e persiste o cookie quando ainda não existe.
    /// Quando o HttpContext não está disponível (evento de circuito), o valor é
    /// mantido em memória para o restante do circuito.
    /// </summary>
    public string VisitorId
    {
        get
        {
            if (_visitorId is not null)
            {
                return _visitorId;
            }

            var context = _httpContextAccessor.HttpContext;
            var cookie = context?.Request.Cookies[CookieName];
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                _visitorId = cookie;
                return _visitorId;
            }

            _visitorId = Guid.NewGuid().ToString("N");
            context?.Response.Cookies.Append(CookieName, _visitorId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = CookieLifetime,
                IsEssential = true
            });

            return _visitorId;
        }
    }
}
