using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using Vector.Store.Hubs;
using Vector.Store.Models;
using Vector.Store.Services;
using Vector.Store.Settings;
using Vector.Store.Stores;

/*
    Research sources:
    https://weaviate.io/blog/chunking-strategies-for-rag
    https://aws.amazon.com/blogs/database/optimize-generative-ai-applications-with-pgvector-indexing-a-deep-dive-into-ivfflat-and-hnsw-techniques/
*/
namespace Vector.Files.Chunking;

public class CodeChunking(ILogger<CodeChunking> logger) : IChunk

{
    private readonly ILogger<CodeChunking> _logger = logger;
    private const char EndOfBlockMarker = '}';

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
            MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount / 2, 1) // Use half of the available processors to avoid overwhelming the system
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
                            await ChunkCSharpAsync(file, rootPath, writePath, minimumChunkSize, cancellationToken);
                            break;
                        default:
                            _logger.LogWarning("Unsupported file extension {FileExtension} for file {FilePath}", fileExtension, file);
                            break;
                    }
                }
            });
    }

    private static async Task ChunkCSharpAsync(
        string filePath,
        string rootPath,
        string savePath,
        int minimumChunkSize,
        CancellationToken cancellationToken)
    {
        var characterCount = 0;
        var filename = Path.GetFileName(filePath);
        var tempFileName = CreateTempFileName(filename);
        var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);

        StreamWriter? writer = null;
        try
        {
            writer = CreateWriter(tempFilePath);

            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);

                var isEndOfBlock = IsEndOfBlock(line);
                characterCount += line.Length + Environment.NewLine.Length;

                if (characterCount >= minimumChunkSize && isEndOfBlock)
                {
                    // Look ahead until there are no more end of block characters
                    // to avoid splitting in the middle of a code block
                    var writeLastLookAheadLine = false;

                    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (IsEndOfBlock(line))
                        {
                            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                            characterCount += line.Length + Environment.NewLine.Length;
                        }
                        else
                        {
                            writeLastLookAheadLine = true;
                            break;
                        }
                    }

                    await writer.FlushAsync(cancellationToken);
                    await writer.DisposeAsync();

                    tempFileName = CreateTempFileName(filename);
                    tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                    writer = CreateWriter(tempFilePath);

                    if (writeLastLookAheadLine && line != null)
                    {
                        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                        characterCount = line.Length + Environment.NewLine.Length;
                    }
                    else
                    {
                        characterCount = 0;
                    }
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.FlushAsync(cancellationToken);
                await writer.DisposeAsync();
            }
        }
    }

    public static async Task ProcessFilesAsync(
        string operationId,
        string connectionId,
        VectorStoreSettings settings,
        OllamaClient ollama,
        CodeVectorStore vectorStore,
        IChunk chunk,
        IHubContext<EmbeddingHub> hubContext,
        ILogger logger,
        CancellationToken requestAborted)
    {
        // Declare a dictionary to test whether the hashing and stable GUID generation is working as expected
        var testDict = new ConcurrentDictionary<string, Guid>();
        var rootPath = settings.RepositoryPath;
        var writePath = Path.Combine(Path.GetTempPath(), $"write-{operationId}");

        var extensions = settings.FileExtensions
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
            if (Directory.Exists(writePath) && settings.DeleteTemporaryFiles)
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

    private static string CreateTempFileName(string filename) => $"{filename}_{Guid.NewGuid()}.txt";

    private static StreamWriter CreateWriter(string path) =>
        new(path, append: true, encoding: Encoding.UTF8);

    private static bool IsEndOfBlock(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();
        var idx = span.Length - 1;

        // Walk backwards over whitespace only
        while (idx >= 0 && char.IsWhiteSpace(span[idx]))
        {
            idx--;
        }

        return idx >= 0 && span[idx] == EndOfBlockMarker;
    }
}

