using System.Text;
using Vector.Files.Chunking;

namespace Vector.Tools.Tests.Files;

public class CodeChunkingTests
{
    [Fact]
    public async Task GetChunksAsync_CreatesChunkFiles_ForLargeInput()
    {
        // File extensions to consider for chunking
        var fileExtensiions = (".cs,.json,.yml,.yaml,.csproj,.props,.targets,.md,.sql,.js,.tsx,.ts,.html,.css,.ps1")
            .Split(',');

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

        var sut = new CodeChunking();
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await sut.GetChunksAsync(writePath, rootPath, fileExtensiions, cts.Token, 5000);

            // Assert
            var chunkDir = Path.Combine(rootPath, writePath);
            Assert.True(Directory.Exists(chunkDir));

            var chunkFiles = Directory
                .EnumerateFiles(chunkDir, "temp_*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            // Optionally assert that each file has content
            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(rootPath))
            {
                try
                {
                    Directory.Delete(rootPath, recursive: true);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }
    }

    [Fact]
    public async Task GetChunksAsync_CodeChunkFiles()
    {
        // File extensions to consider for chunking
        var fileExtensiions = (".cs")
            .Split(',');

        var root = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rootPath = Path.Combine(root, "src");
        var writePath = Path.Combine(root, "write");
        var filePath = Path.Combine(rootPath, "test.cs");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(writePath);
        File.Copy("Resources/Code.txt", filePath, overwrite: true);

        var sut = new CodeChunking();
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await sut.GetChunksAsync(writePath, rootPath, fileExtensiions, cts.Token, 5000);

            var chunkFiles = Directory
                .EnumerateFiles(writePath, "temp_*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            // Optionally assert that each file has content
            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(root))
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }
    }
}
