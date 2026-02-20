namespace Vector.Search.Models;

public record AskRequest(string Question, int TopK = 8);
