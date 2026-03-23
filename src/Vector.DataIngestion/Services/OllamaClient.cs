using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;
using Vector.DataIngestion.Settings;

namespace Vector.DataIngestion.Services;

public sealed class OllamaClient(
    OllamaApiClient ollamaApiClient,
    IOptions<OllamaSettings> ollamaOptions,
    IOptions<VectorStoreSettings> storeOptions)
{
    private readonly OllamaApiClient _ollamaApiClient = ollamaApiClient;
    private readonly string _embeddingModel = storeOptions.Value.EmbeddingsModel;
    private readonly string _chatModel = storeOptions.Value.ChatModel;
    private readonly OllamaSettings _settings = ollamaOptions.Value;

    public async Task<float[]> EmbedAsync(string input, CancellationToken ct)
    {
        var embeddingRequest = new EmbedRequest
        {
            Model = _embeddingModel,
            Input = [input],
            Options = new RequestOptions
            {
                NumCtx = _settings.EmbeddingsContextSize,
                Temperature = _settings.Temperature,
                TopP = _settings.TopP
            }
        };

        var embeddingResponse = await _ollamaApiClient.EmbedAsync(embeddingRequest, ct);

        return embeddingResponse == null
            ? throw new InvalidOperationException("Failed to get embedding from Ollama API.")
            : embeddingResponse.Embeddings.First();
    }

    public async Task<string> ChatAsync(string system, string user, CancellationToken ct)
    {
        var chatRequest = new ChatRequest
        {
            Model = _chatModel,
            Stream = false,
            Messages =
            [
                new (ChatRole.System, system),
                new (ChatRole.User, user)
            ],
            Options = new RequestOptions
            {
                NumCtx = _settings.ChatContextSize,
                Temperature = _settings.Temperature
            }
        };

        var chatResponse = _ollamaApiClient.ChatAsync(chatRequest, cancellationToken: ct);
        var message = new StringBuilder();

        await foreach (var response in chatResponse)
        {
            var content = response?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }
            message.Append(content);
        }

        return message.ToString();
    }
}
