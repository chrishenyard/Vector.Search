using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Vector.Files.Chunking;
using Vector.Search.Models;
using Vector.Store.Hubs;
using Vector.Store.Services;
using Vector.Store.Settings;
using Vector.Store.Stores;

namespace Vector.Search.Services;

public class EndPoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/", () => "Vector search is running...");

        // Antiforgery token endpoint - sets cookie automatically
        app.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
                new CookieOptions
                {
                    HttpOnly = false, // Allow JavaScript to read
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict
                });
            return Results.Ok(new { });
        });

        app.MapGet("/debug/config", (
            IOptions<VectorStoreSettings> storeOptions,
            IOptions<OllamaSettings> ollamaOptions) =>
        {
            return Results.Ok(new
            {
                OllamaUrl = ollamaOptions.Value.Url,
                Models = new List<string> { storeOptions.Value.EmbeddingsModel, storeOptions.Value.ChatModel },
                TimeoutMinutes = ollamaOptions.Value.TimeoutFromMinutes,
            });
        });

        app.MapGet("/debug/connection", async (
            IHttpClientFactory httpClientFactory,
            IOptions<OllamaSettings> options,
            ILogger<EndPoints> logger) =>
        {
            var httpClient = httpClientFactory.CreateClient("ollama");
            logger.LogInformation("Testing connection to Ollama at {BaseAddress}", httpClient.BaseAddress);

            var response = await httpClient.GetAsync("/api/tags");
            var content = await response.Content.ReadAsStringAsync();

            return Results.Ok(new
            {
                StatusCode = (int)response.StatusCode,
                IsSuccess = response.IsSuccessStatusCode,
                Content = content,
                BaseAddress = httpClient.BaseAddress?.ToString()
            });
        });

        app.MapGet("/health/ollama", async (
            OllamaApiClient ollamaClient,
            ILogger<EndPoints> logger,
            IOptions<OllamaSettings> options) =>
        {
            logger.LogInformation("Checking Ollama health at {Url}", options.Value.Url);
            var models = await ollamaClient.ListLocalModelsAsync();
            return Results.Ok(new { status = "healthy", models = models.Select(m => m.Name) });
        });

        app.MapPost("/api/embed", async (
            EmbedRequest request,
            IOptions<VectorStoreSettings> options,
            OllamaClient ollama,
            CodeVectorStore vectorStore,
            IChunk chunk,
            IHubContext<EmbeddingHub> hubContext,
            ILogger<EndPoints> logger,
            HttpContext httpContext) =>
        {
            var operationId = Guid.NewGuid().ToString("N");

            await hubContext.Clients.Client(request.ConnectionId).SendAsync(
                "ChunkProcessed",
                new { OperationId = operationId, FilePath = string.Empty },
                httpContext.RequestAborted);

            _ = Task.Run(async () =>
                chunk.ProcessFilesAsync(
                    operationId,
                    request.ConnectionId,
                    ollama,
                    vectorStore,
                    hubContext,
                    logger,
                    httpContext.RequestAborted), httpContext.RequestAborted);

            return Results.Accepted(value: new
            {
                OperationId = operationId
            });
        });

        app.MapPost("/api/ask", async (
            AskRequest req,
            OllamaClient ollama,
            CodeVectorStore vectorStore,
            CancellationToken ct) =>
        {
            var qvec = await ollama.EmbedAsync(req.Question, ct);
            var searches = await vectorStore.SearchAsync(qvec, req.TopK, ct);

            var promptReadTasks = new[]
{
                File.ReadAllTextAsync("Prompts/CodeSystemPrompt.txt", ct),
                File.ReadAllTextAsync("Prompts/CodeUserPrompt.txt", ct)
            };
            var prompts = await Task.WhenAll(promptReadTasks);

            var codeSystemPrompt = prompts[0];
            var codeUserPrompt = prompts[1];
            codeUserPrompt = codeUserPrompt
                .Replace("{{Question}}", req.Question)
                .Replace("{{Context}}", string.Join("\n\n---\n\n", searches.Select(s => s.Record.Content)));

            var answer = await ollama.ChatAsync(codeSystemPrompt, codeUserPrompt, ct);
            var searchResponses = searches
                .Select(s => new SearchResonse(s.Record, s.Score)).ToList();

            return Results.Ok(new AskResponse(answer, searchResponses));
        });
    }
}
