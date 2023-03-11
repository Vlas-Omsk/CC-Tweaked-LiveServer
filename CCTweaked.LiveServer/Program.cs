using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer;
using CCTweaked.LiveServer.HttpServer.WebSocketServices;
using Microsoft.Extensions.Logging;
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
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole();
        });

        var httpServer = new ApplicationHttpServer(
            config.Url,
            config.RootDirectory,
            config.LuaDirectory,
            loggerFactory.CreateLogger<ApplicationHttpServer>()
        );
        var watcher = new DirectoryWatcher(config.RootDirectory);

        httpServer.WebSocketServices.AddService(
            "/",
            () => new RootWebSocketService(
                watcher,
                loggerFactory.CreateLogger<RootWebSocketService>()
            )
        );
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