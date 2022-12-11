using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer.DTO;
using CCTweaked.LiveServer.HttpServer.Logging;
using PinkJson2;
using PinkLogging;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CCTweaked.LiveServer.HttpServer.WebSocketServices;

public sealed class RootWebSocketService : WebSocketBehavior
{
    private readonly DirectoryWatcher _watcher;
    private readonly ServiceLogger _logger;

    public RootWebSocketService(DirectoryWatcher watcher, ILogger logger)
    {
        _watcher = watcher;
        _logger = new ServiceLogger(nameof(RootWebSocketService), logger);
    }

    protected override void OnStart()
    {
        _logger.User = Context.UserEndPoint;
        Context.WebSocket.FragmentLength = int.MaxValue;
    }

    protected override void OnOpen()
    {
        _logger.Info($"Opened: {Context.UserEndPoint}");

        _watcher.Changed += OnWatcherChanged;
    }

    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
    {
        _logger.Info($"Error: {Context.UserEndPoint}");
    }

    protected override void OnClose(CloseEventArgs e)
    {
        _logger.Info($"Close: {Context.UserEndPoint}");

        _watcher.Changed -= OnWatcherChanged;
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (!e.IsText)
            return;

        InboundPacket<IJson> packet;

        try
        {
            packet = Json.Parse(e.Data).Deserialize<InboundPacket<IJson>>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex.ToString());
            return;
        }

        _logger.Info($"Message: {Context.UserEndPoint} {packet.Type} {packet.Data}");

        switch (packet.Type)
        {
            case InboundPacketType.GetTree:
                OnGetTree();
                break;
        }
    }

    private void OnGetTree()
    {
        SendPacket(new OutboundPacket<EntryDTO[]>()
        {
            Type = OutboundPacketType.EntryTree,
            Data = _watcher
                .GetEntries()
                .Select(x => new EntryDTO()
                {
                    EntryType = MapperUtils.Map(x.EntryType),
                    Path = x.Path
                })
                .ToArray()
        });
    }

    private void OnWatcherChanged(object sender, ChangedEventArgs e)
    {
        SendPacket(new OutboundPacket<ChangedEntryDTO>()
        {
            Type = OutboundPacketType.EntryChanged,
            Data = new ChangedEntryDTO()
            {
                ChangeType = MapperUtils.Map(e.ChangeType),
                EntryType = MapperUtils.Map(e.EntryType),
                Path = e.Path,
                OldPath = e.OldPath,
                ContentChanged = e.EntryType == DirectoryEntryType.File && (e.ChangeType == DirectoryChangeType.Changed || e.ChangeType == DirectoryChangeType.Created)
            }
        });
    }

    private void SendPacket<T>(OutboundPacket<T> packet)
    {
        Send(packet.Serialize().ToString());
    }
}
