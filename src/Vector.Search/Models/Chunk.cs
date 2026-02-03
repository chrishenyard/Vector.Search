namespace Vector.Search.Models;

public record Chunk(
    string Id,
    string Path,
    int StartLine,
    int EndLine,
    string Language,
    string Content,
    string Hash
);

public record AskRequest(string Question, int TopK = 8);
public record AskResponse(string Answer, List<Chunk> Sources);
