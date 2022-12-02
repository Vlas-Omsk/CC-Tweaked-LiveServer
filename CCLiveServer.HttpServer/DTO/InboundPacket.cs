namespace CCLiveServer.HttpServer.DTO;

public sealed class InboundPacket<T>
{
    public InboundPacketType Type { get; set; }
    public T Data { get; set; }
}
