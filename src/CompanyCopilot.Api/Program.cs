using CompanyCopilot.Api.Infrastructure;
using CompanyCopilot.Application;
using CompanyCopilot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.Section));

var webUrl = builder.Configuration["Web:Url"] ?? "http://127.0.0.1:8080";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminFromWeb", policy =>
        policy.WithOrigins(webUrl)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddHostedService<IngestionWorker>();

var app = builder.Build();

app.UseSecurityHeaders();

app.MapControllers();

app.Run();

namespace CompanyCopilot.Api
{
    public partial class Program
    {
    }
}
