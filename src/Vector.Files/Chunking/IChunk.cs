namespace Vector.Files.Chunking;

public interface IChunk
{
    Task GetChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minimumChunkSize = 5000);
}
