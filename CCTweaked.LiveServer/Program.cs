using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer;
using CCTweaked.LiveServer.HttpServer.WebSocketServices;
using PinkLogging;
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
        using var logger = new ConsoleLogger();

        var httpServer = new ApplicationHttpServer(config.Url, config.RootDirectory, config.LuaDirectory, logger);
        var watcher = new DirectoryWatcher(config.RootDirectory);

        httpServer.WebSocketServices.AddService<RootWebSocketService>("/", () => new RootWebSocketService(watcher, logger));
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