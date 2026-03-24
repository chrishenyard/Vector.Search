using System.Text;
using Vector.DataIngestion.Chunkers;
using Vector.DataIngestion.ParserConfiguration;
using Vector.Search.Tests.Files;

namespace Vector.Tools.Tests.Files;

public class CodeChunkingTests(CodeChunkingFixture codeChunkingFixture) : IClassFixture<CodeChunkingFixture>
{
    private readonly IFileParserFactory _fileParserFactory = codeChunkingFixture.FileParserFactory;
    private readonly DocumentChunker _codeChunking = codeChunkingFixture.CodeChunking;

    [Fact]
    public async Task GetChunksAsync_CreatesChunkFiles_ForLargeInput()
    {
        string[] fileExtensions = [".cs"];

        // Arrange
        var rootPath = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var writePath = "chunks";
        var sourceDir = Path.Combine(rootPath, "src");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(Path.Combine(rootPath, writePath));

        var sourceFile = Path.Combine(sourceDir, "test.cs");

        // Create content > 5000 characters to force multiple chunks
        var sb = new StringBuilder();
        for (int i = 0; i < 300; i++)
        {
            sb.AppendLine($"// Line {i} - {new string('x', 40)}");
        }

        await File.WriteAllTextAsync(sourceFile, sb.ToString());
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await _codeChunking.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 1500);

            // Assert
            var chunkDir = Path.Combine(rootPath, writePath);
            Assert.True(Directory.Exists(chunkDir));

            var chunkFiles = Directory
                .EnumerateFiles(chunkDir, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetChunksAsync_CodeChunkFiles()
    {
        string[] fileExtensions = [".cs"];

        var root = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rootPath = Path.Combine(root, "src");
        var writePath = Path.Combine(root, "write");
        var filePath = Path.Combine(rootPath, "test.cs");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(writePath);
        File.Copy("Resources/Code.txt", filePath, overwrite: true);

        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await _codeChunking.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 1500);

            var chunkFiles = Directory
                .EnumerateFiles(writePath, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetChunksAsync_CodeChunkFiles_NoSmallChunks()
    {
        string[] fileExtensions = [".cs"];
        var root = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rootPath = Path.Combine(root, "src");
        var writePath = Path.Combine(root, "write");
        var filePath = Path.Combine(rootPath, "test.cs");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(writePath);
        File.Copy("Resources/GlobalExceptionHandler.txt", filePath, overwrite: true);

        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await _codeChunking.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 1000);

            var chunkFiles = Directory
                .EnumerateFiles(writePath, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles);
            Assert.Equal(2, chunkFiles.Count);

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
