using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Infrastructure.Extraction;
using CompanyCopilot.Infrastructure.Ollama;
using CompanyCopilot.Infrastructure.Persistence;
using CompanyCopilot.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyCopilot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Copilot")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Copilot não configurada. Copie .env.example para .env.");

        services.AddDbContext<CopilotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddScoped<IKnowledgeStore, KnowledgeStore>();
        services.AddScoped<IChatStore, ChatStore>();
        services.AddScoped<IKnowledgeSearch, KnowledgeSearchService>();
        services.AddScoped<IDocumentExtractor, FileExtractor>();

        services.AddHttpClient<IOllamaClient, OllamaClient>();

        return services;
    }
}
