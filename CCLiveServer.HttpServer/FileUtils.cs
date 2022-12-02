namespace CCLiveServer.HttpServer;

public static class FileUtils
{
    public static bool IsTextFile(string path)
    {
        var extension = Path.GetExtension(path).ToLower();

        return new[]
        {
            ".lua"
        }.Contains(extension);
    }
}
