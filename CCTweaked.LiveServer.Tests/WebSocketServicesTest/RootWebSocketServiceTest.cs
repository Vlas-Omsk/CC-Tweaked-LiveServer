using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer;
using CCTweaked.LiveServer.HttpServer.DTO;
using CCTweaked.LiveServer.HttpServer.WebSocketServices;
using PinkJson2;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CCTweaked.LiveServer.Tests;

public abstract class RootWebSocketServiceTest : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _directory;
    private readonly DirectoryWatcher _watcher;
    private readonly WebSocketServer _webSocketServer;
    private readonly WebSocket _webSocket;
    private readonly Queue<IJson> _messagesQueue = new Queue<IJson>();

    public class RootDirectoryTest : RootWebSocketServiceTest
    {
        public RootDirectoryTest() : base("ws://localhost:8888", "RootWebSocketServiceTestRootDirectoryTest", "")
        {
        }
    }

    public class InnerDirectoryTest : RootWebSocketServiceTest
    {
        public InnerDirectoryTest() : base("ws://localhost:8889", "RootWebSocketServiceTestInnerDirectoryTest", "inner")
        {
        }
    }

    public RootWebSocketServiceTest(string url, string rootDirectory, string directory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _directory = Path.Combine(_rootDirectory, directory);

        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, true);
        Directory.CreateDirectory(_directory);
        _watcher = new DirectoryWatcher(_rootDirectory);
        _webSocketServer = new WebSocketServer(url);
        _webSocketServer.WebSocketServices.AddService<RootWebSocketService>("/", () => new RootWebSocketService(_watcher));
        _webSocketServer.Start();
        _webSocket = new WebSocket(url);
        _webSocket.Message += OnWebSocketMessage;
        _webSocket.Connect();
    }

    private void OnWebSocketMessage(object sender, MessageEventArgs e)
    {
        _messagesQueue.Enqueue(Json.Parse(e.Data));
    }

    private T PullPacket<T>(OutboundPacketType type)
    {
        Thread.Sleep(2000);

        var message = _messagesQueue.Dequeue();
        var packet = message.Deserialize<OutboundPacket<IJson>>();

        Assert.Equal(type, packet.Type);

        return packet.Data.Deserialize<T>();
    }

    private void EnsureMessagesQueueEmpty()
    {
        Assert.Empty(_messagesQueue);
    }

    private void SendPacket<T>(InboundPacket<T> packet)
    {
        _webSocket.Send(packet.Serialize().ToString());
    }

    public void Dispose()
    {
        _webSocketServer.Stop();

        _webSocket.Message -= OnWebSocketMessage;
        _webSocket.Close();

        _watcher.Dispose();

        Directory.Delete(_rootDirectory, true);
    }

    [Fact]
    // Creating directory must send created message
    public void Test1()
    {
        var path = Path.Combine(_directory, "innerDirectory1");

        Directory.CreateDirectory(path);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating file must send created message
    public void Test2()
    {
        var path = Path.Combine(_directory, "innerFile2");

        File.OpenWrite(path).Dispose();

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating and writing file must send created and changed message
    public void Test3()
    {
        var path = Path.Combine(_directory, "innerFile3");

        File.OpenWrite(path).Dispose();

        Thread.Sleep(2000);

        using (var stream = File.Open(path, FileMode.Append))
        {
            stream.Write(new byte[] { 0xFF, 0xFE, 0xFD });
        };

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Changed, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating and double writing file must send created and changed message
    public void Test4()
    {
        var path = Path.Combine(_directory, "innerFile4");

        File.OpenWrite(path).Dispose();

        Thread.Sleep(2000);

        using (var stream = File.Open(path, FileMode.Append))
        {
            stream.Write(new byte[] { 0xFF, 0xFE, 0xFD });
        };

        Thread.Sleep(2000);

        using (var stream = File.Open(path, FileMode.Open))
        {
            stream.Write(new byte[] { 0xFF, 0xFE, 0xFD });
        };

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Changed, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating and moving file must send created and moved message
    public void Test5()
    {
        var path = Path.Combine(_directory, "innerFile5");

        File.OpenWrite(path).Dispose();

        Thread.Sleep(2000);

        var newPath = path + "_new";

        File.Move(path, newPath);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Moved, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, newPath), packet.Path);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [IgnoreOnLinuxFactAttrinute]
    // Creating and deleting file must send created and deleted message
    public void Test6()
    {
        var path = Path.Combine(_directory, "innerFile6");

        File.OpenWrite(path).Dispose();

        Thread.Sleep(2000);

        File.Delete(path);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Deleted, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating and moving directory must send created and moved message
    public void Test7()
    {
        var path = Path.Combine(_directory, "innerDirectory7");

        Directory.CreateDirectory(path);

        Thread.Sleep(2000);

        var newPath = path + "_new";

        Directory.Move(path, newPath);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Moved, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, newPath), packet.Path);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [IgnoreOnLinuxFactAttrinute]
    // Creating and deliting directory must send created and deleted message
    public void Test8()
    {
        var path = Path.Combine(_directory, "innerDirectory8");

        Directory.CreateDirectory(path);

        Thread.Sleep(2000);

        Directory.Delete(path, true);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Deleted, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Creating and moving non empty directory must send created and moved message
    public void Test9()
    {
        var path = Path.Combine(_directory, "innerDirectory9");

        Directory.CreateDirectory(path);

        Thread.Sleep(2000);

        var filePath = Path.Combine(path, "innerFile9");

        File.OpenWrite(filePath).Dispose();

        Thread.Sleep(2000);

        var newPath = path + "_new";

        Directory.Move(path, newPath);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.File, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, filePath), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(true, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Moved, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, newPath), packet.Path);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [IgnoreOnLinuxFactAttrinute]
    // Creating and deliting non empty directory must send created and deleted message
    public void Test10()
    {
        var path = Path.Combine(_directory, "innerDirectory10");

        Directory.CreateDirectory(path);

        Thread.Sleep(2000);

        var filePath = Path.Combine(path, "innerFile10");

        File.OpenWrite(filePath).Dispose();

        Thread.Sleep(2000);

        Directory.Delete(path, true);

        var packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Created, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        packet = PullPacket<ChangedEntryDTO>(OutboundPacketType.EntryChanged);

        Assert.Equal(ChangeTypeDTO.Deleted, packet.ChangeType);
        Assert.Equal(EntryTypeDTO.Directory, packet.EntryType);
        Assert.Equal(Path.GetRelativePath(_rootDirectory, path), packet.Path);
        Assert.Equal(null, packet.OldPath);
        Assert.Equal(false, packet.ContentChanged);

        EnsureMessagesQueueEmpty();
    }

    [Fact]
    // Must send actual tree on GetTreee message
    public void Test11()
    {
        SendPacket(new InboundPacket<object>()
        {
            Type = InboundPacketType.GetTree
        });

        var packet = PullPacket<EntryDTO[]>(OutboundPacketType.EntryTree);

        var actualTree = _watcher.GetEntries().ToList();

        foreach (var entry in packet)
        {
            var actualEntry = actualTree.First(x => x.Path == entry.Path && MapperUtils.Map(x.EntryType) == entry.EntryType);

            actualTree.Remove(actualEntry);
        }

        EnsureMessagesQueueEmpty();
    }
}
