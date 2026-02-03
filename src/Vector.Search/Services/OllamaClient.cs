using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;
using Vector.Search.Settings;

namespace Vector.Search.Services;

public sealed class OllamaClient(
    OllamaApiClient ollamaApiClient,
    IOptions<OllamaSettings> options,
    IConfiguration cfg)
{
    private readonly OllamaApiClient _ollamaApiClient = ollamaApiClient;
    private readonly string _embeddingModel = cfg["EMBEDDING_MODEL"]!;
    private readonly string _chatModel = cfg["CHAT_MODEL"]!;
    private readonly OllamaSettings _settings = options.Value;

    public async Task<float[]> EmbedAsync(string input, CancellationToken ct)
    {
        var embeddingRequest = new EmbedRequest
        {
            Model = _embeddingModel,
            Input = [input],
            Options = new RequestOptions
            {
                NumCtx = _settings.EmbeddingsContextWindowSize,
                Temperature = _settings.Temperature
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
                NumCtx = _settings.ChatContextWindowSize,
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
