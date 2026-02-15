using Microsoft.Extensions.VectorData;

namespace Vector.Search.Models;

public class Person
{
    [VectorStoreKey]
    public Guid Key { get; set; }

    [VectorStoreData]
    public string FirstName { get; set; } = string.Empty;

    [VectorStoreData]
    public string LastName { get; set; } = string.Empty;

    [VectorStoreData]
    public int Age { get; set; }

    [VectorStoreData]
    public string Email { get; set; } = string.Empty;

    [VectorStoreData]
    public string Address { get; set; } = string.Empty;

    [VectorStoreData]
    public string PhoneNumber { get; set; } = string.Empty;

    [VectorStoreData]
    public string Biography { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float>? PersonEmbedding { get; set; }

    public override string ToString()
    {
        return $"{FirstName} {LastName} {Age} {Email} {Address} {PhoneNumber} {Biography}";
    }
}
