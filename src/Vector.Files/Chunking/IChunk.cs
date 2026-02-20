namespace Vector.Files.Chunking;

public interface IChunk
{
    Task ProcessChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minimumChunkSize = 5000);
}
