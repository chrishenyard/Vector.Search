using Vector.DataIngestion.Models;

namespace Vector.Search.Models;

public record SearchResonse(CodeChunk Chunk, double? Score);
public record AskResponse(string Answer, List<SearchResonse> SearchResonses);
