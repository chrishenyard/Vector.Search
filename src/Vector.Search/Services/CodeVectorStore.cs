using Microsoft.Extensions.VectorData;
using System.Security.Cryptography;
using System.Text;
using Vector.Search.Models;

namespace Vector.Search.Services;

public sealed class CodeVectorStore(VectorStore vectorStore, IConfiguration cfg)
{
    private readonly VectorStore _vectorStore = vectorStore;
    private readonly string _collectionName = cfg["COLLECTION_NAME"]!;

    private VectorStoreCollection<Guid, CodeChunkRecord> GetCollection()
        => _vectorStore.GetCollection<Guid, CodeChunkRecord>(_collectionName);

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        using var collection = GetCollection();
        await collection.EnsureCollectionExistsAsync(ct); // creates if missing :contentReference[oaicite:11]{index=11}
    }

    public async Task UpsertAsync(IEnumerable<CodeChunkRecord> records, CancellationToken ct)
    {
        using var collection = GetCollection();
        foreach (var r in records)
            await collection.UpsertAsync(r, ct);
    }

    public async Task<IReadOnlyList<(CodeChunkRecord Record, double? Score)>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK,
        CancellationToken ct)
    {
        using var collection = GetCollection();

        // You can pass options (filters, skip, include vectors, vector property, etc.) :contentReference[oaicite:12]{index=12}
        var options = new VectorSearchOptions<CodeChunkRecord>
        {
            IncludeVectors = false
        };

        var results = new List<(CodeChunkRecord, double?)>();

        // SearchAsync returns async stream of VectorSearchResult<TRecord> :contentReference[oaicite:13]{index=13}
        await foreach (var hit in collection.SearchAsync(queryVector, top: topK, options: options, cancellationToken: ct))
        {
            results.Add((hit.Record, hit.Score));
        }

        return results;
    }

    public static string ToSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static Guid StableGuidFromString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
