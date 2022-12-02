using ProcessArguments;

namespace CCTweaked.LiveServer;

internal sealed class Config
{
    [Required]
    public string WebSocketUrl { get; set; }
    [Required]
    public string DirectoryPath { get; set; }
}
