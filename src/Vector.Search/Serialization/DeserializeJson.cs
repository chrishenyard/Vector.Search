using System.Text.Json;

namespace Vector.Search.Serialization;

public static class DeserializeJson
{
    public static T Get<T>(string fileName)
    {
        var jsonString = File.ReadAllText(fileName);
        var result = JsonSerializer.Deserialize<T>(jsonString)!;
        return result;
    }
}
