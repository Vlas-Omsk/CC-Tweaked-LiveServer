using System.Diagnostics;
using System.Net;
using PinkLogging;

namespace CCTweaked.LiveServer.HttpServer.Logging;

public sealed class ServiceLogger : Logger
{
    private readonly string _serviceName;
    private readonly ILogger _logger;

    public ServiceLogger(string serviceName, ILogger logger)
    {
        _serviceName = serviceName;
        _logger = logger;
    }

    public IPEndPoint User { get; set; }

    public override void Log(LogLevel level, StackFrame frame, string message)
    {
        _logger.Log(level, frame, $"[{_serviceName}, {User}] {message}");
    }
}
