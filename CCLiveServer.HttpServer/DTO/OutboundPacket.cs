namespace CCLiveServer.HttpServer.DTO;

public sealed class OutboundPacket<T>
{
    public OutboundPacketType Type { get; set; }
    public T Data { get; set; }
}
