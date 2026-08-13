using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Admin;
using CompanyCopilot.Application.Agents;
using CompanyCopilot.Application.Chat;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Ingest;
using CompanyCopilot.Application.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyCopilot.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.Section));
        services.Configure<ChatOptions>(configuration.GetSection(ChatOptions.Section));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.Configure<VectorSearchOptions>(configuration.GetSection(VectorSearchOptions.Section));

        services.AddSingleton<IChatSessionStore, ChatSessionStore>();
        services.AddSingleton<IRateLimitService, RateLimitService>();
        services.AddSingleton<IGenerationGate, GenerationGate>();

        services.AddSingleton(sp =>
            new EvidenceEvaluator(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VectorSearchOptions>>().Value));

        services.AddScoped<ICompanyKnowledgeAgent, CompanyKnowledgeAgent>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IDocumentAdminService, DocumentAdminService>();

        return services;
    }
}
