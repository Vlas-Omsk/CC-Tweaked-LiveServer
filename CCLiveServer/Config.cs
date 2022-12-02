using ProcessArguments;

namespace CCLiveServer;

internal sealed class Config
{
    [Required]
    public string WebSocketUrl { get; set; }
    [Required]
    public string DirectoryPath { get; set; }
}
