using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Vector.DataIngestion.Hubs;
using Vector.DataIngestion.Services;
using Vector.DataIngestion.Stores;

namespace Vector.DataIngestion.Chunkers;

public interface IChunk
{
    Task ProcessChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minChunkSize = 1500);

    Task ProcessFilesAsync(
        string operationId,
        string connectionId,
        int minChunkSize,
        OllamaClient ollama,
        CodeVectorStore vectorStore,
        IHubContext<EmbeddingHub> hubContext,
        ILogger logger,
        CancellationToken requestAborted);
}
