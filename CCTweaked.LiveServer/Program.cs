using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer;
using CCTweaked.LiveServer.HttpServer.WebSocketServices;
using ProcessArguments;

namespace CCTweaked.LiveServer;

internal static class Program
{
    private static void Main(string[] args)
    {
        ArgumentsDeserializer.Deserialize<Config>(args, Start);
    }

    private static void Start(Config config)
    {
        var httpServer = new ApplicationHttpServer(config.Url, config.RootDirectory, config.LuaDirectory);
        var watcher = new DirectoryWatcher(config.RootDirectory);
        httpServer.WebSocketServices.AddService<RootWebSocketService>("/", () => new RootWebSocketService(watcher));
        httpServer.Start();

        while (true)
        {
            var line = Console.ReadLine();

            if (line == "exit")
                break;
        }

        httpServer.Stop();
    }
}