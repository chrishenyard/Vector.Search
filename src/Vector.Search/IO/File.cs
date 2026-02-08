using System.Security.Cryptography;
using System.Text;
using Vector.Search.Models;

namespace Vector.Search.IO;

public static class File
{
    public static async IAsyncEnumerable<CodeChunkRecord> ChunkFile(
        string filePath,
        string root,
        bool saveChunk = false)
    {
        Directory.CreateDirectory(Path.Combine("chunks", "files"));

        var relPath = Path.GetRelativePath(root, filePath).Replace('\\', '/');
        var lines = System.IO.File.ReadAllLines(filePath);
        var language = GetLanguage(filePath);

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

            if (saveChunk)
            {
                var chunkPath = Path.Combine("chunks", "files", $"{Guid.NewGuid()}.txt");
                await System.IO.File.WriteAllTextAsync(chunkPath, content);
            }

            var hash = ToSha256($"{relPath}:{s}:{e}:{content}");
            yield return new CodeChunkRecord
            {
                Id = StableGuidFromString(hash),
                Path = relPath,
                StartLine = s + 1,
                EndLine = e + 1,
                Language = language,
                Content = content,
                Hash = hash
            };
        }
    }

    private static string GetLanguage(string fullPath)
    {
        var languages = new Dictionary<string, string>
        {
            { ".cs", "csharp" },
            { ".json", "json" },
            { ".yml", "yaml" },
            { ".yaml", "yaml" },
            { ".csproj", "xml" },
            { ".props", "props" },
            { ".targets", "targets" },
            { ".md", "md" },
            { ".sql", "sql" },
            { ".tsx", "typescript" },
            { ".ts", "typescript" },
            { ".html", "html" },
            { ".css", "css" },
            { ".ps1", "power script" }
        };

        foreach (var language in from language in languages
                                 where fullPath.EndsWith(language.Key)
                                 select language)
        {
            return language.Value;
        }

        return "text";
    }

    static string ToSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
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
