using System.Net;
using CompanyCopilot.Web.Components;
using CompanyCopilot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiUrl = builder.Configuration["Api:Url"] ?? "http://127.0.0.1:5081";

builder.Services.AddScoped<ChatSessionService>();
builder.Services.AddScoped<VisitorIdProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ChatApiClient>(client =>
{
    client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(2);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    CookieContainer = new CookieContainer()
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// A área administrativa é local: bloqueia /admin fora de loopback,
// mesmo que a URL pública temporária (Cloudflare) alcance esta aplicação.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin")
        && context.Connection.RemoteIpAddress is not null
        && !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-XSS-Protection"] = "0";
    headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self' ws: wss:; frame-ancestors 'none'";

    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
