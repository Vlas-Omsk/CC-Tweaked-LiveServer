namespace CCLiveServer.Tests;

public static class NetUtils
{
    private static int _port = 9999;
    private static object _portLock = new object();

    public static string GetUnusedUrl()
    {
        int port;

        lock (_portLock)
            port = _port--;

        return $"http://localhost:{port}";
    }
}
