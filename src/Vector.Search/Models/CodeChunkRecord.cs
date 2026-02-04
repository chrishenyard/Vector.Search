using Microsoft.Extensions.VectorData;

namespace Vector.Search.Models;

public sealed class CodeChunkRecord
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string Path { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public int StartLine { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public int EndLine { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string Language { get; set; } = "";

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string Hash { get; set; } = "";

    [VectorStoreVector(
        Dimensions: 768,
        DistanceFunction = DistanceFunction.CosineSimilarity,
        IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
