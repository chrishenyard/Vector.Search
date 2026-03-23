using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Vector.DataIngestion.Models;
using Vector.DataIngestion.Settings;

namespace Vector.DataIngestion.Stores;

public sealed class CodeVectorStore(
    VectorStore vectorStore,
    IOptions<VectorStoreSettings> options)
{
    private readonly VectorStore _vectorStore = vectorStore;
    private readonly VectorStoreSettings _options = options.Value;

    private VectorStoreCollection<Guid, CodeChunk> GetCollection()
        => _vectorStore.GetCollection<Guid, CodeChunk>(_options.CollectionName);

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        using var collection = GetCollection();
        await collection.EnsureCollectionExistsAsync(ct); // creates if missing :contentReference[oaicite:11]{index=11}
    }

    public async Task UpsertAsync(IEnumerable<CodeChunk> records, CancellationToken ct)
    {
        using var collection = GetCollection();
        foreach (var r in records)
            await collection.UpsertAsync(r, ct);
    }

    public async Task<IReadOnlyList<(CodeChunk Record, double? Score)>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK,
        CancellationToken ct)
    {
        using var collection = GetCollection();

        // You can pass options (filters, skip, include vectors, vector property, etc.) :contentReference[oaicite:12]{index=12}
        var options = new VectorSearchOptions<CodeChunk>
        {
            IncludeVectors = false
        };

        var results = new List<(CodeChunk, double?)>();

        // SearchAsync returns async stream of VectorSearchResult<TRecord> :contentReference[oaicite:13]{index=13}
        await foreach (var hit in collection.SearchAsync(queryVector, top: topK, options: options, cancellationToken: ct))
        {
            results.Add((hit.Record, hit.Score));
        }

        return results;
    }

    public static Guid StableGuidFromString(byte[] bytes)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
