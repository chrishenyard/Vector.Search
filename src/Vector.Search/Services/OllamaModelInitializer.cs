using Microsoft.Extensions.Options;
using OllamaSharp;
using Vector.Search.Settings;

namespace Vector.Search.Services;

public class OllamaModelInitializer(
    IServiceProvider serviceProvider,
    IConfiguration cfg,
    ILogger<OllamaModelInitializer> logger,
    IOptions<OllamaSettings> options) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OllamaModelInitializer> _logger = logger;
    private readonly OllamaSettings _ollamaSettings = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var models = new List<string> { cfg["EMBEDDING_MODEL"]!, cfg["CHAT_MODEL"]! };
        _logger.LogDebug("Configured models: {Models}", string.Join(", ", models));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var ollamaClient = scope.ServiceProvider.GetRequiredService<OllamaApiClient>();
            var clientModels = await ollamaClient.ListLocalModelsAsync(cancellationToken);

            foreach (var model in models)
            {
                var modelExists = clientModels.Any(cm => cm.Name.Equals(model, StringComparison.OrdinalIgnoreCase));

                if (!modelExists)
                {
                    _logger.LogDebug("Model {Model} not found. Pulling model...", model);

                    await foreach (var status in ollamaClient.PullModelAsync(model, cancellationToken))
                    {
                        if (status?.Status != null)
                        {
                            _logger.LogDebug("Pull status: {Status} - {Completed}/{Total}",
                                status.Status,
                                status.Completed,
                                status.Total);
                        }
                    }

                    _logger.LogDebug("Model {Model} pulled successfully", model);
                }
                else
                {
                    _logger.LogDebug("Model {Model} already exists", model);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Ollama models");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}