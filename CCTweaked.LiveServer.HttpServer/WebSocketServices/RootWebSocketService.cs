using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer.DTO;
using PinkJson2;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CCTweaked.LiveServer.HttpServer.WebSocketServices;

public sealed class RootWebSocketService : WebSocketBehavior
{
    private readonly DirectoryWatcher _watcher;

    public RootWebSocketService(DirectoryWatcher watcher)
    {
        _watcher = watcher;
    }

    protected override void OnStart()
    {
        Context.WebSocket.FragmentLength = int.MaxValue;
    }

    protected override void OnOpen()
    {
        _watcher.Changed += OnWatcherChanged;
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (!e.IsText)
            return;

        var packet = Json.Parse(e.Data).Deserialize<InboundPacket<IJson>>();

        switch (packet.Type)
        {
            case InboundPacketType.GetTree:
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
                break;
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        _watcher.Changed -= OnWatcherChanged;
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
