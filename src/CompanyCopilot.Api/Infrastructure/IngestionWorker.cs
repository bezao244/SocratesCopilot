using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Infrastructure.Persistence;

namespace CompanyCopilot.Api.Infrastructure;

/// <summary>
/// Worker de ingestão: processa a fila de documentos serialmente.
/// Os embeddings são pausados automaticamente enquanto houver geração de
/// chat ativa (IGenerationGate) para evitar disputa de VRAM.
/// </summary>
public sealed class IngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(IServiceScopeFactory scopeFactory, ILogger<IngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de ingestão iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
                var ingestion = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();

                var jobs = await store.GetQueuedIngestionJobsAsync(stoppingToken);

                if (jobs.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                foreach (var job in jobs)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ingestion.ProcessAsync(job.DocumentId, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo do worker de ingestão");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Worker de ingestão encerrado");
    }
}
