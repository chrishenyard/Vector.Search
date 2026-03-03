using System.Text;

namespace Vector.Store.Utils;

public static class Files
{
    public static string CreateTempFileName(string filename) => $"{filename}_{Guid.NewGuid()}.txt";

    public static StreamWriter CreateWriter(string path) =>
        new(path, append: true, encoding: Encoding.UTF8);
}
