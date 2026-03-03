using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Vector.Store.Hubs;
using Vector.Store.Models;
using Vector.Store.ParserConfiguration;
using Vector.Store.Services;
using Vector.Store.Settings;
using Vector.Store.Stores;

/*
    Research sources:
    https://weaviate.io/blog/chunking-strategies-for-rag
    https://aws.amazon.com/blogs/database/optimize-generative-ai-applications-with-pgvector-indexing-a-deep-dive-into-ivfflat-and-hnsw-techniques/
*/
namespace Vector.Files.Chunking;

public class CodeChunking(
    IFileParserFactory fileParserFactory,
    IOptions<VectorStoreSettings> settings,
    ILogger<CodeChunking> logger) : IChunk

{
    private readonly IFileParserFactory _fileParserFactory = fileParserFactory;
    private readonly VectorStoreSettings _settings = settings.Value;
    private readonly ILogger<CodeChunking> _logger = logger;

    public async Task ProcessChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minimumChunkSize = 1000)
    {
        ArgumentException.ThrowIfNullOrEmpty(writePath, nameof(writePath));
        ArgumentException.ThrowIfNullOrEmpty(rootPath, nameof(rootPath));

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Math.Max(_settings.MaxDegreeOfParallelism, 1)
        };

        var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(p => fileExtensions.Any(ext => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        await Parallel.ForEachAsync(files, parallelOptions,
            async (file, cancellationToken) =>
            {
                _logger.LogDebug("Processing file: {FilePath}", file);

                var fileExtension = Path.GetExtension(file);

                if (fileExtension != null)
                {
                    switch (fileExtension.ToLower())
                    {
                        case ".cs":
                            var parser = _fileParserFactory.Create("csharp");
                            await parser.Parse(
                                file,
                                rootPath,
                                writePath,
                                minimumChunkSize,
                                _settings.LookAheadLines,
                                cancellationToken);
                            break;
                        default:
                            _logger.LogWarning("Unsupported file extension {FileExtension} for file {FilePath}", fileExtension, file);
                            break;
                    }
                }
            });
    }

    public async Task ProcessFilesAsync(
        string operationId,
        string connectionId,
        OllamaClient ollama,
        CodeVectorStore vectorStore,
        IHubContext<EmbeddingHub> hubContext,
        ILogger logger,
        CancellationToken requestAborted)
    {
        // Declare a dictionary to test whether the hashing and stable GUID generation is working as expected
        var testDict = new ConcurrentDictionary<string, Guid>();
        var rootPath = _settings.RepositoryPath;
        var writePath = Path.Combine(Path.GetTempPath(), $"write-{operationId}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        var token = cts.Token;

        try
        {
            var extensions = _settings.FileExtensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            await vectorStore.EnsureCollectionAsync(token);

            int total = 0;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Max(_settings.MaxDegreeOfParallelism, 1)
            };

            Directory.CreateDirectory(writePath);
            await ProcessChunksAsync(writePath, rootPath, extensions, token);

            var chunks = Directory.EnumerateFiles(writePath, "*.*");

            await Parallel.ForEachAsync(chunks, parallelOptions, async (batch, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                var content = await File.ReadAllTextAsync(batch, ct);
                var embeddings = await ollama.EmbedAsync(content, ct);

                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
                var hash = Convert.ToHexString(bytes).ToLowerInvariant();
                var id = CodeVectorStore.StableGuidFromString(bytes);

                var isUniqueKey = testDict.TryAdd(hash, id);
                if (!isUniqueKey)
                {
                    logger.LogWarning(
                        "Hash collision detected for file {FilePath} with hash {Hash}", batch, hash);
                    throw new InvalidOperationException(
                        $"Duplicate hash '{hash}' for file '{batch}'.");
                }

                var record = new CodeChunk
                {
                    Id = id,
                    Filename = batch,
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
            if (Directory.Exists(writePath) && _settings.DeleteTemporaryFiles)
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

