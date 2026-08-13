namespace CompanyCopilot.Api.Infrastructure;

public class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>
    /// Somente quando a aplicação Web estiver atrás do proxy Cloudflare
    /// configurado, o cabeçalho CF-Connecting-IP é aceito.
    /// </summary>
    public bool TrustCloudflareProxy { get; set; } = false;

    /// <summary>
    /// Origem que pode fornecer o IP real do visitante (a aplicação Blazor local).
    /// O IP só é confiado quando a requisição vem desses endereços.
    /// </summary>
    public string[] TrustedClientIpProxies { get; set; } = { "127.0.0.1", "::1" };
}
