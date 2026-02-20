using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Collections.Concurrent;
using Vector.Files.Chunking;
using Vector.Search.Hubs;
using Vector.Search.Models;
using Vector.Search.Settings;

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

        app.MapPost("/api/embed", async (
            EmbedRequest request,
            IConfiguration cfg,
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
                ProcessFilesAsync(
                    operationId,
                    request.ConnectionId,
                    cfg,
                    ollama,
                    vectorStore,
                    chunk,
                    hubContext,
                    logger,
                    httpContext.RequestAborted), httpContext.RequestAborted);

            return Results.Accepted(value: new
            {
                OperationId = operationId
            });
        });

        app.MapPost("/api/code", async (
            [FromForm] IFormFile file,
            ILogger<EndPoints> logger) =>
        {
            return Results.Ok(new
            {
                filename = file.FileName,
                length = file.Length
            });
        });
    }

    private static async Task ProcessFilesAsync(
        string operationId,
        string connectionId,
        IConfiguration cfg,
        OllamaClient ollama,
        CodeVectorStore vectorStore,
        IChunk chunk,
        IHubContext<EmbeddingHub> hubContext,
        ILogger<EndPoints> logger,
        CancellationToken requestAborted)
    {
        // Declare a dictionary to test whether the hashing and stable GUID generation is working as expected
        var testDict = new ConcurrentDictionary<string, Guid>();

        var rootPath = cfg["REPO_ROOT"]!;
        var writePath = Path.Combine(Path.GetTempPath(), $"write-{operationId}");

        var extensions = (cfg["FILE_EXTENSIONS"]!)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        var token = cts.Token;

        try
        {
            await vectorStore.EnsureCollectionAsync(token);

            int total = 0;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount / 2, 1)
            };

            Directory.CreateDirectory(writePath);

            await chunk.ProcessChunksAsync(writePath, rootPath, extensions, token);
            var chunks = Directory.EnumerateFiles(writePath, "*.*");

            await Parallel.ForEachAsync(chunks, parallelOptions, async (batch, ct) =>
            {
                var content = await File.ReadAllTextAsync(batch, ct);
                var embeddings = await ollama.EmbedAsync($"{content}", ct);

                var hash = CodeVectorStore.ToSha256(content);
                var id = CodeVectorStore.StableGuidFromString(hash);

                var isUniqueKey = testDict.TryAdd(hash, id);
                if (!isUniqueKey)
                {
                    logger.LogWarning("Hash collision detected for file {FilePath} with hash {Hash}", batch, hash);
                    throw new Exception("Hash collision detected, aborting operation");
                }

                var record = new CodeChunkRecord
                {
                    Id = id,
                    Path = batch,
                    Language = Path.GetExtension(batch).TrimStart('.'),
                    Hash = hash,
                    Content = content,
                    Embedding = embeddings
                };

                Interlocked.Add(ref total, 1);

                // Persist records for this batch (batched upsert can be added later)
                await vectorStore.UpsertAsync([record], ct);

                await hubContext.Clients.Client(connectionId).SendAsync(
                    "ChunkProcessed",
                    new { OperationId = operationId, FilePath = batch, Indexed = total },
                    ct);

            });

            await hubContext.Clients.Client(connectionId).SendAsync(
                "EmbeddingCompleted",
                new { OperationId = operationId, Indexed = total },
                token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background embedding operation {OperationId} failed", operationId);
            try
            {
                await hubContext.Clients.Client(connectionId).SendAsync(
                    "EmbeddingError",
                    new { OperationId = operationId, Error = ex.Message },
                    CancellationToken.None);
            }
            catch
            {
                // Swallow any SignalR failures in background context
            }
        }
        finally
        {
            if (Directory.Exists(writePath))
            {
                try
                {
                    Directory.Delete(writePath, true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to clean up temporary files at {WritePath}", writePath);
                }
            }
        }
    }
}
