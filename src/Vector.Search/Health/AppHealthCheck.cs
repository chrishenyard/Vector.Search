using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using OllamaSharp;
using Vector.Store.Settings;

namespace Vector.Search.Health;

public class AppHealthCheck(
    OllamaApiClient ollamaClient,
    VectorStore vectorStore,
    IOptions<VectorStoreSettings> options,
    ILogger<AppHealthCheck> logger) : IHealthCheck
{
    private readonly OllamaApiClient _ollamaClient = ollamaClient;
    private readonly VectorStore _vectorStore = vectorStore;
    private readonly VectorStoreSettings settings = options.Value;
    private readonly ILogger<AppHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken token = default)
    {
        _logger.LogDebug("Performing health check...");

        var models = new List<string> { settings.EmbeddingsModel, settings.ChatModel };
        var llmModels = await _ollamaClient.ListLocalModelsAsync(token);

        if (llmModels == null || !llmModels.Any())
        {
            _logger.LogWarning("No models found in Ollama. Health check failed.");
            return HealthCheckResult.Unhealthy("No models found in Ollama.");
        }

        var missingModels = models
            .Where(m => !llmModels.Any(lm => lm.Name.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingModels.Count != 0)
        {
            _logger.LogWarning("Missing models in Ollama: {MissingModels}. Health check failed.", string.Join(", ", missingModels));
            return HealthCheckResult.Unhealthy($"Missing models in Ollama: {string.Join(", ", missingModels)}");
        }

        // Check vector store connectivity by listing collections (you can also perform a more specific check if needed)
        var collections = _vectorStore.ListCollectionNamesAsync(token);

        // Here you can add any custom logic to determine the health of your application
        // For example, you could check database connectivity, external service availability, etc.
        // For simplicity, we'll just return Healthy
        return await Task.FromResult(HealthCheckResult.Healthy("The application is healthy."));
    }
}
