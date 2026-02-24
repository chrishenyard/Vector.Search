using Microsoft.Extensions.VectorData;

namespace Vector.Store.Models;

public sealed class CodeChunk
{
    [VectorStoreKey(StorageName = "id")]
    public Guid Id { get; set; }

    [VectorStoreData(StorageName = "path")]
    public string Path { get; set; } = "";

    [VectorStoreData(StorageName = "language")]
    public string Language { get; set; } = "";

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = "";

    [VectorStoreData(StorageName = "hash")]
    public string Hash { get; set; } = "";

    [VectorStoreVector(
        Dimensions: 768,
        DistanceFunction = DistanceFunction.CosineSimilarity,
        IndexKind = IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
