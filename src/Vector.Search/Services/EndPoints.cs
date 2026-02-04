using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Vector.Search.Models;
using Vector.Search.Services;
using Vector.Search.Settings;

namespace AI.Receipts.Services;

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

        app.MapGet("/debug/config", (IConfiguration cfg, IOptions<OllamaSettings> options) =>
        {
            var models = new List<string> { cfg["EMBEDDING_MODEL"]!, cfg["CHAT_MODEL"]! };
            var settings = options.Value;

            return Results.Ok(new
            {
                OllamaUrl = settings.Url,
                Models = models,
                TimeoutMinutes = settings.TimeoutFromMinutes
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

        app.MapPost("/index", async (
            IConfiguration cfg,
            OllamaClient ollama,
            CodeVectorStore vectorStore,
            CancellationToken ct) =>
        {
            var repoRoot = cfg["REPO_ROOT"]!;
            var extensions = (cfg["FILE_EXTENSIONS"]!)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var files = Directory.EnumerateFiles(repoRoot, "*.*", SearchOption.AllDirectories)
                .Where(p => extensions.Any(ext => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Where(p => !p.Contains("/bin/") && !p.Contains("/obj/"))
                .ToList();

            await vectorStore.EnsureCollectionAsync(ct);

            int total = 0;
            foreach (var file in files)
            {
                var chunks = Vector.Search.IO.File.ChunkFile(file, repoRoot).ToList();
                if (chunks.Count == 0) continue;

                foreach (var batch in chunks.Chunk(16))
                {
                    var records = new List<CodeChunkRecord>();

                    foreach (var c in batch)
                    {
                        var embeddingsVector = await ollama.EmbedAsync($"{c.Path}\n{c.Content}", ct);

                        c.Embedding = embeddingsVector;
                        records.Add(c);
                        total++;
                    }

                    await vectorStore.UpsertAsync(records, ct);
                }
            }

            return Results.Ok(new { indexed = total });
        });

        app.MapPost("/api/code", async (
            [FromForm] IFormFile file,
            ILogger<EndPoints> logger) =>
        {
            return Results.Ok(new { filename = file.FileName, length = file.Length });
        });
    }
}
