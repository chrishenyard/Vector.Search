using System.Security.Cryptography;
using System.Text;
using Vector.Search.Models;
using Vector.Search.Services;

namespace Vector.Search.IO;

public static class File
{
    public static IEnumerable<CodeChunkRecord> ChunkFile(string fullPath, string repoRoot)
    {
        var relPath = Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
        var text = System.IO.File.ReadAllText(fullPath);
        var lines = System.IO.File.ReadAllLines(fullPath);

        string language = fullPath.EndsWith(".cs") ? "csharp"
            : fullPath.EndsWith(".json") ? "json"
            : fullPath.EndsWith(".yml") || fullPath.EndsWith(".yaml") ? "yaml"
            : fullPath.EndsWith(".csproj") ? "xml"
            : "text";

        // Heuristic: chunk by blocks separated by 2+ blank lines OR by size.
        var chunks = new List<(int start, int end)>();
        int start = 0;
        int charCount = 0;
        int blankRun = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            charCount += line.Length + 1;

            if (string.IsNullOrWhiteSpace(line)) blankRun++;
            else blankRun = 0;

            bool splitHere =
                blankRun >= 2 ||
                charCount >= 6000; // keep chunks manageable

            if (splitHere && i > start)
            {
                chunks.Add((start, i));
                start = i + 1;
                charCount = 0;
                blankRun = 0;
            }
        }

        if (start < lines.Length)
            chunks.Add((start, lines.Length - 1));

        foreach (var (s, e) in chunks)
        {
            var content = string.Join("\n", lines.Skip(s).Take(e - s + 1)).Trim();
            if (content.Length < 80) continue; // skip tiny noise

            var hash = ToSha256($"{relPath}:{s}:{e}:{content}");
            yield return new CodeChunkRecord
            {
                Id = CodeVectorStore.StableGuidFromString(hash),
                Path = relPath,
                StartLine = s + 1,
                EndLine = e + 1,
                Language = language,
                Content = content,
                Hash = hash
            };
        }
    }

    static string ToSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
