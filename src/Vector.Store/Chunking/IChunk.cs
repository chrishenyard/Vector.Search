using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Vector.Store.Hubs;
using Vector.Store.Services;
using Vector.Store.Stores;

namespace Vector.Files.Chunking;

public interface IChunk
{
    Task ProcessChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minimumChunkSize = 5000);

    Task ProcessFilesAsync(
        string operationId,
        string connectionId,
        OllamaClient ollama,
        CodeVectorStore vectorStore,
        IHubContext<EmbeddingHub> hubContext,
        ILogger logger,
        CancellationToken requestAborted);
}
