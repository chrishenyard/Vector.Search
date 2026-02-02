using Microsoft.Extensions.Options;
using OllamaSharp;
using Vector.Search.Settings;

namespace Vector.Search.Services;

public class OllamaModelInitializer(
    IServiceProvider serviceProvider,
    ILogger<OllamaModelInitializer> logger,
    IOptions<OllamaSettings> options) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OllamaModelInitializer> _logger = logger;
    private readonly OllamaSettings _ollamaSettings = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Ollama model: {Model}", _ollamaSettings.VisionModel);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var ollamaClient = scope.ServiceProvider.GetRequiredService<OllamaApiClient>();

            var models = await ollamaClient.ListLocalModelsAsync(cancellationToken);
            var modelExists = models.Any(m => m.Name.Equals(_ollamaSettings.VisionModel, StringComparison.OrdinalIgnoreCase));

            if (!modelExists)
            {
                _logger.LogInformation("Model {Model} not found. Pulling model...", _ollamaSettings.VisionModel);

                await foreach (var status in ollamaClient.PullModelAsync(_ollamaSettings.VisionModel, cancellationToken))
                {
                    if (status?.Status != null)
                    {
                        _logger.LogInformation("Pull status: {Status} - {Completed}/{Total}",
                            status.Status,
                            status.Completed,
                            status.Total);
                    }
                }

                _logger.LogInformation("Model {Model} pulled successfully", _ollamaSettings.VisionModel);
            }
            else
            {
                _logger.LogInformation("Model {Model} already exists", _ollamaSettings.VisionModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Ollama model: {Model}", _ollamaSettings.VisionModel);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}